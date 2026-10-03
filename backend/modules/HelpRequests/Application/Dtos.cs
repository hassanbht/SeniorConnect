using SeniorConnect.Modules.HelpRequests.Domain;

namespace SeniorConnect.Modules.HelpRequests.Application;

public sealed record ActivityDto(
    Guid Id,
    Guid? OrganizationId,
    Guid? BranchId,
    Guid VolunteerUserId,
    Guid? SubjectUserId,
    Guid CategoryId,
    DateOnly OccurredOn,
    int DurationMinutes,
    LocationType LocationType,
    string? Notes,
    InsuranceContext InsuranceContext,
    TransportMode TransportMode,
    ActivitySource Source,
    Guid LoggedByUserId,
    DateTimeOffset LoggedAtUtc,
    Guid? ConfirmedByUserId,
    DateTimeOffset? ConfirmedAtUtc,
    ActivityStatus Status);

public sealed record LogActivityRequest(
    Guid? OrganizationId,
    Guid? SubjectUserId,
    Guid CategoryId,
    DateOnly OccurredOn,
    int DurationMinutes,
    LocationType LocationType,
    string? Notes,
    InsuranceContext InsuranceContext,
    TransportMode TransportMode = TransportMode.None);

public sealed record ConfirmActivityRequest(
    Guid ActivityId);

public sealed record DisputeActivityRequest(
    string Reason);

public sealed record ActivityCategoryDto(
    Guid Id,
    string Code,
    string NameKey,
    int DefaultSafetyLevel,
    bool IsBlocked,
    string? ReferralGroup,
    bool IsActive);

public sealed record ReferralProviderDto(
    Guid Id,
    string ReferralGroup,
    string RegionCode,
    string Name,
    string? Phone,
    string? Website,
    string? Address,
    string? NoteKey);

public sealed record HelpRequestDto(
    Guid Id,
    Guid? OrganizationId,
    Guid? BranchId,
    Guid SeniorUserId,
    Guid CreatedByUserId,
    Guid CategoryId,
    int RequiredSafetyLevel,
    int RequiredTrustLevel,
    DateTimeOffset ScheduledStartUtc,
    DateTimeOffset ScheduledEndUtc,
    int DurationMinutes,
    LocationType LocationType,
    string? LocationAddress,
    string? LocationPostalCode,
    string? LocationCity,
    double? Latitude,
    double? Longitude,
    string? Notes,
    TransportMode TransportMode,
    InsuranceContext InsuranceContext,
    HelpRequestStatus Status,
    Guid? AssignedVolunteerUserId,
    DateTimeOffset? AssignedAtUtc,
    DateTimeOffset? CheckedInAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? CancellationReason,
    int RowVersion,
    string? SeniorDisplayName = null,
    string? SeniorPhone = null,
    string? VolunteerDisplayName = null,
    string? VolunteerPhone = null,
    /// <summary>ADR-026 (legal validation pending): whether a live visit-address grant exists for the request's current assignment. Populated for parties of a live assignment only.</summary>
    bool VisitAddressGrantActive = false);

/// <summary>
/// A request's location as the central policy lets this caller see it
/// (docs/security/location-consent-and-visibility.md). <see cref="Disclosure"/>:
/// PRECISE | APPROXIMATE | DISTANCE_ONLY | NONE.
/// </summary>
public sealed record HelpRequestLocationDto(
    Guid HelpRequestId,
    string Disclosure,
    double? Latitude,
    double? Longitude,
    string? PostalCode,
    string? City);

public sealed record CreateHelpRequestRequest(
    Guid? OrganizationId,
    Guid CategoryId,
    DateTimeOffset ScheduledStartUtc,
    DateTimeOffset ScheduledEndUtc,
    int DurationMinutes,
    LocationType LocationType,
    string? Notes = null,
    string? Address = null,
    string? PostalCode = null,
    string? City = null,
    double? Latitude = null,
    double? Longitude = null,
    TransportMode TransportMode = TransportMode.None,
    InsuranceContext InsuranceContext = InsuranceContext.Unknown,
    Guid? SeniorUserId = null);

public sealed record AcceptHelpRequestRequest(
    int ExpectedRowVersion);

public sealed record CompleteHelpRequestRequest(
    int? ActualDurationMinutes = null);

public sealed record CancelHelpRequestRequest(
    string Reason,
    CancellationReasonCode ReasonCode = CancellationReasonCode.Other);

public sealed record NoShowHelpRequestRequest(
    string? Notes = null);

public sealed record DisputeNoShowRequest(
    string Reason);

public sealed record ResolveNoShowDisputeRequest(
    bool Upheld,
    string? Note = null);

public sealed record HelpRequestStatusHistoryDto(
    Guid Id,
    Guid HelpRequestId,
    HelpRequestStatus FromStatus,
    HelpRequestStatus ToStatus,
    Guid ChangedByUserId,
    DateTimeOffset ChangedAtUtc,
    string? Reason);

public sealed record EmergencyAlertDto(
    bool IsEmergency,
    string Message,
    IReadOnlyList<string> EmergencyNumbers);


/// <summary>
/// ADR-026 Option C (approved 2026-09-30; legal validation pending before
/// production activation). Optional: pass a coordinator id + reason to record
/// the audited "coordinator on behalf" path for app-less seniors.
/// </summary>
public sealed record GrantVisitAddressRequest(
    Guid? OnBehalfOfSeniorByCoordinatorId = null,
    string? CoordinatorReason = null);

public sealed record RevokeVisitAddressRequest(string? Reason = null);
