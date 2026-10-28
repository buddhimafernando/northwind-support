using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Application.Abstractions;

/// <summary>A signed access token and the moment it stops being accepted.</summary>
public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);

/// <summary>
/// Issues access tokens for signed-in agents. The token format and signing key are a
/// presentation concern, so the application only sees this.
/// </summary>
public interface IAccessTokenIssuer
{
    AccessToken Issue(Agent agent);
}
