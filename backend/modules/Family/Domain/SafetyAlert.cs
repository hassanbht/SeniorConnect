using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Family.Domain;

public enum SafetyAlertCategory
{
    MissedCheckIn = 0,
    WelfareCheckRequested = 1,
    UnusualInactivity = 2,
    AssistanceRequested = 3,
    Other = 4
}

public enum SafetyAlertStatus
{
    Active = 0,
    Acknowledged = 1,
    Resolved = 2,
    Dismissed = 3
}

public sealed class SafetyAlert
{
    public Guid Id { get; private set; }
    public Guid SeniorUserId { get; private set; }
    public Guid TriggeredByUserId { get; private set; }
    public SafetyAlertCategory Category { get; private set; }
    public SafetyAlertStatus Status { get; private set; }
    public string Details { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? AcknowledgedAtUtc { get; private set; }
    public Guid? AcknowledgedByUserId { get; private set; }
    public DateTimeOffset? ResolvedAtUtc { get; private set; }
    public Guid? ResolvedByUserId { get; private set; }
    public string? ResolutionNotes { get; private set; }

    private SafetyAlert() { }

    public static Result<SafetyAlert> Create(
        Guid seniorUserId,
        Guid triggeredByUserId,
        SafetyAlertCategory category,
        string details)
    {
        if (string.IsNullOrWhiteSpace(details))
            return Error.Validation("Details are required for safety alert.");

        return new SafetyAlert
        {
            Id = Guid.NewGuid(),
            SeniorUserId = seniorUserId,
            TriggeredByUserId = triggeredByUserId,
            Category = category,
            Status = SafetyAlertStatus.Active,
            Details = details.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public Result Acknowledge(Guid userId)
    {
        if (Status != SafetyAlertStatus.Active)
            return Error.Conflict("ALREADY_HANDLED", "Safety alert is no longer active.");

        Status = SafetyAlertStatus.Acknowledged;
        AcknowledgedAtUtc = DateTimeOffset.UtcNow;
        AcknowledgedByUserId = userId;
        return Result.Success();
    }

    public Result Resolve(Guid userId, string resolutionNotes)
    {
        if (Status == SafetyAlertStatus.Resolved || Status == SafetyAlertStatus.Dismissed)
            return Error.Conflict("ALREADY_RESOLVED", "Safety alert is already resolved.");

        Status = SafetyAlertStatus.Resolved;
        ResolvedAtUtc = DateTimeOffset.UtcNow;
        ResolvedByUserId = userId;
        ResolutionNotes = resolutionNotes.Trim();
        return Result.Success();
    }
}
