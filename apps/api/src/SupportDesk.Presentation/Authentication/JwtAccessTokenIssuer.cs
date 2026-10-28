using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Presentation.Authentication;

/// <summary>Issues HMAC-SHA256 signed JWTs that the bearer handler configured alongside it accepts.</summary>
public sealed class JwtAccessTokenIssuer(JwtOptions options, IClock clock) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();
    private readonly SigningCredentials _credentials =
        new(new SymmetricSecurityKey(options.SigningKeyBytes), SecurityAlgorithms.HmacSha256);

    public AccessToken Issue(Agent agent)
    {
        var now = clock.UtcNow;
        var expires = now.AddMinutes(options.AccessTokenMinutes);

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = _credentials,
            Subject = new ClaimsIdentity(
            [
                new Claim(AgentClaims.Id, agent.Id.ToString()),
                new Claim(AgentClaims.Name, agent.FullName),
                new Claim(AgentClaims.Email, agent.Email)
            ])
        });

        return new AccessToken(token, expires);
    }
}
