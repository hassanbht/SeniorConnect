using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Notifications.Domain;

public sealed class NotificationMessage
{
    public Guid Id { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public NotificationCategory Category { get; private set; }
    public NotificationPriority Priority { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public NotificationStatus Status { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? PayloadJson { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? DispatchedAtUtc { get; private set; }
    public DateTimeOffset? ReadAtUtc { get; private set; }
    public bool IsRead => ReadAtUtc.HasValue;

    private NotificationMessage() { }

    public static Result<NotificationMessage> Create(
        Guid recipientUserId,
        NotificationCategory category,
        NotificationPriority priority,
        NotificationChannel channel,
        string title,
        string body,
        string? payloadJson = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Error.Validation("Title is required.");

        if (string.IsNullOrWhiteSpace(body))
            return Error.Validation("Body is required.");

        return new NotificationMessage
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            Category = category,
            Priority = priority,
            Channel = channel,
            Status = NotificationStatus.Queued,
            Title = title.Trim(),
            Body = body.Trim(),
            PayloadJson = payloadJson,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public void MarkDispatched()
    {
        Status = NotificationStatus.Dispatched;
        DispatchedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkRead()
    {
        Status = NotificationStatus.Read;
        ReadAtUtc = DateTimeOffset.UtcNow;
    }

    public void SuppressBudgetExceeded()
    {
        Status = NotificationStatus.SuppressedBudgetExceeded;
    }
}
