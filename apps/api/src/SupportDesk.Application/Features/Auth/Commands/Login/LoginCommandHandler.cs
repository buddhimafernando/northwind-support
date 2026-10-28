using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Commands.Login;

/// <summary>
/// Signs an agent in with email and password and issues an access token.
/// </summary>
/// <remarks>
/// Every failure - unknown email, wrong password, inactive agent - gets the same message, so
/// the response never reveals which emails belong to an agent.
/// </remarks>
public sealed class LoginCommandHandler(
    IAgentRepository agents,
    IPasswordHasher<Agent> passwordHasher,
    IAccessTokenIssuer tokens,
    IUnitOfWork unitOfWork,
    ILogger<LoginCommandHandler> logger)
{
    public const string InvalidCredentials = "Invalid email or password.";

    // Verified against when the email is unknown, so that case takes as long as a wrong password.
    private static string? s_decoyHash;

    /// <exception cref="AuthenticationFailedException">The credentials are not accepted.</exception>
    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var agent = await agents.GetByEmailAsync(request.Email.Trim(), ct);

        var result = agent is { PasswordHash.Length: > 0 }
            ? passwordHasher.VerifyHashedPassword(agent, agent.PasswordHash, request.Password)
            : VerifyAgainstDecoy(request.Password);

        if (agent is null || !agent.IsActive || result == PasswordVerificationResult.Failed)
        {
            logger.LogInformation("Failed sign-in attempt.");
            throw new AuthenticationFailedException(InvalidCredentials);
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            // The hash was made with older settings; upgrade it now that we have the password.
            agent.SetPasswordHash(passwordHasher.HashPassword(agent, request.Password));
            await unitOfWork.SaveChangesAsync(ct);
        }

        var token = tokens.Issue(agent);

        logger.LogInformation("Agent {AgentId} signed in.", agent.Id);

        return new LoginResponse(token.Token, token.ExpiresAtUtc, new CurrentAgentDto(agent.Id, agent.FullName, agent.Email));
    }

    private PasswordVerificationResult VerifyAgainstDecoy(string password)
    {
        s_decoyHash ??= passwordHasher.HashPassword(null!, Guid.NewGuid().ToString());
        passwordHasher.VerifyHashedPassword(null!, s_decoyHash, password);

        return PasswordVerificationResult.Failed;
    }
}
