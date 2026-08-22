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
}
