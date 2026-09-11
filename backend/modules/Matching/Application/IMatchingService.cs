using SeniorConnect.Domain;
using SeniorConnect.Modules.Matching.Domain;

namespace SeniorConnect.Modules.Matching.Application;

public interface IMatchingService
{
    Task<Result<IReadOnlyList<MatchingCandidate>>> FindCandidatesAsync(
        Guid helpRequestId,
        MatchingConfig? config = null,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<VolunteerFeedItem>>> GetVolunteerFeedAsync(
        Guid volunteerUserId,
        double? latitude = null,
        double? longitude = null,
        double radiusKm = 20,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<HybridMatchingProposal>>> GetHybridProposalsAsync(
        HybridMatchingRequest request,
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
