using SeniorConnect.Domain;
using SeniorConnect.Modules.Identity.Domain;

namespace SeniorConnect.Modules.Identity.Application;

public sealed record VersionedConsentDto(
    Guid Id,
    Guid UserId,
    ConsentType ConsentType,
    string DocumentVersion,
    bool Granted,
    DateTimeOffset GrantedAtUtc,
    DateTimeOffset? WithdrawnAtUtc);

public sealed record UserDataExportDto(
    Guid UserId,
    DateTimeOffset ExportedAtUtc,
    string ExportFormat,
    object UserProfile,
    IReadOnlyList<object> Consents,
    IReadOnlyList<object> AuditHistory);

public sealed record DeletionRequestDto(
    Guid Id,
    Guid UserId,
    DeletionTierStatus Status,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset ScheduledTier2PurgeUtc,
    DateTimeOffset? Tier1ExecutedAtUtc);

public sealed record RequestDeletionRequest(
    string? Reason = null);

public sealed record ConfirmDeletionRequest(
    string ConfirmationToken);

public interface IPrivacyService
{
    Task<Result<IReadOnlyList<VersionedConsentDto>>> GetUserConsentsAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<Result<VersionedConsentDto>> RecordConsentAsync(
        Guid userId,
        RecordConsentRequest request,
        string? ipHash = null,
        CancellationToken ct = default);

    Task<Result<VersionedConsentDto>> WithdrawConsentAsync(
        Guid userId,
        ConsentType consentType,
        CancellationToken ct = default);

    Task<Result<UserDataExportDto>> ExportUserDataAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<Result<DeletionRequestDto>> RequestAccountDeletionAsync(
        Guid userId,
        RequestDeletionRequest request,
        CancellationToken ct = default);

    Task<Result<DeletionRequestDto>> ConfirmAccountDeletionAsync(
        Guid userId,
        ConfirmDeletionRequest request,
        CancellationToken ct = default);
}
