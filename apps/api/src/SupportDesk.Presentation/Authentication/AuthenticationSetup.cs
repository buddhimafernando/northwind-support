using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;

namespace SupportDesk.Presentation.Authentication;

public static class AuthenticationSetup
{
    /// <summary>
    /// JWT bearer authentication, and a fallback policy that makes every endpoint require a
    /// signed-in agent unless it opts out with <c>[AllowAnonymous]</c>.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException($"Configuration section '{JwtOptions.SectionName}' is missing.");
        options.Validate();

        services.AddSingleton(options);
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(bearer =>
            {
                // Keep the short JWT claim names ("sub", "name") instead of mapping them to URIs.
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = options.Issuer,
                    ValidAudience = options.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(options.SigningKeyBytes),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = AgentClaims.Name,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }
}
