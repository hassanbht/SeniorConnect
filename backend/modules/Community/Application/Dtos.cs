using SeniorConnect.Modules.Community.Domain;

namespace SeniorConnect.Modules.Community.Application;

public sealed record CommunityGroupDto(
    Guid Id,
    Guid CreatorUserId,
    Guid? OrganizationId,
    string Title,
    string Description,
    string Category,
    GroupVisibilityScope Scope,
    GroupJoinPolicy JoinPolicy,
    string? LocationPostalCode,
    int? MaxMembers,
    int MemberCount,
    bool IsArchived,
    DateTimeOffset CreatedAtUtc);

public sealed record CreateCommunityGroupRequest(
    string Title,
    string Description,
    string Category = "general",
    GroupVisibilityScope Scope = GroupVisibilityScope.Public,
    GroupJoinPolicy JoinPolicy = GroupJoinPolicy.Open,
    Guid? OrganizationId = null,
    string? LocationPostalCode = null,
    int? MaxMembers = null);

public sealed record UpdateCommunityGroupRequest(
    string Title,
    string Description,
    string Category,
    GroupVisibilityScope Scope,
    GroupJoinPolicy JoinPolicy,
    string? LocationPostalCode = null,
    int? MaxMembers = null);

public sealed record GroupMembershipDto(
    Guid Id,
    Guid GroupId,
    Guid UserId,
    GroupMemberRole Role,
    GroupMembershipStatus Status,
    DateTimeOffset JoinedAtUtc);

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
    EventRsvpStatus? MyRegistrationStatus = null);

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
    int? Capacity = null);

public sealed record UpdateCommunityEventRequest(
    string Title,
    string Description,
    string Category,
    string? LocationAddress = null,
    string? LocationPostalCode = null,
    int? Capacity = null);

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
