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
    int RowVersion);

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
    InsuranceContext InsuranceContext = InsuranceContext.Unknown);

public sealed record AcceptHelpRequestRequest(
    int ExpectedRowVersion);

public sealed record CompleteHelpRequestRequest(
    int? ActualDurationMinutes = null);

public sealed record CancelHelpRequestRequest(
    string Reason);

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
