using System.Text.Json;
using SeniorConnect.Domain;

namespace SeniorConnect.Modules.HelpRequests.Domain;

public sealed class HelpRequest : Entity, IOrganizationScoped, IAuditable, ISoftDeletable
{
    private readonly object _gate = new();
    private HelpRequest() { }

    [DataClass(DataClass.Operational)]
    public Guid? OrganizationId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? BranchId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid SeniorUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid CreatedByUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid CategoryId { get; private set; }

    [DataClass(DataClass.Operational)]
    public int RequiredSafetyLevel { get; private set; } = 1;

    [DataClass(DataClass.Operational)]
    public int RequiredTrustLevel { get; private set; } = 1;

    [DataClass(DataClass.Operational)]
    public DateTimeOffset ScheduledStartUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset ScheduledEndUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public int DurationMinutes { get; private set; }

    [DataClass(DataClass.Operational)]
    public LocationType LocationType { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? LocationAddress { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? LocationPostalCode { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? LocationCity { get; private set; }

    [DataClass(DataClass.Operational)]
    public double? Latitude { get; private set; }

    [DataClass(DataClass.Operational)]
    public double? Longitude { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? Notes { get; private set; }

    [DataClass(DataClass.Operational)]
    public TransportMode TransportMode { get; private set; }

    [DataClass(DataClass.Operational)]
    public InsuranceContext InsuranceContext { get; private set; }

    [DataClass(DataClass.Operational)]
    public HelpRequestStatus Status { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? AssignedVolunteerUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public string OfferedVolunteersJson { get; private set; } = "[]";

    /// <summary>P3-13: 1 = first 3 candidates, 2 = first 7, 3 = all eligible.</summary>
    [DataClass(DataClass.Operational)]
    public int OfferTier { get; private set; } = 1;

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? TierAdvancedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? EscalatedToCoordinatorAtUtc { get; private set; }

    /// <summary>P3-18 / BR-NOTIFY-01: at most 2 reminders per assignment, ever.</summary>
    [DataClass(DataClass.Operational)]
    public DateTimeOffset? Reminder24hSentAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? Reminder2hSentAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? AssignedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? CheckedInAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? CheckedOutAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? CancellationReason { get; private set; }

    [DataClass(DataClass.Operational)]
    public CancellationReasonCode? CancellationReasonCode { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CancelledByUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? CancelledAtUtc { get; private set; }

    /// <summary>P3-19 / BR-HELP-06: the volunteer's reliability score as it
    /// was immediately before this no-show, so a successful dispute can
    /// restore it exactly rather than just nudging it back up.</summary>
    [DataClass(DataClass.Operational)]
    public decimal? PreNoShowReliabilityScore { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? NoShowDisputeReason { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? NoShowDisputedByUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? NoShowDisputedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public int RowVersion { get; private set; } = 1;

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

    public static Result<HelpRequest> Create(
        Guid? organizationId,
        Guid seniorUserId,
        Guid createdByUserId,
        Guid categoryId,
        int safetyLevel,
        int trustLevel,
        DateTimeOffset scheduledStartUtc,
        DateTimeOffset scheduledEndUtc,
        int durationMinutes,
        LocationType locationType,
        string? notes = null,
        string? address = null,
        string? postalCode = null,
        string? city = null,
        double? latitude = null,
        double? longitude = null,
        TransportMode transportMode = TransportMode.None,
        InsuranceContext insuranceContext = InsuranceContext.Unknown,
        Guid? branchId = null)
    {
        if (durationMinutes is < 5 or > 1440)
        {
            return Error.Validation("Duration must be between 5 and 1440 minutes.");
        }

        if (scheduledEndUtc <= scheduledStartUtc)
        {
            return Error.Validation("Scheduled end must be after scheduled start.");
        }

        var now = DateTimeOffset.UtcNow;
        var request = new HelpRequest
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = organizationId,
            BranchId = branchId,
            SeniorUserId = seniorUserId,
            CreatedByUserId = createdByUserId,
            CategoryId = categoryId,
            RequiredSafetyLevel = safetyLevel,
            RequiredTrustLevel = trustLevel,
            ScheduledStartUtc = scheduledStartUtc,
            ScheduledEndUtc = scheduledEndUtc,
            DurationMinutes = durationMinutes,
            LocationType = locationType,
            Notes = notes,
            LocationAddress = address,
            LocationPostalCode = postalCode,
            LocationCity = city,
            Latitude = latitude,
            Longitude = longitude,
            TransportMode = transportMode,
            InsuranceContext = insuranceContext,
            Status = HelpRequestStatus.Open,
            RowVersion = 1,
            CreatedAtUtc = now,
            CreatedBy = createdByUserId,
            UpdatedAtUtc = now,
            UpdatedBy = createdByUserId,
            IsDeleted = false
        };

        return Result<HelpRequest>.Success(request);
    }

    public Result Publish()
    {
        if (Status != HelpRequestStatus.Draft)
        {
            return Error.InvalidStateTransition(Status.ToString(), nameof(HelpRequestStatus.Open));
        }

        var now = DateTimeOffset.UtcNow;
        Status = HelpRequestStatus.Open;
        OfferTier = 1;
        TierAdvancedAtUtc = now;
        RowVersion++;
        UpdatedAtUtc = now;
        return Result.Success();
    }

    /// <summary>
    /// P3-13: widens the offer from 3 → 7 → all eligible candidates. Returns
    /// false when already at the widest tier — the caller must escalate to
    /// a coordinator instead of advancing further.
    /// </summary>
    public Result<bool> AdvanceOfferTier(IReadOnlyCollection<Guid> newlyOfferedVolunteerIds)
    {
        if (OfferTier >= 3)
        {
            return Result<bool>.Success(false);
        }

        OfferTier++;

        var already = JsonSerializer.Deserialize<List<Guid>>(OfferedVolunteersJson) ?? [];
        foreach (var id in newlyOfferedVolunteerIds)
        {
            if (!already.Contains(id))
            {
                already.Add(id);
            }
        }

        OfferedVolunteersJson = JsonSerializer.Serialize(already);
        TierAdvancedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        return Result<bool>.Success(true);
    }

    /// <summary>P3-13 / Gate 3 item 7: reached only once tier 3 also produces nothing.</summary>
    public Result MarkEscalatedToCoordinator()
    {
        if (EscalatedToCoordinatorAtUtc.HasValue)
        {
            return Error.Conflict("ALREADY_ESCALATED", "This request was already escalated to a coordinator.");
        }

        EscalatedToCoordinatorAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    /// <summary>P3-18 / BR-NOTIFY-01: the T-24h reminder, sent at most once.</summary>
    public Result MarkReminder24hSent()
    {
        if (Reminder24hSentAtUtc.HasValue)
        {
            return Error.Conflict("REMINDER_ALREADY_SENT", "The 24-hour reminder was already sent.");
        }

        Reminder24hSentAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    /// <summary>P3-18 / BR-NOTIFY-01: the T-2h reminder, sent at most once.</summary>
    public Result MarkReminder2hSent()
    {
        if (Reminder2hSentAtUtc.HasValue)
        {
            return Error.Conflict("REMINDER_ALREADY_SENT", "The 2-hour reminder was already sent.");
        }

        Reminder2hSentAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result MakeOffer(Guid volunteerUserId)
    {
        if (Status is not (HelpRequestStatus.Open or HelpRequestStatus.Matching or HelpRequestStatus.Offered))
        {
            return Error.InvalidStateTransition(Status.ToString(), nameof(HelpRequestStatus.Offered));
        }

        Status = HelpRequestStatus.Offered;
        RowVersion++;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result Assign(Guid volunteerUserId, int expectedRowVersion)
    {
        lock (_gate)
        {
        if (Status is not (HelpRequestStatus.Open or HelpRequestStatus.Offered or HelpRequestStatus.Matching))
        {
            return new Error("HELP_ALREADY_ASSIGNED", "This help request is no longer available to accept.", ErrorKind.Conflict);
        }

        if (RowVersion != expectedRowVersion)
        {
            return new Error("CONCURRENCY_CONFLICT", "The request was modified or claimed by another user.", ErrorKind.Conflict);
        }

        if (volunteerUserId == SeniorUserId)
        {
            return Error.Validation("A senior cannot be assigned to their own help request.");
        }

        Status = HelpRequestStatus.Assigned;
        AssignedVolunteerUserId = volunteerUserId;
        AssignedAtUtc = DateTimeOffset.UtcNow;
        RowVersion++;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = volunteerUserId;

        return Result.Success();
        }
    }

    public Result CheckIn(Guid volunteerUserId)
    {
        if (Status != HelpRequestStatus.Assigned)
        {
            return Error.InvalidStateTransition(Status.ToString(), nameof(HelpRequestStatus.InProgress));
        }

        if (AssignedVolunteerUserId != volunteerUserId)
        {
            return Error.Forbidden("Only the assigned volunteer can check in.");
        }

        Status = HelpRequestStatus.InProgress;
        CheckedInAtUtc = DateTimeOffset.UtcNow;
        RowVersion++;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = volunteerUserId;

        return Result.Success();
    }

    public Result Complete(Guid volunteerUserId, int? actualDurationMinutes = null)
    {
        if (Status is not (HelpRequestStatus.Assigned or HelpRequestStatus.InProgress))
        {
            return Error.InvalidStateTransition(Status.ToString(), nameof(HelpRequestStatus.Completed));
        }

        if (AssignedVolunteerUserId != volunteerUserId)
        {
            return Error.Forbidden("Only the assigned volunteer can complete this request.");
        }

        var duration = actualDurationMinutes ?? DurationMinutes;
        if (duration is < 5 or > 1440)
        {
            return Error.Validation("Actual duration must be between 5 and 1440 minutes.");
        }

        Status = HelpRequestStatus.Completed;
        DurationMinutes = duration;
        CheckedOutAtUtc ??= DateTimeOffset.UtcNow;
        CompletedAtUtc = DateTimeOffset.UtcNow;
        RowVersion++;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = volunteerUserId;

        // Raise domain event to generate Activity in Phase 2 module
        Raise(new HelpRequestCompleted(
            HelpRequestId: Id,
            OrganizationId: OrganizationId,
            BranchId: BranchId,
            VolunteerUserId: volunteerUserId,
            SubjectUserId: SeniorUserId,
            CategoryId: CategoryId,
            OccurredOn: DateOnly.FromDateTime(ScheduledStartUtc.UtcDateTime),
            DurationMinutes: DurationMinutes,
            LocationType: LocationType,
            TransportMode: TransportMode,
            InsuranceContext: InsuranceContext,
            Notes: Notes,
            OccurredAtUtc: CompletedAtUtc.Value));

        return Result.Success();
    }

    public Result Cancel(Guid cancelledByUserId, string reason, SeniorConnect.Modules.HelpRequests.Domain.CancellationReasonCode reasonCode = SeniorConnect.Modules.HelpRequests.Domain.CancellationReasonCode.Other)
    {
        if (Status is (HelpRequestStatus.Completed or HelpRequestStatus.Cancelled or HelpRequestStatus.Expired))
        {
            return Error.InvalidStateTransition(Status.ToString(), nameof(HelpRequestStatus.Cancelled));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("Cancellation reason is required.");
        }

        Status = HelpRequestStatus.Cancelled;
        CancelledByUserId = cancelledByUserId;
        CancellationReason = reason.Trim();
        CancellationReasonCode = reasonCode;
        CancelledAtUtc = DateTimeOffset.UtcNow;
        RowVersion++;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = cancelledByUserId;

        return Result.Success();
    }

    public Result MarkNoShow(Guid reportedByUserId, string? reason = null)
    {
        if (Status is not (HelpRequestStatus.Assigned or HelpRequestStatus.InProgress))
        {
            return Error.InvalidStateTransition(Status.ToString(), nameof(HelpRequestStatus.NoShow));
        }

        var now = DateTimeOffset.UtcNow;
        if (now < ScheduledStartUtc.AddMinutes(30))
        {
            return Error.Validation("No-show can only be reported at least 30 minutes after scheduled start.");
        }

        Status = HelpRequestStatus.NoShow;
        CancellationReason = reason?.Trim() ?? "Volunteer / Senior no-show";
        CancellationReasonCode = SeniorConnect.Modules.HelpRequests.Domain.CancellationReasonCode.Other;
        CancelledByUserId = reportedByUserId;
        CancelledAtUtc = now;
        RowVersion++;
        UpdatedAtUtc = now;
        UpdatedBy = reportedByUserId;

        return Result.Success();
    }

    /// <summary>Records the volunteer's reliability score as it was just
    /// before this no-show — called by the service layer right after a
    /// successful <see cref="MarkNoShow"/>, once the score has actually
    /// been read/updated elsewhere. Kept separate from MarkNoShow so the
    /// no-show's own validation never depends on reliability lookups.</summary>
    public void RecordPreNoShowReliabilityScore(decimal? score) => PreNoShowReliabilityScore = score;

    /// <summary>
    /// P3-19 / BR-HELP-06: a successful dispute REVERTS the reliability
    /// score to what it was before the no-show — the caller (service layer)
    /// reads <see cref="PreNoShowReliabilityScore"/> and restores it via
    /// IVolunteerReliabilityUpdater; this method only records that the
    /// dispute happened, once, and blocks re-disputing.
    /// </summary>
    public Result DisputeNoShow(Guid disputedByUserId, string reason)
    {
        if (Status != HelpRequestStatus.NoShow)
        {
            return Error.InvalidStateTransition(Status.ToString(), "NoShowDisputed");
        }

        if (NoShowDisputedAtUtc.HasValue)
        {
            return Error.Validation("This no-show has already been disputed.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("A dispute requires a reason.");
        }

        NoShowDisputeReason = reason.Trim();
        NoShowDisputedByUserId = disputedByUserId;
        NoShowDisputedAtUtc = DateTimeOffset.UtcNow;
        RowVersion++;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = disputedByUserId;

        return Result.Success();
    }

    public Result Expire()
    {
        if (Status is not (HelpRequestStatus.Open or HelpRequestStatus.Matching or HelpRequestStatus.Offered))
        {
            return Error.InvalidStateTransition(Status.ToString(), nameof(HelpRequestStatus.Expired));
        }

        Status = HelpRequestStatus.Expired;
        RowVersion++;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }
}

public enum CancellationReasonCode
{
    SeniorCancelled,
    VolunteerCancelled,
    NeedsMetOtherwise,
    SafetyConcern,
    ScheduleConflict,
    Emergency,
    Other
}

public enum HelpRequestStatus
{
    Draft,
    Open,
    Matching,
    Offered,
    Assigned,
    InProgress,
    Completed,
    Cancelled,
    Expired,
    NoShow
}

public sealed record HelpRequestCompleted(
    Guid HelpRequestId,
    Guid? OrganizationId,
    Guid? BranchId,
    Guid VolunteerUserId,
    Guid SubjectUserId,
    Guid CategoryId,
    DateOnly OccurredOn,
    int DurationMinutes,
    LocationType LocationType,
    TransportMode TransportMode,
    InsuranceContext InsuranceContext,
    string? Notes,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
