namespace SupportDesk.Application.Contracts.Auth;

/// <summary>Body of POST /api/auth/login.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>The signed-in agent, enough to greet them.</summary>
public sealed record CurrentAgentDto(int Id, string FullName, string Email);

/// <summary>Response of POST /api/auth/login.</summary>
public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, CurrentAgentDto Agent);
