using SeniorConnect.Modules.Community.Domain;

namespace SeniorConnect.Modules.Community.Application;

// --- Groups ---

public sealed record CommunityGroupDto(
    Guid Id,
    Guid CreatorUserId,
    CreatorType CreatorType,
    Guid? OrganizationId,
    string Name,
    string Description,
    GroupType Type,
    string Category,
    GroupVisibility Visibility,
    GroupStatus Status,
    string? City,
    string? Region,
    string? PostalCode,
    double? Latitude,
    double? Longitude,
    string? PrimaryLanguageCode,
    string? CoverImageUrl,
    string Rules,
    int? MaxMembers,
    int MemberCount,
    GroupVerificationStatus VerificationStatus,
    DateTimeOffset CreatedAtUtc,
    GroupMembershipStatus? MyMembershipStatus = null,
    GroupMemberRole? MyRole = null);

public sealed record CreateCommunityGroupRequest(
    string Name,
    string Description,
    GroupType Type,
    GroupVisibility Visibility = GroupVisibility.Public,
    Guid? OrganizationId = null,
    string? City = null,
    string? Region = null,
    string? PostalCode = null,
    double? Latitude = null,
    double? Longitude = null,
    string? PrimaryLanguageCode = null,
    string? CoverImageUrl = null,
    string? Rules = null,
    int? MaxMembers = null);

public sealed record UpdateCommunityGroupRequest(
    string Name,
    string Description,
    GroupType Type,
    GroupVisibility Visibility,
    string? City = null,
    string? Region = null,
    string? PostalCode = null,
    double? Latitude = null,
    double? Longitude = null,
    string? PrimaryLanguageCode = null,
    string? CoverImageUrl = null,
    string? Rules = null,
    int? MaxMembers = null);

public sealed record GroupMembershipDto(
    Guid Id,
    Guid GroupId,
    Guid UserId,
    GroupMemberRole Role,
    GroupMembershipStatus Status,
    DateTimeOffset? JoinedAtUtc,
    DateTimeOffset? MutedUntilUtc,
    string? BanReason);

public sealed record CommunityInvitationDto(
    Guid Id,
    Guid GroupId,
    Guid InvitedByUserId,
    Guid? InvitedUserId,
    string? Email,
    string? Phone,
    string Token,
    CommunityInvitationStatus Status,
    DateTimeOffset ExpiresAtUtc);

public sealed record InviteMemberRequest(
    Guid? InvitedUserId = null,
    string? Email = null,
    string? Phone = null);

public sealed record ModerationReasonRequest(string? Reason = null);

public sealed record MuteMemberRequest(DateTimeOffset UntilUtc, string? Reason = null);

public sealed record CommunityCategoryDto(
    string Code,
    string NameKey,
    string? DescriptionKey,
    GroupType GroupType,
    int SortOrder);

// --- Events ---

public sealed record CommunityEventDto(
    Guid Id,
    Guid HostUserId,
    Guid? GroupId,
    Guid? OrganizationId,
    string Title,
    string Description,
    string Category,
    string? LocationAddress,
    string? LocationPostalCode,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    EventRecurrenceFrequency RecurrenceFrequency,
    DateTimeOffset? RecurrenceUntilUtc,
    int? Capacity,
    int GoingCount,
    int WaitlistCount,
    bool IsCancelled,
    string? CancellationReason,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<EventOccurrenceDto> Occurrences,
    EventRsvpStatus? MyRegistrationStatus = null,
    bool IsPendingReview = false,
    EventVenueType VenueType = EventVenueType.Private);

public sealed record EventOccurrenceDto(DateTimeOffset StartsAtUtc, bool IsCancelled);

public sealed record CancelEventOccurrenceRequest(DateTimeOffset OccurrenceStartUtc, string Reason);

public sealed record CreateCommunityEventRequest(
    string Title,
    string Description,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string Category = "general",
    Guid? GroupId = null,
    Guid? OrganizationId = null,
    string? LocationAddress = null,
    string? LocationPostalCode = null,
    EventRecurrenceFrequency RecurrenceFrequency = EventRecurrenceFrequency.None,
    DateTimeOffset? RecurrenceUntilUtc = null,
    int? Capacity = null,
    EventVenueType VenueType = EventVenueType.Private);

public sealed record UpdateCommunityEventRequest(
    string Title,
    string Description,
    string Category,
    string? LocationAddress = null,
    string? LocationPostalCode = null,
    int? Capacity = null,
    EventVenueType? VenueType = null);

public sealed record CancelCommunityEventRequest(string Reason);

public sealed record EventRegistrationDto(
    Guid Id,
    Guid EventId,
    Guid UserId,
    EventRsvpStatus Status,
    int? WaitlistPosition,
    string? Note,
    DateTimeOffset RegisteredAtUtc);

public sealed record RegisterEventRequest(
    EventRsvpStatus Status = EventRsvpStatus.Going,
    string? Note = null);

public sealed record MyScheduleItemDto(
    Guid EventId,
    string Title,
    string Category,
    string? LocationAddress,
    string? LocationPostalCode,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    EventRsvpStatus MyStatus,
    int? WaitlistPosition,
    bool IsCancelled);

// --- Posts / Announcements ---

public sealed record CommunityPostDto(
    Guid Id,
    Guid AuthorUserId,
    Guid GroupId,
    CommunityPostType PostType,
    string Title,
    string Content,
    bool IsPinned,
    CommunityPostStatus Status,
    string? ModerationReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record CreateCommunityPostRequest(
    string Title,
    string Content,
    CommunityPostType PostType = CommunityPostType.Post,
    bool IsPinned = false);

public sealed record UpdateCommunityPostRequest(
    string Title,
    string Content);

public sealed record CommunityCommentDto(
    Guid Id,
    Guid AuthorUserId,
    Guid PostId,
    string Content,
    CommunityPostStatus Status,
    string? ModerationReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record CreateCommunityCommentRequest(string Content);

// --- Reports ---

public sealed record CommunityReportDto(
    Guid Id,
    Guid ReporterUserId,
    CommunityReportTargetType TargetType,
    Guid TargetId,
    Guid? GroupId,
    CommunityReportCategory Category,
    string Description,
    CommunityReportStatus Status,
    DateTimeOffset CreatedAtUtc);

public sealed record CreateCommunityReportRequest(
    CommunityReportTargetType TargetType,
    Guid TargetId,
    CommunityReportCategory Category,
    string Description,
    Guid? GroupId = null);

// --- Moderation ---

public sealed record CommunityModerationActionDto(
    Guid Id,
    Guid ActorUserId,
    CommunityModerationActionType Action,
    CommunityModerationTargetType TargetType,
    Guid TargetId,
    Guid? GroupId,
    string Reason,
    DateTimeOffset CreatedAtUtc);

public sealed record ModerationActionRequest(
    CommunityModerationActionType Action,
    string Reason);

// BR-COMM-05 human review queue: held posts, held messages, held groups/events
// and open reports, scoped to what the caller may moderate.
public enum ModerationQueueItemType
{
    Post,
    Message,
    Group,
    Event,
    Report
}

/// <summary>Decision about the content: Approve = publish / keep it, Reject = take it down.</summary>
public enum ModerationResolution
{
    Approve,
    Reject
}

public sealed record ModerationQueueItemDto(
    ModerationQueueItemType ItemType,
    Guid ItemId,
    Guid? GroupId,
    Guid? AuthorUserId,
    string Content,
    string? Reason,
    DateTimeOffset CreatedAtUtc,
    CommunityReportTargetType? ReportTargetType = null,
    Guid? ReportTargetId = null);

public sealed record ResolveModerationItemRequest(
    ModerationResolution Resolution,
    string? Note = null);

// --- Contextual Threads (legacy) ---

public sealed record MessageThreadDto(
    Guid Id,
    ThreadContextType ContextType,
    Guid ContextId,
    string Title,
    bool IsClosed,
    DateTimeOffset CreatedAtUtc);

public sealed record ThreadMessageDto(
    Guid Id,
    Guid ThreadId,
    Guid SenderUserId,
    string Content,
    bool IsFlaggedForModeration,
    DateTimeOffset CreatedAtUtc);

public sealed record PostMessageRequest(
    string Content);

public sealed record ReportMessageRequest(
    string Reason);


/// <summary>
/// B-2 (Option A, 2026-09-30): payload for the officer's manual conversion of an
/// escalated unsafe-behavior report into a safeguarding case. The officer sets
/// severity and writes the case summary; nothing is automatic (BR-SG-01).
/// </summary>
/// <summary>
/// B-2 (Option A): officer-provided payload for the manual conversion of an
/// escalated unsafe-behavior report. Severity crosses as a neutral string
/// ("Low"/"Medium"/"High"/"Critical") so no TrustSafety Domain type leaks
/// into the Community contract (ModuleBoundaryTests).
/// </summary>
public sealed record ConvertReportToCaseRequest(
    string Summary,
    string Severity,
    Guid? OrganizationId = null);
