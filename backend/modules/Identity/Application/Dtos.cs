using SeniorConnect.Modules.Identity.Domain;

namespace SeniorConnect.Modules.Identity.Application;

public sealed record RequestPhoneOtpRequest(
    string Phone,
    string? DisplayName = null,
    string PreferredLocale = "de",
    bool SeniorModeDefault = false,
    OtpPurpose Purpose = OtpPurpose.Login);

public sealed record RegisterEmailPasswordRequest(
    string Email,
    string Password,
    string ConfirmPassword,
    string PreferredLocale = "de");

public sealed record VerifyEmailRegistrationRequest(
    string Token);

public sealed record EmailPasswordLoginRequest(
    string Email,
    string Password,
    string? DeviceLabel = null,
    bool IsPersonalDevice = true);

public sealed record GoogleLoginRequest(
    string IdToken,
    string? DeviceLabel = null,
    bool IsPersonalDevice = true);

public sealed record IdAustriaLoginRequest(
    string Code,
    string? State = null,
    string? DeviceLabel = null,
    bool IsPersonalDevice = true);

public sealed record RequestProfilePhoneVerificationRequest(
    string Phone,
    string PreferredLocale = "de");

public sealed record VerifyProfilePhoneRequest(
    string Phone,
    string Code);

public sealed record VerifyPhoneOtpRequest(
    string Phone,
    string Code,
    string? DeviceLabel = null,
    bool IsPersonalDevice = true,
    OtpPurpose Purpose = OtpPurpose.Login);

public sealed record RequestEmailMagicLinkRequest(
    string Email,
    string? DisplayName = null,
    string PreferredLocale = "de",
    OtpPurpose Purpose = OtpPurpose.Login);

public sealed record VerifyEmailMagicLinkRequest(
    string Email,
    string Code,
    string? DeviceLabel = null,
    bool IsPersonalDevice = true,
    OtpPurpose Purpose = OtpPurpose.Login);

public sealed record StaffLoginRequest(
    string Email,
    string Password,
    string? TotpCode = null,
    string? DeviceLabel = null,
    bool IsPersonalDevice = true);

public sealed record RefreshTokenRequest(
    string RefreshToken,
    string? DeviceLabel = null);

public sealed record RevokeDeviceRequest(
    Guid RefreshTokenId);

public sealed record InitiatePhoneChangeRequest(
    string NewPhone);

public sealed record VerifyPhoneChangeRequest(
    string NewPhone,
    string Code);

public sealed record UpdateProfileRequest(
    string DisplayName,
    DateOnly? DateOfBirth,
    string PreferredLocale,
    bool SeniorModeDefault);

public sealed record RecordConsentRequest(
    ConsentType ConsentType,
    string DocumentVersion,
    bool Granted);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAtUtc,
    UserSummaryDto User,
    IReadOnlyList<string> Capabilities,
    short TrustLevel);

public sealed record UserSummaryDto(
    Guid Id,
    string? Phone,
    string? Email,
    string DisplayName,
    string PreferredLocale,
    bool SeniorModeDefault,
    string PrimaryAuthMethod,
    string Status);

public sealed record DeviceSessionDto(
    Guid Id,
    string? DeviceLabel,
    bool IsPersonalDevice,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    bool IsActive,
    bool IsCurrent);

public sealed record TrustLevelDto(
    short Level,
    IReadOnlyList<VerificationSummaryDto> Verifications,
    IReadOnlyList<string> MissingForNextLevel);

public sealed record VerificationSummaryDto(
    string Type,
    string Status,
    DateTimeOffset? VerifiedAtUtc,
    DateTimeOffset? ValidUntilUtc,
    string? RejectionReason);
