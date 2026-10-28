using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Repositories;
using SupportDesk.Domain.Triage;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Raises a ticket's priority one level, recomputes its due date and re-checks its owner,
/// recording the change in the ticket's escalation history.
/// </summary>
public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentQueries agents,
    ITicketQueries ticketQueries,
    TicketTriage triage,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ILogger<EscalateTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">No ticket has this id.</exception>
    /// <exception cref="BusinessRuleViolationException">The ticket is Critical, Resolved or Closed.</exception>
    /// <param name="escalatedBy">The signed-in agent's name, taken from their access token.</param>
    public async Task<EscalationResultDto> HandleAsync(
        int id, EscalateTicketRequest request, string escalatedBy, CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(id, ct) ?? throw new NotFoundException("Ticket", id);

        // Checked before loading anything else, so a refused escalation costs one query.
        ticket.EnsureCanBeEscalated();

        // A ticket cannot exist without its customer and category (foreign keys), so these are
        // only missing if the data is broken.
        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);
        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        var now = clock.UtcNow;

        var decision = triage.ForEscalation(
            ticket, category, customer.Tier, await agents.GetTriageCandidatesAsync(ct), now);

        var escalation = ticket.Escalate(decision, request.Reason, escalatedBy, now);

        await tickets.AddEscalationAsync(escalation, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} escalated from {From} to {To} by {EscalatedBy}.",
            ticket.Reference, escalation.FromPriority, escalation.ToPriority, escalation.EscalatedBy);

        var recorded = await ticketQueries.GetEscalationAsync(escalation.Id, ct)
            ?? throw new InvalidOperationException($"Escalation {escalation.Id} was saved but cannot be read back.");

        return new EscalationResultDto(await getTicket.HandleAsync(id, ct), recorded);
    }
}
