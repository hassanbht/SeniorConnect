using SeniorConnect.Domain;
using SeniorConnect.Modules.Community.Domain;

namespace SeniorConnect.Modules.Community.Application;

public interface ICommunityService
{
    // --- Groups ---
    Task<Result<CommunityGroupDto>> CreateGroupAsync(
        Guid userId,
        CreateCommunityGroupRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CommunityGroupDto>> GetGroupByIdAsync(
        Guid groupId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<CommunityGroupDto>>> GetGroupsAsync(
        string? category = null,
        string? postalCode = null,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default);

    Task<Result<CommunityGroupDto>> UpdateGroupAsync(
        Guid groupId,
        Guid userId,
        UpdateCommunityGroupRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<GroupMembershipDto>> JoinGroupAsync(
        Guid groupId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result> ApproveMemberAsync(
        Guid groupId,
        Guid targetUserId,
        Guid adminUserId,
        CancellationToken cancellationToken = default);

    Task<Result> RejectMemberAsync(
        Guid groupId,
        Guid targetUserId,
        Guid adminUserId,
        CancellationToken cancellationToken = default);

    Task<Result> LeaveGroupAsync(
        Guid groupId,
        Guid userId,
        CancellationToken cancellationToken = default);

    // --- Events ---
    Task<Result<CommunityEventDto>> CreateEventAsync(
        Guid userId,
        CreateCommunityEventRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CommunityEventDto>> GetEventByIdAsync(
        Guid eventId,
        Guid? requestingUserId = null,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<CommunityEventDto>>> GetEventsAsync(
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        string? category = null,
        string? postalCode = null,
        Guid? groupId = null,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default);

    Task<Result<CommunityEventDto>> UpdateEventAsync(
        Guid eventId,
        Guid userId,
        UpdateCommunityEventRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> CancelEventAsync(
        Guid eventId,
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<Result<CommunityEventDto>> CancelEventOccurrenceAsync(
        Guid eventId,
        Guid userId,
        CancelEventOccurrenceRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<EventRegistrationDto>> RegisterForEventAsync(
        Guid eventId,
        Guid userId,
        RegisterEventRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> CancelRegistrationAsync(
        Guid eventId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<MyScheduleItemDto>>> GetMyScheduleAsync(
        Guid userId,
        DateTimeOffset? fromUtc = null,
        CancellationToken cancellationToken = default);

    // --- Contextual Threads ---
    Task<Result<MessageThreadDto>> GetOrCreateThreadAsync(
        ThreadContextType contextType,
        Guid contextId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ThreadMessageDto>>> GetMessagesAsync(
        Guid threadId,
        Guid requestingUserId,
        CancellationToken cancellationToken = default);

    Task<Result<ThreadMessageDto>> PostMessageAsync(
        Guid threadId,
        Guid senderUserId,
        PostMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteMessageAsync(
        Guid threadId,
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result<ThreadMessageDto>> ReportMessageAsync(
        Guid threadId,
        Guid messageId,
        Guid reporterUserId,
        ReportMessageRequest request,
        CancellationToken cancellationToken = default);
}
