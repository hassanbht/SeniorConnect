using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.HelpRequests.Application;
using SeniorConnect.Modules.HelpRequests.Domain;
using SeniorConnect.Modules.Matching.Application;
using SeniorConnect.Modules.Matching.Domain;
using SeniorConnect.Modules.Profiles.Application;

namespace SeniorConnect.Modules.Matching.Infrastructure;

public sealed class MatchingService : IMatchingService
{
    private readonly IHelpRequestsDbContext _helpDb;
    private readonly IProfilesDbContext _profileDb;

    public MatchingService(IHelpRequestsDbContext helpDb, IProfilesDbContext profileDb)
    {
        _helpDb = helpDb;
        _profileDb = profileDb;
    }

    public async Task<Result<IReadOnlyList<MatchingCandidate>>> FindCandidatesAsync(
        Guid helpRequestId,
        MatchingConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        var cfg = config ?? new MatchingConfig();

        var request = await _helpDb.HelpRequests
            .FirstOrDefaultAsync(r => r.Id == helpRequestId && !r.IsDeleted, cancellationToken);

        if (request is null)
        {
            return Error.NotFound("HelpRequest");
        }

        var volunteers = await _profileDb.VolunteerProfiles
            .Where(v => v.IsAcceptingRequests)
            .ToListAsync(cancellationToken);

        // Find prior activities with this senior for continuity scoring
        var priorVolunteerIds = await _helpDb.Activities
            .Where(a => a.SubjectUserId == request.SeniorUserId && a.Status == ActivityStatus.Confirmed)
            .Select(a => a.VolunteerUserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var candidates = new List<MatchingCandidate>();

        foreach (var v in volunteers)
        {
            if (v.UserId == request.SeniorUserId) continue; // Senior cannot volunteer for themselves

            var ineligibility = new List<string>();

            // Calculate spatial distance
            double distanceKm = 5.0; // default if coordinates not set
            if (v.Latitude.HasValue && v.Longitude.HasValue && request.Latitude.HasValue && request.Longitude.HasValue)
            {
                distanceKm = CalculateDistanceKm(v.Latitude.Value, v.Longitude.Value, request.Latitude.Value, request.Longitude.Value);
                if (distanceKm > v.MaxDistanceKm)
                {
                    ineligibility.Add(string.Format(CultureInfo.InvariantCulture, "Distance ({0:F1} km) exceeds volunteer maximum ({1} km)", distanceKm, v.MaxDistanceKm));
                }
            }

            var isPriorMatch = priorVolunteerIds.Contains(v.UserId);
            var reliability = (double)(v.ReliabilityScore ?? (decimal)cfg.ColdStartReliability);

            // Calculate component scores [0.0 - 1.0]
            var distanceScore = Math.Max(0.0, 1.0 - (distanceKm / 20.0));
            var continuityScore = isPriorMatch ? 1.0 : 0.0;
            var reliabilityScore = Math.Clamp(reliability, 0.0, 1.0);
            var availabilityScore = 1.0; // verified active

            var totalScore = (distanceScore * cfg.DistanceWeight) +
                             (continuityScore * cfg.ContinuityWeight) +
                             (reliabilityScore * cfg.ReliabilityWeight) +
                             (availabilityScore * cfg.AvailabilityWeight);

            var explanation = isPriorMatch
                ? "Prior positive connection with senior (+30% continuity boost)"
                : string.Format(CultureInfo.InvariantCulture, "Nearby volunteer ({0:F1} km), reliability {1:P0}", distanceKm, reliability);

            var breakdown = new ScoreBreakdown(
                DistanceScore: distanceScore,
                ContinuityScore: continuityScore,
                ReliabilityScore: reliabilityScore,
                AvailabilityScore: availabilityScore,
                Explanation: explanation);

            candidates.Add(new MatchingCandidate(
                VolunteerUserId: v.UserId,
                TotalScore: Math.Round(totalScore, 3),
                DistanceKm: Math.Round(distanceKm, 1),
                IsPriorMatch: isPriorMatch,
                ReliabilityScore: reliability,
                Breakdown: breakdown,
                IsEligible: ineligibility.Count == 0,
                IneligibilityReasons: ineligibility));
        }

        var sorted = candidates
            .OrderByDescending(c => c.IsEligible)
            .ThenByDescending(c => c.TotalScore)
            .ToList();

        return Result<IReadOnlyList<MatchingCandidate>>.Success(sorted);
    }

    public async Task<Result<IReadOnlyList<VolunteerFeedItem>>> GetVolunteerFeedAsync(
        Guid volunteerUserId,
        double? latitude = null,
        double? longitude = null,
        double radiusKm = 20,
        CancellationToken cancellationToken = default)
    {
        var openRequests = await _helpDb.HelpRequests
            .Where(r => (r.Status == HelpRequestStatus.Open || r.Status == HelpRequestStatus.Offered || r.Status == HelpRequestStatus.Matching) && !r.IsDeleted && r.SeniorUserId != volunteerUserId)
            .OrderBy(r => r.ScheduledStartUtc)
            .ToListAsync(cancellationToken);

        var categories = await _helpDb.ActivityCategories
            .ToDictionaryAsync(c => c.Id, c => c.NameKey, cancellationToken);

        var items = new List<VolunteerFeedItem>();

        foreach (var r in openRequests)
        {
            double dist = 3.0;
            if (latitude.HasValue && longitude.HasValue && r.Latitude.HasValue && r.Longitude.HasValue)
            {
                dist = CalculateDistanceKm(latitude.Value, longitude.Value, r.Latitude.Value, r.Longitude.Value);
            }

            var categoryName = categories.TryGetValue(r.CategoryId, out var name) ? name : "activity.general";

            items.Add(new VolunteerFeedItem(
                HelpRequestId: r.Id,
                CategoryNameKey: categoryName,
                RequiredSafetyLevel: r.RequiredSafetyLevel,
                ScheduledStartUtc: r.ScheduledStartUtc,
                DurationMinutes: r.DurationMinutes,
                LocationCity: r.LocationCity ?? "City",
                LocationPostalCode: r.LocationPostalCode,
                DistanceKm: Math.Round(dist, 1),
                IsEligible: dist <= radiusKm,
                IneligibilityReason: dist > radiusKm ? "Outside preferred travel radius" : null));
        }

        return Result<IReadOnlyList<VolunteerFeedItem>>.Success(items);
    }

    public async Task<Result<IReadOnlyList<HybridMatchingProposal>>> GetHybridProposalsAsync(
        HybridMatchingRequest request,
        CancellationToken cancellationToken = default)
    {
        var helpRequest = await _helpDb.HelpRequests
            .FirstOrDefaultAsync(r => r.Id == request.HelpRequestId && !r.IsDeleted, cancellationToken);

        if (helpRequest is null)
        {
            return Error.NotFound("HelpRequest");
        }

        var candidatesResult = await FindCandidatesAsync(request.HelpRequestId, null, cancellationToken);
        if (candidatesResult.IsFailure) return candidatesResult.Error!;

        var eligibleCandidates = candidatesResult.Value!.Where(c => c.IsEligible).ToList();
        var proposals = new List<HybridMatchingProposal>();

        // Per ADR-014: AI proposes, humans decide. Any Safety Level 3+ requires manual human coordinator approval.
        var requiresManualApproval = helpRequest.RequiredSafetyLevel >= 3;

        foreach (var candidate in eligibleCandidates)
        {
            // Historical completion rate estimation
            var pastTotal = await _helpDb.Activities
                .CountAsync(a => a.VolunteerUserId == candidate.VolunteerUserId, cancellationToken);

            var pastConfirmed = await _helpDb.Activities
                .CountAsync(a => a.VolunteerUserId == candidate.VolunteerUserId && a.Status == ActivityStatus.Confirmed, cancellationToken);

            double completionProbability = pastTotal > 0
                ? (double)pastConfirmed / pastTotal
                : candidate.ReliabilityScore;

            completionProbability = Math.Clamp(completionProbability, 0.40, 0.99);

            // Combined hybrid score: 65% rule-based + 35% completion probability
            double combinedScore = Math.Round((candidate.TotalScore * 0.65) + (completionProbability * 0.35), 3);

            if (combinedScore < request.MinConfidenceThreshold) continue;

            string aiReason = candidate.IsPriorMatch
                ? string.Format(CultureInfo.InvariantCulture, "High continuity preference: previously completed activities for this beneficiary. Predicted completion: {0:P0}", completionProbability)
                : string.Format(CultureInfo.InvariantCulture, "Optimal proximity ({0:F1} km) and active reliability. Predicted completion: {1:P0}", candidate.DistanceKm, completionProbability);

            proposals.Add(new HybridMatchingProposal(
                HelpRequestId: helpRequest.Id,
                CandidateVolunteerUserId: candidate.VolunteerUserId,
                CombinedScore: combinedScore,
                RuleBasedScore: candidate.TotalScore,
                PredictedCompletionProbability: Math.Round(completionProbability, 2),
                Breakdown: candidate.Breakdown,
                AiRecommendationReason: aiReason,
                RequiresManualCoordinatorApproval: requiresManualApproval));
        }

        var sortedProposals = proposals
            .OrderByDescending(p => p.CombinedScore)
            .ToList();

        return Result<IReadOnlyList<HybridMatchingProposal>>.Success(sortedProposals);
    }

    private static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371.0; // Earth radius in km
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return r * c;
    }

    private static double ToRadians(double degrees) => degrees * (Math.PI / 180.0);
}
