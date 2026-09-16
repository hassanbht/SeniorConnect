using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SeniorConnect.Domain;
using SeniorConnect.Modules.HelpRequests.Application;
using SeniorConnect.Modules.HelpRequests.Domain;
using SeniorConnect.Modules.Identity.Contracts;
using SeniorConnect.Modules.Matching.Application;
using SeniorConnect.Modules.Matching.Domain;
using SeniorConnect.Modules.Notifications.Application;
using SeniorConnect.Modules.Notifications.Domain;
using SeniorConnect.Modules.Organizations.Contracts;
using SeniorConnect.Modules.Profiles.Application;
using SeniorConnect.Modules.TrustSafety.Contracts;

namespace SeniorConnect.Modules.Matching.Infrastructure;

public sealed class MatchingService : IMatchingService
{
    // P3-13: how long a tier sits stale before widening/escalating.
    private static readonly TimeSpan TierEscalationInterval = TimeSpan.FromMinutes(15);

    private readonly IHelpRequestsDbContext _helpDb;
    private readonly IProfilesDbContext _profileDb;
    private readonly ITrustLevelReader _trustLevelReader;
    private readonly ISafetyBoundaryReader _safetyBoundaryReader;
    private readonly IOptions<MatchingConfig> _configuredWeights;
    private readonly INotificationService _notificationService;
    private readonly IOrganizationCoordinatorReader _coordinatorReader;

    public MatchingService(
        IHelpRequestsDbContext helpDb,
        IProfilesDbContext profileDb,
        ITrustLevelReader trustLevelReader,
        ISafetyBoundaryReader safetyBoundaryReader,
        IOptions<MatchingConfig> configuredWeights,
        INotificationService notificationService,
        IOrganizationCoordinatorReader coordinatorReader)
    {
        _helpDb = helpDb;
        _profileDb = profileDb;
        _trustLevelReader = trustLevelReader;
        _safetyBoundaryReader = safetyBoundaryReader;
        _configuredWeights = configuredWeights;
        _notificationService = notificationService;
        _coordinatorReader = coordinatorReader;
    }

    public async Task<Result<IReadOnlyList<MatchingCandidate>>> FindCandidatesAsync(
        Guid helpRequestId,
        MatchingConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        // P3-09 / Gate 3 item 6: an explicit caller-supplied config still
        // wins (used by tests and any future A/B path); otherwise the
        // configured "Matching:Weights" section from appsettings.json is
        // used, so changing a weight never requires a rebuild.
        var cfg = config ?? _configuredWeights.Value;

        var request = await _helpDb.HelpRequests
            .FirstOrDefaultAsync(r => r.Id == helpRequestId && !r.IsDeleted, cancellationToken);

        if (request is null)
        {
            return Error.NotFound("HelpRequest");
        }

        var volunteers = await _profileDb.VolunteerProfiles
            .Where(v => v.IsAcceptingRequests)
            .ToListAsync(cancellationToken);

        var volunteerUserIds = volunteers.Select(v => v.UserId).ToList();

        // P4-16: Query blocked relationships
        var blockedVolunteerIds = await _safetyBoundaryReader.GetBlockedUserIdsAsync(request.SeniorUserId, volunteerUserIds, cancellationToken);

        // P4-06: Query latest verified trust level snapshots
        var latestSnapshots = await _trustLevelReader.GetEffectiveTrustLevelsAsync(volunteerUserIds, cancellationToken);

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
            if (blockedVolunteerIds.Contains(v.UserId)) continue; // P4-16: Exclude blocked users entirely

            var ineligibility = new List<string>();

            // P4-06: Hard Trust Level Invariant (BR-TRUST-03)
            var trustLevel = latestSnapshots.TryGetValue(v.UserId, out var lvl) ? lvl : 0;
            if (trustLevel < request.RequiredTrustLevel)
            {
                ineligibility.Add(string.Format(CultureInfo.InvariantCulture, "Volunteer trust level (L{0}) is below required level (L{1})", trustLevel, request.RequiredTrustLevel));
            }

            // P4-07: Buddy System — checked here too, not just at accept, so a
            // volunteer who cannot yet accept a Safety Level 3+ request
            // without a buddy is never offered one in the first place.
            if (request.RequiredSafetyLevel >= 3
                && await _safetyBoundaryReader.IsBuddyRequiredForLevel3Async(v.UserId, cancellationToken))
            {
                ineligibility.Add("Requires an assigned buddy for the first three Safety Level 3+ visits");
            }

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
        // P4-06 & P4-16: Query volunteer's effective trust level & block list
        var volunteerTrustLevel = await _trustLevelReader.GetEffectiveTrustLevelAsync(volunteerUserId, cancellationToken);

        var openRequests = await _helpDb.HelpRequests
            .Where(r => (r.Status == HelpRequestStatus.Open || r.Status == HelpRequestStatus.Offered || r.Status == HelpRequestStatus.Matching) 
                     && !r.IsDeleted 
                     && r.SeniorUserId != volunteerUserId)
            .OrderBy(r => r.ScheduledStartUtc)
            .ToListAsync(cancellationToken);

        var requestSeniorIds = openRequests.Select(r => r.SeniorUserId).Distinct().ToList();
        var blockedSeniorIds = await _safetyBoundaryReader.GetBlockedUserIdsAsync(volunteerUserId, requestSeniorIds, cancellationToken);
        openRequests = openRequests.Where(r => !blockedSeniorIds.Contains(r.SeniorUserId)).ToList();

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

            // P4-06: Check trust level eligibility
            var isTrustEligible = volunteerTrustLevel >= r.RequiredTrustLevel;
            var isDistEligible = dist <= radiusKm;
            var isEligible = isTrustEligible && isDistEligible;

            string? ineligibilityReason = null;
            if (!isTrustEligible)
            {
                ineligibilityReason = string.Format(CultureInfo.InvariantCulture, "Requires Trust Level {0} (you currently have Level {1})", r.RequiredTrustLevel, volunteerTrustLevel);
            }
            else if (!isDistEligible)
            {
                ineligibilityReason = "Outside preferred travel radius";
            }

            items.Add(new VolunteerFeedItem(
                HelpRequestId: r.Id,
                CategoryNameKey: categoryName,
                RequiredSafetyLevel: r.RequiredSafetyLevel,
                ScheduledStartUtc: r.ScheduledStartUtc,
                DurationMinutes: r.DurationMinutes,
                LocationCity: r.LocationCity ?? "City",
                LocationPostalCode: r.LocationPostalCode,
                DistanceKm: Math.Round(dist, 1),
                IsEligible: isEligible,
                IneligibilityReason: ineligibilityReason,
                RowVersion: r.RowVersion));
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

    public async Task<int> AdvanceStaleOffersAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now - TierEscalationInterval;

        var staleRequests = await _helpDb.HelpRequests
            .Where(r => !r.IsDeleted
                     && (r.Status == HelpRequestStatus.Open || r.Status == HelpRequestStatus.Matching || r.Status == HelpRequestStatus.Offered)
                     && r.EscalatedToCoordinatorAtUtc == null
                     && r.TierAdvancedAtUtc != null
                     && r.TierAdvancedAtUtc <= cutoff)
            .ToListAsync(cancellationToken);

        var actedOn = 0;

        foreach (var request in staleRequests)
        {
            if (request.OfferTier >= 3)
            {
                // Tier 3 has been sitting stale too — this is what Gate 3
                // item 7 means by "tiers 1-3 produce nothing".
                var escalateResult = request.MarkEscalatedToCoordinator();
                if (escalateResult.IsFailure)
                {
                    continue;
                }

                actedOn++;

                if (request.OrganizationId.HasValue)
                {
                    var coordinatorIds = await _coordinatorReader.GetActiveCoordinatorUserIdsAsync(
                        request.OrganizationId.Value, cancellationToken);

                    foreach (var coordinatorId in coordinatorIds)
                    {
                        await _notificationService.DispatchNotificationAsync(new DispatchNotificationRequest(
                            RecipientUserId: coordinatorId,
                            Category: NotificationCategory.HelpRequests,
                            Priority: NotificationPriority.Urgent,
                            PreferredChannel: NotificationChannel.Push,
                            Title: "Anfrage braucht Ihre Aufmerksamkeit",
                            Body: "Keine Freiwillige/r hat eine Anfrage angenommen, obwohl der Kreis mehrfach erweitert wurde.",
                            PayloadJson: $"{{\"helpRequestId\":\"{request.Id}\"}}"), cancellationToken);
                    }
                }

                continue;
            }

            var candidatesResult = await FindCandidatesAsync(request.Id, config: null, cancellationToken);
            if (candidatesResult.IsFailure)
            {
                continue;
            }

            var eligible = candidatesResult.Value!.Where(c => c.IsEligible).ToList();
            var nextTier = request.OfferTier + 1;
            var take = TierCandidateCount(nextTier);
            var batch = eligible.Take(take).Select(c => c.VolunteerUserId).ToList();

            var advanceResult = request.AdvanceOfferTier(batch);
            if (advanceResult.IsFailure)
            {
                continue;
            }

            actedOn++;

            foreach (var volunteerId in batch)
            {
                await _notificationService.DispatchNotificationAsync(new DispatchNotificationRequest(
                    RecipientUserId: volunteerId,
                    Category: NotificationCategory.HelpRequests,
                    Priority: NotificationPriority.Normal,
                    PreferredChannel: NotificationChannel.Push,
                    Title: "Neue Anfrage in Ihrer Nähe",
                    Body: "Es gibt eine offene Anfrage, die zu Ihnen passen könnte."), cancellationToken);
            }
        }

        if (actedOn > 0)
        {
            await _helpDb.SaveChangesAsync(cancellationToken);
        }

        return actedOn;
    }

    private static int TierCandidateCount(int tier) => tier switch
    {
        1 => 3,
        2 => 7,
        _ => int.MaxValue
    };

    public async Task<int> DispatchDueAssignmentRemindersAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var dueFor24h = await _helpDb.HelpRequests
            .Where(r => !r.IsDeleted
                     && r.Status == HelpRequestStatus.Assigned
                     && r.Reminder24hSentAtUtc == null
                     && r.ScheduledStartUtc <= now.AddHours(24))
            .ToListAsync(cancellationToken);

        var dueFor2h = await _helpDb.HelpRequests
            .Where(r => !r.IsDeleted
                     && r.Status == HelpRequestStatus.Assigned
                     && r.Reminder2hSentAtUtc == null
                     && r.ScheduledStartUtc <= now.AddHours(2))
            .ToListAsync(cancellationToken);

        var sent = 0;

        foreach (var request in dueFor24h)
        {
            if (!request.AssignedVolunteerUserId.HasValue)
            {
                continue;
            }

            if (request.MarkReminder24hSent().IsFailure)
            {
                continue;
            }

            await _notificationService.DispatchNotificationAsync(new DispatchNotificationRequest(
                RecipientUserId: request.AssignedVolunteerUserId.Value,
                Category: NotificationCategory.HelpRequests,
                Priority: NotificationPriority.Normal,
                PreferredChannel: NotificationChannel.Push,
                Title: "Erinnerung: morgen",
                Body: "Ihre Zusage beginnt in etwa 24 Stunden."), cancellationToken);

            sent++;
        }

        foreach (var request in dueFor2h)
        {
            if (!request.AssignedVolunteerUserId.HasValue)
            {
                continue;
            }

            if (request.MarkReminder2hSent().IsFailure)
            {
                continue;
            }

            await _notificationService.DispatchNotificationAsync(new DispatchNotificationRequest(
                RecipientUserId: request.AssignedVolunteerUserId.Value,
                Category: NotificationCategory.HelpRequests,
                Priority: NotificationPriority.Urgent,
                PreferredChannel: NotificationChannel.Push,
                Title: "Erinnerung: bald",
                Body: "Ihre Zusage beginnt in etwa 2 Stunden. Ich komme / Ich schaffe es nicht?"), cancellationToken);

            sent++;
        }

        if (sent > 0)
        {
            await _helpDb.SaveChangesAsync(cancellationToken);
        }

        return sent;
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
