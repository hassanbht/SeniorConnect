using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Identity.Application;

public interface IGoogleIdTokenValidator
{
    Task<Result<GoogleIdTokenPayload>> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}

public sealed record GoogleIdTokenPayload(
    string Subject,
    string Email,
    string? Name,
    string? Picture,
    bool EmailVerified);

public interface IIdAustriaClient
{
    Task<Result<IdAustriaTokenResponse>> ExchangeCodeForTokensAsync(string code, string? state, CancellationToken cancellationToken = default);
    Task<Result<IdAustriaUserInfo>> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default);
}

public sealed record IdAustriaTokenResponse(
    string AccessToken,
    string? IdToken,
    string TokenType,
    int ExpiresIn,
    string? RefreshToken);

public sealed record IdAustriaUserInfo(
    string Subject,
    string Email,
    string? GivenName,
    string? FamilyName,
    bool EmailVerified);