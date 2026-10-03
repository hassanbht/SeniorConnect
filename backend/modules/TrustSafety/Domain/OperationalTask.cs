using SeniorConnect.Domain;

namespace SeniorConnect.Modules.TrustSafety.Domain;

/// <summary>
/// S-2 (Option B, approved 2026-09-30): a content-free operational task for the
/// coordinator role when a safeguarding restriction is imposed.
///
/// Deliberately carries NO safeguarding reason, NO restriction kind, NO case
/// reference and NO officer identity (BR-SG-02/05) — the task only says that a
/// participant needs an operational follow-up. Tenant-scoped by OrganizationId
/// (null tasks are never created; org-less cases stay entirely with platform
/// officers, see S-4).
/// </summary>
public sealed class OperationalTask : Entity, IAuditable
{
    private OperationalTask() { }

    [DataClass(DataClass.Operational)]
    public Guid? OrganizationId { get; private set; }

    /// <summary>The participant the operational follow-up is about.</summary>
    [DataClass(DataClass.PersonalData)]
    public Guid SubjectUserId { get; private set; }

    /// <summary>Fixed content-free marker; never encodes the restriction type.</summary>
    [DataClass(DataClass.Operational)]
    public string TaskKind { get; private set; } = OperationalTaskKinds.SafeguardingFollowUp;

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid CreatedByUserId { get; private set; }


    [DataClass(DataClass.Operational)]
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CompletedByUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? CancelledAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsOpen => CompletedAtUtc is null && CancelledAtUtc is null;

    // IAuditable (get-only): the officer user doubles as CreatedBy/UpdatedBy.
    Guid? IAuditable.CreatedBy => CreatedByUserId;
    Guid? IAuditable.UpdatedBy => CompletedByUserId;

    public static OperationalTask Create(
        Guid? organizationId,
        Guid subjectUserId,
        Guid createdByUserId)
    {
        if (organizationId is null)
        {
            throw new InvalidOperationException(
                "S-2: operational tasks are only created for organization-scoped cases; org-less cases stay with platform officers.");
        }

        var now = DateTimeOffset.UtcNow;
        return new OperationalTask
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = organizationId,
            SubjectUserId = subjectUserId,
            CreatedAtUtc = now,
            CreatedByUserId = createdByUserId,
            UpdatedAtUtc = now
        };
    }

    public Result Complete(Guid completedByUserId)
    {
        if (!IsOpen)
        {
            return Error.InvalidStateTransition("Closed", nameof(Complete));
        }

        CompletedAtUtc = DateTimeOffset.UtcNow;
        CompletedByUserId = completedByUserId;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public void Cancel()
    {
        if (!IsOpen)
        {
            return; // idempotent cancellation
        }

        CancelledAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}

/// <summary>Content-free task kinds. Never encode restriction kinds here.</summary>
public static class OperationalTaskKinds
{
    public const string SafeguardingFollowUp = "SAFEGUARDING_FOLLOW_UP";
}
