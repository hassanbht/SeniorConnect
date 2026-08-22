using SeniorConnect.Domain;

namespace SeniorConnect.Modules.HelpRequests.Domain;

/// <summary>
/// The Phase 2 wedge, in one aggregate.
///
/// Deliberately standalone: it does NOT depend on HelpRequest, which does not
/// exist until Phase 3. A coordinator must be able to record
/// "Anna visited Frau Müller, Tuesday, 90 minutes" on day one (F1).
///
/// In Phase 3 a completed HelpRequest produces an Activity — the model does not
/// fork, and volunteer hours keep one source of truth.
/// </summary>
public sealed class Activity : Entity, IOrganizationScoped, IAuditable, ISoftDeletable
{
    private Activity() { } // EF

    // --- Scope -------------------------------------------------------------

    /// <summary>Null = independent / neighbourly. ADR-007.</summary>
    [DataClass(DataClass.Operational)]
    public Guid? OrganizationId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? BranchId { get; private set; }

    // --- Parties -----------------------------------------------------------

    [DataClass(DataClass.Operational)]
    public Guid VolunteerUserId { get; private set; }

    /// <summary>Null for group activities with no single subject.</summary>
    [DataClass(DataClass.Operational)]
    public Guid? SubjectUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid CategoryId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? HelpRequestId { get; private set; } // Phase 3

    [DataClass(DataClass.Operational)]
    public Guid? EventId { get; private set; }       // Phase 5

    // --- What happened -----------------------------------------------------

    [DataClass(DataClass.Operational)]
    public DateOnly OccurredOn { get; private set; }

    [DataClass(DataClass.Operational)]
    public int DurationMinutes { get; private set; }

    [DataClass(DataClass.Operational)]
    public LocationType LocationType { get; private set; }

    /// <summary>
    /// Free text written by a user, so PersonalData.
    /// BR-VISIBILITY-01: an operational instruction, never a diagnosis.
    /// </summary>
    [DataClass(DataClass.PersonalData)]
    public string? Notes { get; private set; }

    // --- Insurance (F4, BR-SAFETY-06) --------------------------------------

    [DataClass(DataClass.Operational)]
    public InsuranceContext InsuranceContext { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? InsuranceDisclaimerAcceptedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? InsuranceDisclaimerAcceptedByUserId { get; private set; }

    // --- Transport (BR-TRANSPORT) ------------------------------------------

    [DataClass(DataClass.Operational)]
    public TransportMode TransportMode { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool InvolvesTransport => TransportMode != TransportMode.None;

    // --- Provenance --------------------------------------------------------

    [DataClass(DataClass.Operational)]
    public ActivitySource Source { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid LoggedByUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset LoggedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? ConfirmedByUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? ConfirmedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public ActivityStatus Status { get; private set; }

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

    // =======================================================================
    // Behaviour
    // =======================================================================

    public static Result<Activity> Log(
        Guid? organizationId,
        Guid volunteerUserId,
        Guid? subjectUserId,
        ActivityCategory category,
        DateOnly occurredOn,
        int durationMinutes,
        LocationType locationType,
        TransportMode transportMode,
        InsuranceContext insuranceContext,
        ActivitySource source,
        Guid loggedByUserId,
        DateOnly today,
        string? notes = null)
    {
        // BR-SCOPE-02/03. A blocked category never becomes a record; the caller
        // must respond with a referral instead.
        if (category.IsBlocked)
        {
            return Error.CategoryBlocked(category.ReferralGroup!);
        }

        if (durationMinutes is < 1 or > 1440)
        {
            return Error.Validation("Duration must be between 1 and 1440 minutes.");
        }

        // Deliberately validated here rather than as a database CHECK: a CHECK
        // using CURRENT_DATE is re-evaluated on every later UPDATE and during
        // pg_restore, so a row valid when written could block an unrelated
        // update years later.
        if (occurredOn > today)
        {
            return Error.Validation("An activity cannot be recorded in the future.");
        }

        if (subjectUserId == volunteerUserId)
        {
            return Error.Validation("A volunteer cannot be the subject of their own activity.");
        }

        var now = DateTimeOffset.UtcNow;

        return new Activity
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = organizationId,
            VolunteerUserId = volunteerUserId,
            SubjectUserId = subjectUserId,
            CategoryId = category.Id,
            OccurredOn = occurredOn,
            DurationMinutes = durationMinutes,
            LocationType = locationType,
            TransportMode = transportMode,
            InsuranceContext = insuranceContext,
            Notes = notes,
            Source = source,
            LoggedByUserId = loggedByUserId,
            LoggedAtUtc = now,
            Status = ActivityStatus.Logged,
            CreatedAtUtc = now,
            CreatedBy = loggedByUserId,
            UpdatedAtUtc = now,
            UpdatedBy = loggedByUserId,
        };
    }

    /// <summary>
    /// Resolves the insurance question. Required before a private-vehicle
    /// activity can be confirmed (BR-TRANSPORT-04).
    /// </summary>
    public Result ResolveInsurance(
        InsuranceContext context,
        Guid resolvedByUserId,
        bool disclaimerAccepted)
    {
        if (context == InsuranceContext.Unknown)
        {
            return Error.Validation("Cannot resolve the insurance context to 'unknown'.");
        }

        // Neighbourly help carries no organizational cover, so somebody has to
        // acknowledge who bears the risk — in writing, attributed, stored.
        if (context == InsuranceContext.PrivateNeighbourly && !disclaimerAccepted)
        {
            return Error.Validation(
                "Private neighbourly help requires an acknowledged disclaimer.");
        }

        InsuranceContext = context;

        if (disclaimerAccepted)
        {
            InsuranceDisclaimerAcceptedAtUtc = DateTimeOffset.UtcNow;
            InsuranceDisclaimerAcceptedByUserId = resolvedByUserId;
        }

        Touch(resolvedByUserId);
        return Result.Success();
    }

    /// <summary>
    /// Confirmation is what turns a logged activity into a countable hour.
    /// Everything the product is sold on flows from here.
    /// </summary>
    public Result Confirm(Guid confirmedByUserId)
    {
        if (Status != ActivityStatus.Logged)
        {
            return Error.InvalidStateTransition(Status.ToString(), nameof(ActivityStatus.Confirmed));
        }

        // BR-TRANSPORT-04. This is the primary guard; the database CHECK
        // constraint is the backstop. Both exist on purpose — F4 records a real
        // insurance dispute between a Gemeinde and an association.
        if (TransportMode == TransportMode.VolunteerPrivateVehicle
            && InsuranceContext == InsuranceContext.Unknown)
        {
            return Error.InsuranceUnresolved();
        }

        Status = ActivityStatus.Confirmed;
        ConfirmedByUserId = confirmedByUserId;
        ConfirmedAtUtc = DateTimeOffset.UtcNow;
        Touch(confirmedByUserId);

        Raise(new ActivityConfirmed(Id, VolunteerUserId, OrganizationId,
            DurationMinutes, DateTimeOffset.UtcNow));

        return Result.Success();
    }

    public Result Dispute(Guid disputedByUserId, string reason)
    {
        if (Status is not (ActivityStatus.Logged or ActivityStatus.Confirmed))
        {
            return Error.InvalidStateTransition(Status.ToString(), nameof(ActivityStatus.Disputed));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("A dispute requires a reason.");
        }

        Status = ActivityStatus.Disputed;
        Touch(disputedByUserId);
        return Result.Success();
    }

    private void Touch(Guid byUserId)
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = byUserId;
    }
}

public sealed record ActivityConfirmed(
    Guid ActivityId,
    Guid VolunteerUserId,
    Guid? OrganizationId,
    int DurationMinutes,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public enum ActivityStatus { Draft, Logged, Confirmed, Disputed, Cancelled }

public enum ActivitySource { SelfLogged, CoordinatorLogged, FromHelpRequest, FromEvent }

public enum LocationType { SeniorHome, PublicPlace, Institution, Organization, Other }

public enum InsuranceContext { Unknown, OrganizationCovered, PrivateNeighbourly }

/// <summary>
/// BR-TRANSPORT-01. Orthogonal to safety level: safety levels model access to a
/// PERSON, transport models liability for a JOURNEY.
/// </summary>
public enum TransportMode
{
    None,
    PublicTransportTogether,
    VolunteerPrivateVehicle,
    OrganizationVehicle,
    TaxiProfessional,
}

/// <summary>Reference data. Loaded, not created by users.</summary>
public sealed class ActivityCategory : Entity
{
    private ActivityCategory() { }

    [DataClass(DataClass.Operational)]
    public string Code { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public string NameKey { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public int DefaultSafetyLevel { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsBlocked { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? ReferralGroup { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsActive { get; private set; }
}
