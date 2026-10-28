using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Domain.Triage;

namespace SupportDesk.Application.Abstractions;

/// <summary>
/// The read side of agents.
/// </summary>
public interface IAgentQueries
{
    Task<IReadOnlyList<AgentDto>> GetAllAsync(CancellationToken ct);

    /// <summary>Every agent with just what triage needs: status, limit, workload and specializations.</summary>
    Task<IReadOnlyList<AgentCandidate>> GetTriageCandidatesAsync(CancellationToken ct);
}
