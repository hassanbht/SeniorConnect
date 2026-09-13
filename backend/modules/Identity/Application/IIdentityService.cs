using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Identity.Application;

public interface IIdentityService
{
    Task<Result<string>> RequestPhoneOtpAsync(
        RequestPhoneOtpRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<Result<AuthResponse>> VerifyPhoneOtpAsync(
        VerifyPhoneOtpRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<string>> RequestEmailMagicLinkAsync(
        RequestEmailMagicLinkRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<Result<AuthResponse>> VerifyEmailMagicLinkAsync(
        VerifyEmailMagicLinkRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AuthResponse>> StaffLoginAsync(
        StaffLoginRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AuthResponse>> RefreshTokenAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> LogoutAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken = default);

    Task<Result<UserSummaryDto>> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result<UserSummaryDto>> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<TrustLevelDto>> GetTrustLevelAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<string>>> GetCapabilitiesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<DeviceSessionDto>>> GetActiveDevicesAsync(
        Guid userId,
        string? currentRawRefreshToken,
        CancellationToken cancellationToken = default);

    Task<Result> RevokeDeviceSessionAsync(
        Guid userId,
        Guid refreshTokenId,
        CancellationToken cancellationToken = default);

    Task<Result<string>> InitiatePhoneChangeAsync(
        Guid userId,
        InitiatePhoneChangeRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<Result> VerifyPhoneChangeAsync(
        Guid userId,
        VerifyPhoneChangeRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> RecordConsentAsync(
        Guid userId,
        RecordConsentRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    // ADR-021 additions
    Task<Result<string>> RegisterEmailPasswordAsync(
        RegisterEmailPasswordRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<Result> VerifyEmailRegistrationAsync(
        VerifyEmailRegistrationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AuthResponse>> EmailPasswordLoginAsync(
        EmailPasswordLoginRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AuthResponse>> LoginWithGoogleAsync(
        GoogleLoginRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AuthResponse>> LoginWithIdAustriaAsync(
        IdAustriaLoginRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<string>> RequestProfilePhoneVerificationAsync(
        Guid userId,
        RequestProfilePhoneVerificationRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<Result> VerifyProfilePhoneAsync(
        Guid userId,
        VerifyProfilePhoneRequest request,
        CancellationToken cancellationToken = default);
}
