using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Community.Domain;

public enum EventRecurrenceFrequency
{
    None,
    Daily,
    Weekly,
    BiWeekly,
    Monthly
}

public sealed class CommunityEvent : Entity, IAuditable, ISoftDeletable, IOrganizationScoped
{
    private CommunityEvent() { }

    [DataClass(DataClass.Operational)]
    public Guid HostUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? GroupId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? OrganizationId { get; private set; }

    [DataClass(DataClass.Operational)]
    public string Title { get; private set; } = string.Empty;

    [DataClass(DataClass.PersonalData)]
    public string Description { get; private set; } = string.Empty;

    [DataClass(DataClass.Operational)]
    public string Category { get; private set; } = "general";

    [DataClass(DataClass.PersonalData)]
    public string? LocationAddress { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? LocationPostalCode { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset StartsAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset EndsAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public EventRecurrenceFrequency RecurrenceFrequency { get; private set; } = EventRecurrenceFrequency.None;

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? RecurrenceUntilUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public int? Capacity { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsCancelled { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? CancellationReason { get; private set; }

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

    public static Result<CommunityEvent> Create(
        Guid hostUserId,
        string title,
        string description,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        string category = "general",
        Guid? groupId = null,
        Guid? organizationId = null,
        string? locationAddress = null,
        string? locationPostalCode = null,
        EventRecurrenceFrequency recurrenceFrequency = EventRecurrenceFrequency.None,
        DateTimeOffset? recurrenceUntilUtc = null,
        int? capacity = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Error.Validation("Title is required.");
        }

        if (endsAtUtc <= startsAtUtc)
        {
            return Error.Validation("Event end time must be after start time.");
        }

        if (capacity.HasValue && capacity.Value <= 0)
        {
            return Error.Validation("Capacity must be positive.");
        }

        var now = DateTimeOffset.UtcNow;
        var ev = new CommunityEvent
        {
            Id = Guid.CreateVersion7(),
            HostUserId = hostUserId,
            GroupId = groupId,
            OrganizationId = organizationId,
            Title = title.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Category = string.IsNullOrWhiteSpace(category) ? "general" : category.Trim().ToLowerInvariant(),
            LocationAddress = locationAddress?.Trim(),
            LocationPostalCode = locationPostalCode?.Trim(),
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = endsAtUtc,
            RecurrenceFrequency = recurrenceFrequency,
            RecurrenceUntilUtc = recurrenceUntilUtc,
            Capacity = capacity,
            IsCancelled = false,
            IsDeleted = false,
            CreatedAtUtc = now,
            CreatedBy = hostUserId,
            UpdatedAtUtc = now,
            UpdatedBy = hostUserId
        };

        return Result<CommunityEvent>.Success(ev);
    }

    public Result Reschedule(DateTimeOffset newStartsAtUtc, DateTimeOffset newEndsAtUtc, Guid hostUserId)
    {
        if (newEndsAtUtc <= newStartsAtUtc)
        {
            return Error.Validation("Event end time must be after start time.");
        }

        StartsAtUtc = newStartsAtUtc;
        EndsAtUtc = newEndsAtUtc;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = hostUserId;

        return Result.Success();
    }

    public Result UpdateDetails(
        string title,
        string description,
        string category,
        string? locationAddress,
        string? locationPostalCode,
        int? capacity,
        Guid updatedByUserId)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Error.Validation("Title is required.");
        }

        if (capacity.HasValue && capacity.Value <= 0)
        {
            return Error.Validation("Capacity must be positive.");
        }

        Title = title.Trim();
        Description = description?.Trim() ?? string.Empty;
        Category = string.IsNullOrWhiteSpace(category) ? "general" : category.Trim().ToLowerInvariant();
        LocationAddress = locationAddress?.Trim();
        LocationPostalCode = locationPostalCode?.Trim();
        Capacity = capacity;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedByUserId;

        return Result.Success();
    }

    public Result Cancel(string reason, Guid hostUserId)
    {
        IsCancelled = true;
        CancellationReason = reason;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = hostUserId;
        return Result.Success();
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    // P5-04: occurrences are computed on read, never persisted — only a
    // cancellation of a specific occurrence persists
    // (CommunityEventOccurrenceCancellation). Bounded to maxOccurrences so a
    // series with no RecurrenceUntilUtc can't enumerate forever.
    public IReadOnlyList<DateTimeOffset> ComputeOccurrenceStartsUtc(int maxOccurrences = 52)
    {
        if (RecurrenceFrequency == EventRecurrenceFrequency.None)
        {
            return [StartsAtUtc];
        }

        var step = RecurrenceFrequency switch
        {
            EventRecurrenceFrequency.Daily => TimeSpan.FromDays(1),
            EventRecurrenceFrequency.Weekly => TimeSpan.FromDays(7),
            EventRecurrenceFrequency.BiWeekly => TimeSpan.FromDays(14),
            // Approximate — this is a discovery/cancel-picker horizon, not a
            // billing calendar; exact month-length RRULE math is not needed here.
            EventRecurrenceFrequency.Monthly => TimeSpan.FromDays(30),
            _ => TimeSpan.Zero
        };

        var occurrences = new List<DateTimeOffset>();
        var current = StartsAtUtc;
        while (occurrences.Count < maxOccurrences
            && (RecurrenceUntilUtc is null || current <= RecurrenceUntilUtc.Value))
        {
            occurrences.Add(current);
            current += step;
        }

        return occurrences;
    }
}
