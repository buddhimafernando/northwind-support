using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Triage;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketCommandHandlerTests
{
    private static readonly EscalateTicketRequest Request = new("Customer is losing sales");
    private const string SignedInAgent = "Team lead";

    [Fact]
    public async Task HandleAsync_EscalatesFromNow_AndStoresTheHistoryRow()
    {
        var context = new TicketCommandTestContext();
        context.Candidates.Add(new AgentCandidate(1, "Alex Turner", true, 10, 5, []));
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(TicketPriority.Medium).AssignedTo(1).Build());
        context.Clock.Advance(TimeSpan.FromHours(10));

        await context.EscalateTicket.HandleAsync(ticket.Id, Request, SignedInAgent, CancellationToken.None);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(context.Clock.UtcNow.AddHours(8), ticket.DueAtUtc);
        Assert.Equal(1, ticket.AssignedAgentId);

        var escalation = context.AddedEscalation;
        Assert.NotNull(escalation);
        Assert.Equal(TicketPriority.Medium, escalation.FromPriority);
        Assert.Equal("Team lead", escalation.EscalatedBy);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WhenTicketDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();

        await Assert.ThrowsAsync<NotFoundException>(
            () => context.EscalateTicket.HandleAsync(404, Request, SignedInAgent, CancellationToken.None));

        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenCritical_ThrowsWithoutLoadingAgentsOrSaving()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(TicketPriority.Critical).Build());

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request, SignedInAgent, CancellationToken.None));

        Assert.Contains("already Critical", exception.Message);
        context.AgentQueries.Verify(a => a.GetTriageCandidatesAsync(It.IsAny<CancellationToken>()), Times.Never());
        Assert.Null(context.AddedEscalation);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenTheOwnerIsNoLongerEligible_ReassignsAndRecordsBoth()
    {
        var context = new TicketCommandTestContext();
        context.Candidates.Add(new AgentCandidate(1, "Yuki Tanaka", false, 8, 0, []));
        context.Candidates.Add(new AgentCandidate(2, "Ben Osei", true, 8, 3, []));
        var ticket = context.ExistingTicket(new TicketBuilder().AssignedTo(1).Build());

        await context.EscalateTicket.HandleAsync(ticket.Id, Request, SignedInAgent, CancellationToken.None);

        Assert.Equal(2, ticket.AssignedAgentId);
        Assert.Equal(1, context.AddedEscalation?.FromAgentId);
        Assert.Equal(2, context.AddedEscalation?.ToAgentId);
    }

    [Fact]
    public async Task HandleAsync_WhenResolved_ThrowsSayingSo()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithStatus(TicketStatus.Resolved).Build());

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request, SignedInAgent, CancellationToken.None));

        Assert.Contains("Resolved", exception.Message);
        Assert.Equal(TicketPriority.Medium, ticket.Priority);
        context.VerifySaved(Times.Never());
    }
}
