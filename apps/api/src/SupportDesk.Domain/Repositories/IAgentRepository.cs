using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Loads <see cref="Agent"/> aggregates.
/// </summary>
public interface IAgentRepository
{
    Task<Agent?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>Loads a tracked agent by sign-in email, or null when none has it.</summary>
    Task<Agent?> GetByEmailAsync(string email, CancellationToken ct);
}
