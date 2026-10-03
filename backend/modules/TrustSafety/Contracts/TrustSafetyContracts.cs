using SeniorConnect.Domain;

namespace SeniorConnect.Modules.TrustSafety.Contracts;

/// <summary>
/// Cross-module contract for safety boundaries, buddy rules, and user blocks.
/// </summary>
public interface ISafetyBoundaryReader
{
    Task<bool> IsBlockedAsync(Guid userIdA, Guid userIdB, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetBlockedUserIdsAsync(Guid userId, IReadOnlyCollection<Guid> candidateUserIds, CancellationToken ct = default);
    Task<bool> IsBuddyRequiredForLevel3Async(Guid volunteerUserId, CancellationToken ct = default);
}

/// <summary>
/// Cross-module read contract for open safeguarding cases. Used by eligibility
/// gates (e.g. BR-COMM-06 / ADR-025) to block group creation while a user has
/// an open safeguarding case naming them as the subject.
/// </summary>
public interface ISafeguardingCaseReader
{
    Task<bool> HasOpenCaseAsSubjectAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>Phase 4: the four things a safeguarding restriction can withhold.</summary>
public enum RestrictionKind
{
    Discovery,
    Location,
    Contact,
    Assignment
}

/// <summary>
/// Phase 4: what a restriction does, and nothing else. No case id, reason,
/// officer or severity crosses the safeguarding boundary (BR-SG-05).
/// </summary>
public sealed record ActiveRestriction(
    Guid SubjectUserId,
    Guid? CounterpartUserId,
    bool Discovery,
    bool Location,
    bool Contact,
    bool Assignment)
{
    public bool Blocks(RestrictionKind kind) => kind switch
    {
        RestrictionKind.Discovery => Discovery,
        RestrictionKind.Location => Location,
        RestrictionKind.Contact => Contact,
        _ => Assignment
    };

    /// <summary>
    /// Applies between <paramref name="a"/> and <paramref name="b"/> in
    /// either role. A restriction without a counterpart applies to the
    /// subject towards everyone.
    /// </summary>
    public bool AppliesBetween(Guid a, Guid b) =>
        (SubjectUserId == a && (CounterpartUserId is null || CounterpartUserId == b))
        || (SubjectUserId == b && (CounterpartUserId is null || CounterpartUserId == a));
}

public static class ActiveRestrictionExtensions
{
    public static bool Restricts(this IEnumerable<ActiveRestriction> restrictions, RestrictionKind kind, Guid a, Guid b) =>
        restrictions.Any(r => r.Blocks(kind) && r.AppliesBetween(a, b));
}

/// <summary>
/// Phase 4: the safety hook other modules consult for discovery, location,
/// contact and assignment. Read fresh on every call.
/// </summary>
public interface ISafeguardingRestrictionReader
{
    /// <summary>Active restrictions where any of the given users is subject or counterpart.</summary>
    Task<IReadOnlyList<ActiveRestriction>> GetActiveAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
}

/// <summary>
/// S-2/S-3 (Options B, approved 2026-09-30): cross-module read contract for the
/// coordinator surfaces. It exposes ONLY content-free operational facts:
/// that a participant needs an operational follow-up, and that they are
/// currently unavailable for operational reasons. No restriction kind, no
/// reason, no case reference, no officer identity crosses this boundary
/// (BR-SG-05); the leak-sweep tests keep it that way.
/// </summary>
public interface ISafeguardingOperationalReader
{
    /// <summary>User ids that have an OPEN operational follow-up task, restricted to the given organization.</summary>
    Task<IReadOnlySet<Guid>> GetOpenFollowUpUserIdsAsync(Guid organizationId, CancellationToken ct = default);
}

/// <summary>
/// B-2 (Option A, approved 2026-09-30): the Community module's ONE permitted
/// write direction into safeguarding — a manual officer conversion of an
/// escalated unsafe-behavior report (BR-SG-01: safeguarding is not an
/// extension of ordinary reporting, so nothing here raises automatically).
///
/// Boundary hygiene (ModuleBoundaryTests): severity crosses as a neutral
/// string; only the new case's id returns. No Domain or Application type of
/// either module appears in this signature.
/// </summary>
public interface ISafeguardingCaseRaiser
{
    /// <summary>
    /// Raises a new safeguarding case. <paramref name="severity"/> is one of
    /// "Low"/"Medium"/"High"/"Critical" (TrustSafety severity vocabulary);
    /// unknown values fail closed with a validation error.
    /// </summary>
    Task<Result<Guid>> RaiseCaseAsync(
        Guid reporterUserId,
        Guid subjectUserId,
        string summary,
        string severity,
        string category,
        Guid? organizationId,
        CancellationToken ct = default);
}

/// <summary>
/// B-1 (Option B, approved 2026-09-30): implemented by the HelpRequests module.
/// When a block between two people is created, every assignment between them
/// that is live (Assigned / InProgress) is unassigned deterministically and
/// idempotently. Returns the number of assignments that were unassigned by
/// this call. No block reason crosses this boundary.
/// </summary>
public interface IAssignmentBlockEnforcementPort
{
    Task<int> UnassignLiveAssignmentsBetweenAsync(Guid userA, Guid userB, CancellationToken ct = default);
}
