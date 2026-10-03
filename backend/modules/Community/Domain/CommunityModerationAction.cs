using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Community.Domain;

public enum CommunityModerationActionType
{
    HidePost,
    RemovePost,
    HideComment,
    RemoveComment,
    DisableComments,
    MuteMember,
    UnmuteMember,
    RemoveMember,
    BanMember,
    UnbanMember,
    AppointModerator,
    RemoveModerator,
    PlatformHide,
    PlatformRemove,
    PlatformBlockGroup,
    ApproveHeldContent,
    RejectHeldContent,
    DismissReport,
    UpholdReport
}

public enum CommunityModerationTargetType
{
    Post,
    Comment,
    Member,
    Group,
    Event,
    Message,
    Report
}

public sealed class CommunityModerationAction : Entity, IAuditable
{
    private CommunityModerationAction() { }

    [DataClass(DataClass.Operational)]
    public Guid ActorUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public CommunityModerationActionType Action { get; private set; }

    [DataClass(DataClass.Operational)]
    public CommunityModerationTargetType TargetType { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid TargetId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? GroupId { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string Reason { get; private set; } = string.Empty;

    [DataClass(DataClass.Operational)]
    public string? MetadataJson { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsPlatformAction { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static CommunityModerationAction Create(
        Guid actorUserId,
        CommunityModerationActionType action,
        CommunityModerationTargetType targetType,
        Guid targetId,
        string reason,
        Guid? groupId = null,
        string? metadataJson = null,
        bool isPlatformAction = false)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason is required.", nameof(reason));
        }

        var now = DateTimeOffset.UtcNow;
        return new CommunityModerationAction
        {
            Id = Guid.CreateVersion7(),
            ActorUserId = actorUserId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            GroupId = groupId,
            Reason = reason.Trim(),
            MetadataJson = metadataJson,
            IsPlatformAction = isPlatformAction,
            CreatedAtUtc = now,
            CreatedBy = actorUserId,
            UpdatedAtUtc = now,
            UpdatedBy = actorUserId
        };
    }
}
