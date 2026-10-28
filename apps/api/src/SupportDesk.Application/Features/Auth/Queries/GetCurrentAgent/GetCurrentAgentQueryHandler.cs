using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Queries.GetCurrentAgent;

/// <summary>
/// Returns the agent a valid token belongs to. Read from the database rather than the token,
/// so an agent deactivated since signing in is turned away.
/// </summary>
public sealed class GetCurrentAgentQueryHandler(IAgentRepository agents)
{
    /// <exception cref="AuthenticationFailedException">The agent no longer exists or is inactive.</exception>
    public async Task<CurrentAgentDto> HandleAsync(int agentId, CancellationToken ct)
    {
        var agent = await agents.GetByIdAsync(agentId, ct);

        if (agent is null || !agent.IsActive)
        {
            throw new AuthenticationFailedException("Your session is no longer valid. Please sign in again.");
        }

        return new CurrentAgentDto(agent.Id, agent.FullName, agent.Email);
    }
}
