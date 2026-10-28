using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.Domain.Triage;

/// <summary>
/// An agent as seen by triage: who they are, what they may handle and how busy they are.
/// </summary>
/// <param name="OpenTicketCount">Tickets assigned to them that are not Resolved or Closed.</param>
public sealed record AgentCandidate(
    int Id,
    string FullName,
    bool IsActive,
    int MaxOpenTickets,
    int OpenTicketCount,
    IReadOnlyCollection<int> SpecializationCategoryIds)
{
    /// <summary>Active, and a specialist in the category when the category demands one.</summary>
    public bool IsQualifiedFor(Category category) =>
        IsActive && (!category.RequiresSpecialist || SpecializationCategoryIds.Contains(category.Id));
}

/// <summary>
/// Picks the owner for a ticket. Pure: works on a snapshot of the agents passed in.
/// </summary>
public static class AgentSelector
{
    /// <summary>
    /// The qualified agent with room for one more ticket and the fewest open tickets; ties go
    /// to the lowest id so the result never depends on the order agents were loaded in.
    /// </summary>
    /// <returns>The chosen agent, or null with the reason nobody was eligible.</returns>
    public static (AgentCandidate? Agent, string Reason) Choose(Category category, IReadOnlyCollection<AgentCandidate> agents)
    {
        var qualified = agents.Where(a => a.IsQualifiedFor(category)).ToList();

        var chosen = qualified
            .Where(a => a.OpenTicketCount < a.MaxOpenTickets)
            .OrderBy(a => a.OpenTicketCount)
            .ThenBy(a => a.Id)
            .FirstOrDefault();

        if (chosen is not null)
        {
            return (chosen, $"Assigned to {chosen.FullName}, who had the fewest open tickets " +
                $"({chosen.OpenTicketCount} of {chosen.MaxOpenTickets}) among eligible agents.");
        }

        var who = category.RequiresSpecialist ? $"{category.Name} specialist" : "agent";

        return (null, qualified.Count == 0
            ? $"Left unassigned because no active {who} is available."
            : $"Left unassigned because every active {who} is at their open-ticket limit.");
    }

    /// <summary>
    /// Keeps the current owner when they may still hold the ticket, otherwise chooses again.
    /// The ticket is already in the owner's open count, so keeping it does not need spare
    /// capacity - only that they are not over their limit.
    /// </summary>
    public static (AgentCandidate? Agent, string Reason) KeepOrChoose(
        int? currentAgentId, Category category, IReadOnlyCollection<AgentCandidate> agents)
    {
        var current = agents.FirstOrDefault(a => a.Id == currentAgentId);

        if (current is not null && current.IsQualifiedFor(category) && current.OpenTicketCount <= current.MaxOpenTickets)
        {
            return (current, $"Kept with {current.FullName}, who is still eligible.");
        }

        var (chosen, reason) = Choose(category, agents);

        return current is null
            ? (chosen, reason)
            : (chosen, $"{current.FullName} is no longer eligible. {reason}");
    }
}
