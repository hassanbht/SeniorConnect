using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Community.Domain;

public enum EventRsvpStatus
{
    Going,
    Interested,
    Waitlisted,
    Cancelled,
    NoShow
}

public sealed class EventRegistration : Entity, IAuditable
{
    private EventRegistration() { }

    [DataClass(DataClass.Operational)]
    public Guid EventId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid UserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public EventRsvpStatus Status { get; private set; } = EventRsvpStatus.Going;

    [DataClass(DataClass.Operational)]
    public int? WaitlistPosition { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? Note { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset RegisteredAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? CancelledAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static EventRegistration RegisterGoing(Guid eventId, Guid userId, string? note = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new EventRegistration
        {
            Id = Guid.CreateVersion7(),
            EventId = eventId,
            UserId = userId,
            Status = EventRsvpStatus.Going,
            WaitlistPosition = null,
            Note = note?.Trim(),
            RegisteredAtUtc = now,
            CreatedAtUtc = now,
            CreatedBy = userId,
            UpdatedAtUtc = now,
            UpdatedBy = userId
        };
    }

    public static EventRegistration RegisterInterested(Guid eventId, Guid userId)
    {
        var now = DateTimeOffset.UtcNow;
        return new EventRegistration
        {
            Id = Guid.CreateVersion7(),
            EventId = eventId,
            UserId = userId,
            Status = EventRsvpStatus.Interested,
            WaitlistPosition = null,
            RegisteredAtUtc = now,
            CreatedAtUtc = now,
            CreatedBy = userId,
            UpdatedAtUtc = now,
            UpdatedBy = userId
        };
    }

    public static EventRegistration RegisterWaitlist(Guid eventId, Guid userId, int waitlistPosition, string? note = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new EventRegistration
        {
            Id = Guid.CreateVersion7(),
            EventId = eventId,
            UserId = userId,
            Status = EventRsvpStatus.Waitlisted,
            WaitlistPosition = waitlistPosition,
            Note = note?.Trim(),
            RegisteredAtUtc = now,
            CreatedAtUtc = now,
            CreatedBy = userId,
            UpdatedAtUtc = now,
            UpdatedBy = userId
        };
    }

    public void PromoteToGoing()
    {
        Status = EventRsvpStatus.Going;
        WaitlistPosition = null;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        Status = EventRsvpStatus.Cancelled;
        CancelledAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkNoShow()
    {
        Status = EventRsvpStatus.NoShow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
