using SeniorConnect.Domain;
using SeniorConnect.Modules.Matching.Domain;

namespace SeniorConnect.Modules.Matching.Application;

public interface IMatchingService
{
    /// <summary>
    /// Hotfix: the candidate list for a help request, for a human caller.
    /// Only a Coordinator/Admin of the request's own organization (resolved
    /// from the request row, never from the client) may see it:
    ///   request unknown, community request (no org) or caller not a member → 404
    ///   member without the staff role                                      → 403
    /// </summary>
    Task<Result<IReadOnlyList<MatchingCandidate>>> GetCandidatesForCallerAsync(
        Guid helpRequestId,
        Guid callerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Hotfix: same authorization as <see cref="GetCandidatesForCallerAsync"/>.</summary>
    Task<Result<IReadOnlyList<HybridMatchingProposal>>> GetHybridProposalsForCallerAsync(
        HybridMatchingRequest request,
        Guid callerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hotfix: the origin is the volunteer's own stored location, read
    /// server-side — a client-supplied point is never used.
    /// </summary>
    Task<Result<IReadOnlyList<VolunteerFeedItem>>> GetVolunteerFeedAsync(
        Guid volunteerUserId,
        double radiusKm = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// P3-13: widens stale, still-unassigned requests from tier 1 (3
    /// candidates) → tier 2 (7) → tier 3 (all eligible) → escalated to the
    /// organization's coordinators. Intended to be called periodically by a
    /// hosted service. Returns the number of requests it acted on.
    /// </summary>
    Task<int> AdvanceStaleOffersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// P3-18 / BR-NOTIFY-01: dispatches the T-24h and T-2h assignment
    /// reminders, each at most once per assignment. Intended to be called
    /// periodically by a hosted service. Returns the number of reminders sent.
    /// </summary>
    Task<int> DispatchDueAssignmentRemindersAsync(CancellationToken cancellationToken = default);
}
