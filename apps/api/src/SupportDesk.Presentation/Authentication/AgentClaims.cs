using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace SupportDesk.Presentation.Authentication;

/// <summary>
/// The claims an access token carries, under their standard short JWT names, and how to read
/// them back from the signed-in user.
/// </summary>
public static class AgentClaims
{
    public const string Id = JwtRegisteredClaimNames.Sub;
    public const string Name = JwtRegisteredClaimNames.Name;
    public const string Email = JwtRegisteredClaimNames.Email;

    /// <remarks>Only reachable behind the fallback policy, so a validated token is present.</remarks>
    public static int AgentId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(Id) ?? throw MissingClaim(Id));

    public static string AgentName(this ClaimsPrincipal user) =>
        user.FindFirstValue(Name) ?? throw MissingClaim(Name);

    private static InvalidOperationException MissingClaim(string claim) =>
        new($"The access token has no '{claim}' claim.");
}
