using System.Text;

namespace SupportDesk.Presentation.Authentication;

/// <summary>
/// The <c>Jwt</c> configuration section. The signing key is deliberately absent from every
/// appsettings file: it comes from user-secrets in development, or the <c>Jwt__SigningKey</c>
/// environment variable anywhere else.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    private const int MinimumKeyBytes = 32; // HMAC-SHA256 needs a 256-bit key.

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string SigningKey { get; init; } = string.Empty;

    public int AccessTokenMinutes { get; init; }

    public byte[] SigningKeyBytes => Encoding.UTF8.GetBytes(SigningKey);

    /// <summary>Fails fast at startup, rather than on the first sign-in.</summary>
    /// <exception cref="InvalidOperationException">A value is missing or unsafe.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException($"{SectionName}:Issuer and {SectionName}:Audience are required.");
        }

        if (SigningKeyBytes.Length < MinimumKeyBytes)
        {
            throw new InvalidOperationException(
                $"{SectionName}:SigningKey must be at least {MinimumKeyBytes} bytes. Set it with user-secrets " +
                "or the Jwt__SigningKey environment variable - see README.md.");
        }

        if (AccessTokenMinutes <= 0)
        {
            throw new InvalidOperationException($"{SectionName}:AccessTokenMinutes must be greater than zero.");
        }
    }
}
