using SeniorConnect.Domain;
using SeniorConnect.Modules.Family.Domain;

namespace SeniorConnect.Modules.Family.Application;

public interface IFamilyService
{
    Task<Result<FamilyRelationshipDto>> InviteCaregiverAsync(
        Guid requesterUserId,
        InviteCaregiverRequest request,
        CancellationToken ct = default);

    Task<Result<FamilyRelationshipDto>> AcceptInvitationAsync(
        Guid caregiverUserId,
        AcceptInvitationRequest request,
        CancellationToken ct = default);

    Task<Result> RevokeRelationshipAsync(
        Guid relationshipId,
        Guid requesterUserId,
        CancellationToken ct = default);

    Task<Result> UpdatePermissionsAsync(
        Guid relationshipId,
        Guid requesterUserId,
        UpdatePermissionsRequest request,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<FamilyRelationshipDto>>> GetCaregiversForSeniorAsync(
        Guid seniorUserId,
        Guid requesterUserId,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<FamilyRelationshipDto>>> GetSeniorsForCaregiverAsync(
        Guid caregiverUserId,
        CancellationToken ct = default);

    Task<Result<ZugangskarteDto>> CreateSeniorWithZugangskarteAsync(
        Guid caregiverUserId,
        CreateSeniorWithZugangskarteRequest request,
        CancellationToken ct = default);

    Task<Result<ZugangskarteDto>> ClaimZugangskarteAsync(
        Guid seniorUserId,
        ClaimZugangskarteRequest request,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<SeniorAccessLogDto>>> GetAccessLogsAsync(
        Guid seniorUserId,
        Guid requesterUserId,
        int days = 30,
        CancellationToken ct = default);

    Task<Result<TrustedContactDto>> CreateTrustedContactAsync(
        Guid requesterUserId,
        CreateTrustedContactRequest request,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<TrustedContactDto>>> GetTrustedContactsAsync(
        Guid seniorUserId,
        Guid requesterUserId,
        CancellationToken ct = default);

    Task<Result<TrustedContactDto>> UpdateTrustedContactAsync(
        Guid id,
        Guid requesterUserId,
        UpdateTrustedContactRequest request,
        CancellationToken ct = default);

    Task<Result> DeleteTrustedContactAsync(
        Guid id,
        Guid requesterUserId,
        CancellationToken ct = default);

    Task<Result<SafetyAlertDto>> TriggerSafetyAlertAsync(
        Guid requesterUserId,
        TriggerSafetyAlertRequest request,
        CancellationToken ct = default);

    Task<Result<SafetyAlertDto>> AcknowledgeSafetyAlertAsync(
        Guid alertId,
        Guid caregiverUserId,
        CancellationToken ct = default);

    Task<Result<SafetyAlertDto>> ResolveSafetyAlertAsync(
        Guid alertId,
        Guid caregiverUserId,
        ResolveSafetyAlertRequest request,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<SafetyAlertDto>>> GetSafetyAlertsForSeniorAsync(
        Guid seniorUserId,
        Guid requesterUserId,
        CancellationToken ct = default);
}
