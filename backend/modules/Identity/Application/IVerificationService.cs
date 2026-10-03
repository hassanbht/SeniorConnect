using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Identity.Application;

/// <summary>
/// Phase 4 (docs/security/verification-and-discovery.md): a person's own
/// verification facts, and the staff workflow that records or revokes the
/// facts nobody can record about themselves (address, organization).
/// </summary>
public interface IVerificationService
{
    Task<Result<MyVerificationDto>> GetMineAsync(Guid userId, CancellationToken ct = default);

    Task<Result<StaffVerificationDto>> RecordAsync(
        Guid actorUserId, Guid organizationId, Guid volunteerUserId,
        RecordVerificationRequest request, CancellationToken ct = default);

    Task<Result<StaffVerificationDto>> RevokeAsync(
        Guid actorUserId, Guid organizationId, Guid verificationId,
        RevokeVerificationRequest request, CancellationToken ct = default);
}

/// <summary>
/// Level is one of NONE, EMAIL, EMAIL_PHONE, EMAIL_PHONE_ADDRESS. Each badge
/// names one fact and who/when (BR-TRUST-04); none of them says "safe".
/// </summary>
public sealed record MyVerificationDto(
    string Level,
    IReadOnlyList<VerificationBadgeDto> Badges,
    string? NextStep);

/// <summary>Fact is one of EMAIL_VERIFIED, PHONE_VERIFIED, ADDRESS_VERIFIED, ORGANIZATION_VERIFIED.</summary>
public sealed record VerificationBadgeDto(
    string Fact,
    DateTimeOffset? VerifiedAtUtc,
    DateTimeOffset? ValidUntilUtc,
    Guid? VerifiedByOrganizationId,
    string? OrganizationName,
    string? Method);

/// <summary>Type: ADDRESS | ORGANIZATION. Method: DOCUMENT_SIGHTED | ORGANIZATION_RECORD | ORGANIZATION_MEMBERSHIP.</summary>
public sealed record RecordVerificationRequest(string Type, string Method, DateTimeOffset? ValidUntilUtc = null);

public sealed record RevokeVerificationRequest(string Reason);

public sealed record StaffVerificationDto(
    Guid Id,
    Guid UserId,
    string Type,
    string Status,
    string? Method,
    Guid? VerifiedByOrganizationId,
    DateTimeOffset? VerifiedAtUtc,
    DateTimeOffset? ValidUntilUtc,
    DateTimeOffset? RevokedAtUtc);

public static class VerificationCodes
{
    public const string TypeInvalid = "VERIFICATION_TYPE_INVALID";
    public const string MethodInvalid = "VERIFICATION_METHOD_INVALID";
    public const string NotAMember = "VERIFICATION_TARGET_NOT_A_MEMBER";
}
