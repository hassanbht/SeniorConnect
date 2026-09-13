using Microsoft.Extensions.Logging;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Identity.Application;

namespace SeniorConnect.Modules.Identity.Infrastructure;

public sealed class GoogleIdTokenValidatorStub : IGoogleIdTokenValidator
{
    private readonly ILogger<GoogleIdTokenValidatorStub> _logger;

    public GoogleIdTokenValidatorStub(ILogger<GoogleIdTokenValidatorStub> logger)
    {
        _logger = logger;
    }

    public Task<Result<GoogleIdTokenPayload>> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("[Google OAuth STUB] Validating ID token: {TokenPrefix}...", idToken[..Math.Min(20, idToken.Length)]);

        // In dev, accept any non-empty token and return a deterministic payload
        // Production would use GoogleJsonWebSignature.ValidateAsync
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return Task.FromResult(Result<GoogleIdTokenPayload>.Failure(
                new Error("EXTERNAL_LOGIN_FAILED", "Invalid Google ID token.", ErrorKind.Unauthenticated)));
        }

        return Task.FromResult(Result<GoogleIdTokenPayload>.Success(new GoogleIdTokenPayload(
            Subject: "google-oauth2|1234567890",
            Email: "test.user@gmail.com",
            Name: "Test User",
            Picture: null,
            EmailVerified: true)));
    }
}

public sealed class IdAustriaClientStub : IIdAustriaClient
{
    private readonly ILogger<IdAustriaClientStub> _logger;

    public IdAustriaClientStub(ILogger<IdAustriaClientStub> logger)
    {
        _logger = logger;
    }

    public Task<Result<IdAustriaTokenResponse>> ExchangeCodeForTokensAsync(string code, string? state, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("[ID Austria STUB] Exchanging code for tokens. Code: {Code}", code);

        if (string.IsNullOrWhiteSpace(code))
        {
            return Task.FromResult(Result<IdAustriaTokenResponse>.Failure(
                new Error("EXTERNAL_LOGIN_FAILED", "Invalid authorization code.", ErrorKind.Unauthenticated)));
        }

        return Task.FromResult(Result<IdAustriaTokenResponse>.Success(new IdAustriaTokenResponse(
            AccessToken: "stub-access-token-" + Guid.NewGuid(),
            IdToken: "stub-id-token",
            TokenType: "Bearer",
            ExpiresIn: 3600,
            RefreshToken: "stub-refresh-token")));
    }

    public Task<Result<IdAustriaUserInfo>> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("[ID Austria STUB] Fetching user info with access token");

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Task.FromResult(Result<IdAustriaUserInfo>.Failure(
                new Error("EXTERNAL_LOGIN_FAILED", "Invalid access token.", ErrorKind.Unauthenticated)));
        }

        return Task.FromResult(Result<IdAustriaUserInfo>.Success(new IdAustriaUserInfo(
            Subject: "id-austria|9876543210",
            Email: "max.mustermann@buergerkarte.at",
            GivenName: "Max",
            FamilyName: "Mustermann",
            EmailVerified: true)));
    }
}