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
