using Microsoft.Extensions.Configuration;

namespace SeniorConnect.Modules.Identity.Infrastructure;

/// <summary>
/// Single source of truth for JWT signing/validation settings. Both the token
/// issuer (<see cref="JwtTokenService"/>) and the API's JwtBearer validation
/// resolve through here, so they can never read different keys.
/// </summary>
public sealed record JwtSettings(string SecretKey, string Issuer, string Audience)
{
    /// <summary>Development-only fallback. Startup refuses it outside Development.</summary>
    public const string DevelopmentSecret =
        "SeniorConnect_jwt_super_secret_signing_key_2026_default_secure_key_123456";

    private const int MinimumSecretLength = 32;

    public bool IsDevelopmentSecret => SecretKey == DevelopmentSecret;

    public static JwtSettings Resolve(IConfiguration configuration)
    {
        var secret = configuration["JWT_SECRET_KEY"]
            ?? configuration["JWT_SECRET"]
            ?? configuration["Jwt:SecretKey"];

        return new JwtSettings(
            string.IsNullOrWhiteSpace(secret) ? DevelopmentSecret : secret,
            configuration["JWT_ISSUER"] ?? configuration["Jwt:Issuer"] ?? "SeniorConnect.Api",
            configuration["JWT_AUDIENCE"] ?? configuration["Jwt:Audience"] ?? "SeniorConnect.Client");
    }

    /// <summary>Fails startup when a non-Development host runs with a missing, weak or public key.</summary>
    public void EnsureSafeFor(bool isDevelopment)
    {
        if (isDevelopment)
        {
            return;
        }

        if (IsDevelopmentSecret || SecretKey.Length < MinimumSecretLength)
        {
            throw new InvalidOperationException(
                $"JWT_SECRET_KEY must be set to a private value of at least {MinimumSecretLength} characters outside Development.");
        }
    }
}
