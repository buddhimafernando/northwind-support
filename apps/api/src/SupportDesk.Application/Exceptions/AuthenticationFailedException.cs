namespace SupportDesk.Application.Exceptions;

/// <summary>
/// Thrown when a caller cannot be identified: wrong credentials, or a token for an agent who
/// can no longer sign in. Surfaces as HTTP 401.
/// </summary>
public sealed class AuthenticationFailedException : Exception
{
    public AuthenticationFailedException(string message) : base(message)
    {
    }
}
