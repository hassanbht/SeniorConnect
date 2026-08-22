using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Community.Domain;

public enum ThreadContextType
{
    Group,
    Event
}

public sealed class MessageThread : Entity, IAuditable
{
    private MessageThread() { }

    [DataClass(DataClass.Operational)]
    public ThreadContextType ContextType { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid ContextId { get; private set; }

    [DataClass(DataClass.Operational)]
    public string Title { get; private set; } = string.Empty;

    [DataClass(DataClass.Operational)]
    public bool IsClosed { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static MessageThread Create(ThreadContextType contextType, Guid contextId, string title, Guid creatorUserId)
    {
        var now = DateTimeOffset.UtcNow;
        return new MessageThread
        {
            Id = Guid.CreateVersion7(),
            ContextType = contextType,
            ContextId = contextId,
            Title = string.IsNullOrWhiteSpace(title) ? "General Discussion" : title.Trim(),
            IsClosed = false,
            CreatedAtUtc = now,
            CreatedBy = creatorUserId,
            UpdatedAtUtc = now,
            UpdatedBy = creatorUserId
        };
    }

    public void Close(Guid adminUserId)
    {
        IsClosed = true;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = adminUserId;
    }
}

public sealed class ThreadMessage : Entity, IAuditable, ISoftDeletable
{
    private ThreadMessage() { }

    [DataClass(DataClass.Operational)]
    public Guid ThreadId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid SenderUserId { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string Content { get; private set; } = string.Empty;

    [DataClass(DataClass.Operational)]
    public bool IsFlaggedForModeration { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? ModerationReason { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsDeleted { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static Result<ThreadMessage> Create(Guid threadId, Guid senderUserId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Error.Validation("Message content cannot be empty.");
        }

        var now = DateTimeOffset.UtcNow;
        return Result<ThreadMessage>.Success(new ThreadMessage
        {
            Id = Guid.CreateVersion7(),
            ThreadId = threadId,
            SenderUserId = senderUserId,
            Content = content.Trim(),
            IsFlaggedForModeration = false,
            IsDeleted = false,
            CreatedAtUtc = now,
            CreatedBy = senderUserId,
            UpdatedAtUtc = now,
            UpdatedBy = senderUserId
        });
    }

    public void FlagForModeration(string reason)
    {
        IsFlaggedForModeration = true;
        ModerationReason = reason;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
