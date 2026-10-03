using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Identity.Application;

namespace SeniorConnect.Modules.Identity.Infrastructure;

/// <summary>
/// Validates a Google Sign-In ID token against Google's published signing keys:
/// signature, issuer, audience (our OAuth client id) and lifetime. Without a
/// configured client id every token is rejected.
/// </summary>
public sealed class GoogleIdTokenValidator : IGoogleIdTokenValidator
{
    private const string JwksUrl = "https://www.googleapis.com/oauth2/v3/certs";
    private static readonly string[] ValidIssuers = ["accounts.google.com", "https://accounts.google.com"];
    private static readonly TimeSpan KeyCacheDuration = TimeSpan.FromHours(6);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly SemaphoreSlim KeyLock = new(1, 1);
    private static IList<SecurityKey>? _cachedKeys;
    private static DateTime _keysFetchedAtUtc;

    private readonly string? _clientId;

    public GoogleIdTokenValidator(IConfiguration configuration)
    {
        _clientId = configuration["GOOGLE_CLIENT_ID"] ?? configuration["Authentication:Google:ClientId"];
    }

    public async Task<Result<GoogleIdTokenPayload>> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_clientId))
        {
            return Result<GoogleIdTokenPayload>.Failure(new Error(
                "EXTERNAL_PROVIDER_NOT_CONFIGURED", "Google sign-in is not configured.", ErrorKind.Unauthenticated));
        }

        if (string.IsNullOrWhiteSpace(idToken))
        {
            return Failed();
        }

        try
        {
            var keys = await GetSigningKeysAsync(cancellationToken);
            var parameters = new TokenValidationParameters
            {
                ValidIssuers = ValidIssuers,
                ValidAudience = _clientId,
                IssuerSigningKeys = keys,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2),
            };

            var principal = new JwtSecurityTokenHandler { MapInboundClaims = false }
                .ValidateToken(idToken, parameters, out _);

            var subject = principal.FindFirst("sub")?.Value;
            var email = principal.FindFirst("email")?.Value;
            if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(email))
            {
                return Failed();
            }

            return Result<GoogleIdTokenPayload>.Success(new GoogleIdTokenPayload(
                Subject: subject,
                Email: email,
                Name: principal.FindFirst("name")?.Value,
                Picture: principal.FindFirst("picture")?.Value,
                EmailVerified: string.Equals(principal.FindFirst("email_verified")?.Value, "true", StringComparison.OrdinalIgnoreCase)));
        }
        catch (SecurityTokenException)
        {
            return Failed();
        }
        catch (ArgumentException)
        {
            // Malformed token string.
            return Failed();
        }
        catch (HttpRequestException)
        {
            return Result<GoogleIdTokenPayload>.Failure(new Error(
                "EXTERNAL_PROVIDER_UNAVAILABLE", "Google sign-in is temporarily unavailable.", ErrorKind.Unauthenticated));
        }
    }

    private static Result<GoogleIdTokenPayload> Failed() =>
        Result<GoogleIdTokenPayload>.Failure(new Error(
            "EXTERNAL_LOGIN_FAILED", "Invalid Google ID token.", ErrorKind.Unauthenticated));

    private static async Task<IList<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken)
    {
        if (_cachedKeys is not null && DateTime.UtcNow - _keysFetchedAtUtc < KeyCacheDuration)
        {
            return _cachedKeys;
        }

        await KeyLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedKeys is null || DateTime.UtcNow - _keysFetchedAtUtc >= KeyCacheDuration)
            {
                var json = await Http.GetStringAsync(JwksUrl, cancellationToken);
                _cachedKeys = new JsonWebKeySet(json).GetSigningKeys();
                _keysFetchedAtUtc = DateTime.UtcNow;
            }

            return _cachedKeys;
        }
        finally
        {
            KeyLock.Release();
        }
    }
}

/// <summary>
/// Outside Development until the real ID Austria (eIDAS) integration and its
/// contract exist: every attempt is refused instead of trusting a stub.
/// </summary>
public sealed class DisabledIdAustriaClient : IIdAustriaClient
{
    private static readonly Error NotConfigured = new(
        "EXTERNAL_PROVIDER_NOT_CONFIGURED", "ID Austria sign-in is not available yet.", ErrorKind.Unauthenticated);

    public Task<Result<IdAustriaTokenResponse>> ExchangeCodeForTokensAsync(string code, string? state, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<IdAustriaTokenResponse>.Failure(NotConfigured));

    public Task<Result<IdAustriaUserInfo>> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<IdAustriaUserInfo>.Failure(NotConfigured));
}
