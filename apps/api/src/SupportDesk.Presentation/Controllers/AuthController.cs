using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Application.Features.Auth.Queries.GetCurrentAgent;
using SupportDesk.Presentation.Authentication;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    /// <summary>
    /// Signs an agent in and returns an access token. A wrong email, a wrong password and an
    /// inactive agent all get the same 401.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public Task<LoginResponse> Login(
        LoginRequest request,
        [FromServices] LoginCommandHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(request, ct);

    /// <summary>The signed-in agent, or 401.</summary>
    [HttpGet("me")]
    public Task<CurrentAgentDto> Me([FromServices] GetCurrentAgentQueryHandler handler, CancellationToken ct) =>
        handler.HandleAsync(User.AgentId(), ct);
}
