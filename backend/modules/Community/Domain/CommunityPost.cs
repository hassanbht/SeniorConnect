using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Community.Domain;

public enum CommunityPostType
{
    Post,
    Announcement
}

public enum CommunityPostStatus
{
    Visible,
    PendingReview,
    Hidden,
    Removed,
    Archived
}

public sealed class CommunityPost : Entity, IAuditable, ISoftDeletable
{
    private CommunityPost() { }

    [DataClass(DataClass.Operational)]
    public Guid AuthorUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid GroupId { get; private set; }

    [DataClass(DataClass.Operational)]
    public CommunityPostType PostType { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string Title { get; private set; } = string.Empty;

    [DataClass(DataClass.PersonalData)]
    public string Content { get; private set; } = string.Empty;

    [DataClass(DataClass.Operational)]
    public bool IsPinned { get; private set; }

    [DataClass(DataClass.Operational)]
    public CommunityPostStatus Status { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? ModerationReason { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsDeleted { get; private set; }

    public static Result<CommunityPost> Create(
        Guid authorUserId,
        Guid groupId,
        CommunityPostType postType,
        string title,
        string content,
        bool isPinned = false)
    {
        if (string.IsNullOrWhiteSpace(title) && postType == CommunityPostType.Announcement)
        {
            return Error.Validation("Announcements require a title.");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return Error.Validation("Content is required.");
        }

        var now = DateTimeOffset.UtcNow;
        return Result<CommunityPost>.Success(new CommunityPost
        {
            Id = Guid.CreateVersion7(),
            AuthorUserId = authorUserId,
            GroupId = groupId,
            PostType = postType,
            Title = title?.Trim() ?? string.Empty,
            Content = content.Trim(),
            IsPinned = isPinned,
            Status = CommunityPostStatus.Visible,
            CreatedAtUtc = now,
            CreatedBy = authorUserId,
            UpdatedAtUtc = now,
            UpdatedBy = authorUserId,
            IsDeleted = false
        });
    }

    public Result Update(string title, string content, Guid updatedByUserId)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Error.Validation("Content is required.");
        }

        Title = title?.Trim() ?? string.Empty;
        Content = content.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedByUserId;
        return Result.Success();
    }

    public void Pin(Guid moderatorUserId)
    {
        IsPinned = true;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = moderatorUserId;
    }

    public void Unpin(Guid moderatorUserId)
    {
        IsPinned = false;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = moderatorUserId;
    }

    public void Hide(Guid moderatorUserId, string? reason = null)
    {
        Status = CommunityPostStatus.Hidden;
        ModerationReason = reason;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = moderatorUserId;
    }

    public void Remove(Guid moderatorUserId, string? reason = null)
    {
        Status = CommunityPostStatus.Removed;
        ModerationReason = reason;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = moderatorUserId;
    }

    public void Restore(Guid moderatorUserId)
    {
        Status = CommunityPostStatus.Visible;
        ModerationReason = null;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = moderatorUserId;
    }

    public void SetPendingReview(string? reason = null)
    {
        Status = CommunityPostStatus.PendingReview;
        ModerationReason = reason;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
