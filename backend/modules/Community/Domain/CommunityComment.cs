using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Community.Domain;

public sealed class CommunityComment : Entity, IAuditable, ISoftDeletable
{
    private CommunityComment() { }

    [DataClass(DataClass.Operational)]
    public Guid AuthorUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid PostId { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string Content { get; private set; } = string.Empty;

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

    public static Result<CommunityComment> Create(
        Guid authorUserId,
        Guid postId,
        string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Error.Validation("Content is required.");
        }

        var now = DateTimeOffset.UtcNow;
        return Result<CommunityComment>.Success(new CommunityComment
        {
            Id = Guid.CreateVersion7(),
            AuthorUserId = authorUserId,
            PostId = postId,
            Content = content.Trim(),
            Status = CommunityPostStatus.Visible,
            CreatedAtUtc = now,
            CreatedBy = authorUserId,
            UpdatedAtUtc = now,
            UpdatedBy = authorUserId,
            IsDeleted = false
        });
    }

    public Result Update(string content, Guid updatedByUserId)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Error.Validation("Content is required.");
        }

        Content = content.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedByUserId;
        return Result.Success();
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
