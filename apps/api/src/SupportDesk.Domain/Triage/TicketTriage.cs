using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Triage;

/// <summary>
/// What triage decided for a ticket, with a human-readable reason for each part.
/// </summary>
public sealed record TriageDecision(
    TicketPriority Priority,
    string PriorityReason,
    DateTime DueAtUtc,
    string SlaReason,
    int? AssignedAgentId,
    string AssignmentReason);

/// <summary>
/// Decides priority, due date and owner, both when a ticket is raised and when it is
/// escalated. Both paths share the same SLA and assignment rules.
/// </summary>
public sealed class TicketTriage(SlaPolicy slaPolicy)
{
    public TriageDecision ForNewTicket(
        Category category,
        CustomerTier tier,
        TicketPriority? requestedPriority,
        IReadOnlyCollection<AgentCandidate> agents,
        DateTime nowUtc)
    {
        var (priority, priorityReason) = category.ForcesCriticalPriority
            ? (TicketPriority.Critical, $"Critical because every {category.Name} ticket is treated as critical.")
            : requestedPriority is { } requested
                ? (requested, $"{requested} as requested.")
                : (TicketPriority.Medium, "Medium because no priority was requested.");

        var (agent, assignmentReason) = AgentSelector.Choose(category, agents);

        return Decide(priority, priorityReason, tier, agent?.Id, assignmentReason, nowUtc);
    }

    /// <exception cref="Exceptions.BusinessRuleViolationException">The ticket cannot be escalated.</exception>
    public TriageDecision ForEscalation(
        Ticket ticket,
        Category category,
        CustomerTier tier,
        IReadOnlyCollection<AgentCandidate> agents,
        DateTime nowUtc)
    {
        ticket.EnsureCanBeEscalated();

        // The enum is ordered by urgency, so one level up is the next value.
        var priority = ticket.Priority + 1;

        var (agent, assignmentReason) = AgentSelector.KeepOrChoose(ticket.AssignedAgentId, category, agents);

        return Decide(priority, $"Escalated from {ticket.Priority} to {priority}.", tier, agent?.Id, assignmentReason, nowUtc);
    }

    private TriageDecision Decide(
        TicketPriority priority,
        string priorityReason,
        CustomerTier tier,
        int? agentId,
        string assignmentReason,
        DateTime windowStartUtc)
    {
        var window = slaPolicy.WindowFor(priority, tier);

        var slaReason = $"{window.TotalHours:0.##} h window for a {priority} ticket from a {tier} customer.";

        return new TriageDecision(
            priority, priorityReason, windowStartUtc + window, slaReason, agentId, assignmentReason);
    }
}
