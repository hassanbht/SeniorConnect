using SeniorConnect.Domain;

namespace SeniorConnect.Modules.TrustSafety.Domain;

/// <summary>
/// Phase 4 (docs/security/safeguarding-location-policy.md): a safety hook,
/// not a case-management workflow. A safeguarding officer attaches it to a
/// case they may see. It restricts the case subject, either towards everyone
/// or only towards one counterpart. Readers outside safeguarding learn only
/// which restrictions apply, never the case, its reason or its officer
/// (BR-SG-05).
/// </summary>
public sealed class SafeguardingRestriction : Entity
{
    private SafeguardingRestriction() { }

    [DataClass(DataClass.Operational)]
    public Guid CaseId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid SubjectUserId { get; private set; }

    /// <summary>Null: towards everyone. Set: only between the subject and this person.</summary>
    [DataClass(DataClass.Operational)]
    public Guid? CounterpartUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool BlocksDiscovery { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool BlocksLocation { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool BlocksContact { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool BlocksAssignment { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid CreatedByOfficerUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? LiftedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? LiftedByOfficerUserId { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? LiftReason { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsActive => LiftedAtUtc is null;

    public static Result<SafeguardingRestriction> Impose(
        SafeguardingCase sCase,
        Guid officerUserId,
        Guid? counterpartUserId,
        bool blocksDiscovery,
        bool blocksLocation,
        bool blocksContact,
        bool blocksAssignment)
    {
        if (!(blocksDiscovery || blocksLocation || blocksContact || blocksAssignment))
        {
            return Error.Validation("At least one restriction must be selected.");
        }
        if (counterpartUserId == sCase.SubjectUserId)
        {
            return Error.Validation("The counterpart must be a different person than the case subject.");
        }
        if (sCase.Status is SafeguardingStatus.Resolved or SafeguardingStatus.Closed)
        {
            return Error.Conflict("SAFEGUARDING_CASE_CLOSED", "A closed case cannot impose new restrictions.");
        }

        return Result<SafeguardingRestriction>.Success(new SafeguardingRestriction
        {
            Id = Guid.CreateVersion7(),
            CaseId = sCase.Id,
            SubjectUserId = sCase.SubjectUserId,
            CounterpartUserId = counterpartUserId,
            BlocksDiscovery = blocksDiscovery,
            BlocksLocation = blocksLocation,
            BlocksContact = blocksContact,
            BlocksAssignment = blocksAssignment,
            CreatedByOfficerUserId = officerUserId,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
    }

    public Result Lift(Guid officerUserId, string reason)
    {
        if (!IsActive)
        {
            return Error.Conflict("SAFEGUARDING_RESTRICTION_ALREADY_LIFTED", "The restriction is no longer active.");
        }
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("A reason is required to lift a restriction.");
        }
        LiftedAtUtc = DateTimeOffset.UtcNow;
        LiftedByOfficerUserId = officerUserId;
        LiftReason = reason.Trim();
        return Result.Success();
    }
}
