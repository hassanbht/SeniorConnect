using SeniorConnect.Domain;
using SeniorConnect.Modules.Community.Domain;
using SeniorConnect.Modules.TrustSafety.Application;

namespace SeniorConnect.Modules.Community.Application;

public interface ICommunityService
{
    // --- Groups ---
    Task<Result<CommunityGroupDto>> CreateGroupAsync(
        Guid userId,
        CreatorType creatorType,
        CreateCommunityGroupRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CommunityGroupDto>> GetGroupByIdAsync(
        Guid groupId,
        Guid? requestingUserId = null,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<CommunityGroupDto>>> GetGroupsAsync(
        string? category = null,
        string? postalCode = null,
        string? city = null,
        string? region = null,
        string? language = null,
        GroupType? type = null,
        Guid? organizationId = null,
        string? query = null,
        CancellationToken cancellationToken = default);

    Task<Result<CommunityGroupDto>> UpdateGroupAsync(
        Guid groupId,
        Guid userId,
        UpdateCommunityGroupRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> ArchiveGroupAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default);

    // --- Membership ---
    Task<Result<GroupMembershipDto>> JoinGroupAsync(
        Guid groupId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result<GroupMembershipDto>> AcceptInvitationAsync(
        Guid groupId,
        string token,
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

    Task<Result> RemoveMemberAsync(
        Guid groupId,
        Guid targetUserId,
        Guid adminUserId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<Result> MuteMemberAsync(
        Guid groupId,
        Guid targetUserId,
        Guid moderatorUserId,
        DateTimeOffset untilUtc,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<Result> UnmuteMemberAsync(
        Guid groupId,
        Guid targetUserId,
        Guid moderatorUserId,
        CancellationToken cancellationToken = default);

    Task<Result> BanMemberAsync(
        Guid groupId,
        Guid targetUserId,
        Guid moderatorUserId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<Result> UnbanMemberAsync(
        Guid groupId,
        Guid targetUserId,
        Guid moderatorUserId,
        CancellationToken cancellationToken = default);

    Task<Result<CommunityInvitationDto>> InviteMemberAsync(
        Guid groupId,
        Guid invitedByUserId,
        Guid? invitedUserId,
        string? email,
        string? phone,
        CancellationToken cancellationToken = default);

    Task<Result> AppointModeratorAsync(
        Guid groupId,
        Guid targetUserId,
        Guid ownerUserId,
        CancellationToken cancellationToken = default);

    Task<Result> RemoveModeratorAsync(
        Guid groupId,
        Guid targetUserId,
        Guid ownerUserId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<GroupMembershipDto>>> GetGroupMembersAsync(
        Guid groupId,
        Guid requestingUserId,
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
        Guid? requestingUserId = null,
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

    // --- Posts / Announcements ---
    Task<Result<CommunityPostDto>> CreatePostAsync(
        Guid groupId,
        Guid authorUserId,
        CreateCommunityPostRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<CommunityPostDto>>> GetGroupPostsAsync(
        Guid groupId,
        Guid? requestingUserId = null,
        CancellationToken cancellationToken = default);

    Task<Result<CommunityPostDto>> HidePostAsync(
        Guid groupId,
        Guid postId,
        Guid moderatorUserId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<Result<CommunityPostDto>> RemovePostAsync(
        Guid groupId,
        Guid postId,
        Guid moderatorUserId,
        string? reason,
        CancellationToken cancellationToken = default);

    // --- Reports ---
    Task<Result<CommunityReportDto>> CreateReportAsync(
        Guid reporterUserId,
        CreateCommunityReportRequest request,
        CancellationToken cancellationToken = default);

    // --- Contextual Threads (legacy) ---
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

    // --- Moderation review queue (BR-COMM-05) ---
    // isPlatformModerator is derived by the endpoint from server-side JWT claims.
    Task<Result<IReadOnlyList<ModerationQueueItemDto>>> GetModerationQueueAsync(
        Guid callerUserId,
        bool isPlatformModerator,
        CancellationToken cancellationToken = default);

    Task<Result> ResolveModerationItemAsync(
        Guid callerUserId,
        bool isPlatformModerator,
        ModerationQueueItemType itemType,
        Guid itemId,
        ResolveModerationItemRequest request,
        CancellationToken cancellationToken = default);

    // --- B-2 (Option A, 2026-09-30): manual officer conversion ---
    /// <summary>B-2: returns the id of the newly raised safeguarding case.</summary>
    Task<Result<Guid>> ConvertReportToCaseAsync(
        Guid reportId,
        Guid officerUserId,
        bool callerIsSafeguardingOfficer,
        ConvertReportToCaseRequest request,
        CancellationToken cancellationToken = default);
}
