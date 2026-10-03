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
using SeniorConnect.Modules.Profiles.Contracts;
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
    private readonly IOrganizationAccessGuard? _accessGuard;
    private readonly IDiscoveryEligibilityService? _eligibility;
    private readonly ILocationVisibilityService? _locationVisibility;
    private readonly TimeProvider _time;

    // Scoring prior for a volunteer or request without a stored location. It
    // only ranks; it is never reported as a distance.
    private const double UnknownDistanceScoringPriorKm = 5.0;

    public MatchingService(
        IHelpRequestsDbContext helpDb,
        IProfilesDbContext profileDb,
        ITrustLevelReader trustLevelReader,
        ISafetyBoundaryReader safetyBoundaryReader,
        IOptions<MatchingConfig> configuredWeights,
        INotificationService notificationService,
        IOrganizationCoordinatorReader coordinatorReader,
        TimeProvider? timeProvider = null,
        IOrganizationAccessGuard? accessGuard = null,
        IDiscoveryEligibilityService? eligibility = null,
        ILocationVisibilityService? locationVisibility = null)
    {
        _helpDb = helpDb;
        _profileDb = profileDb;
        _trustLevelReader = trustLevelReader;
        _safetyBoundaryReader = safetyBoundaryReader;
        _configuredWeights = configuredWeights;
        _notificationService = notificationService;
        _coordinatorReader = coordinatorReader;
        _time = timeProvider ?? TimeProvider.System;
        _accessGuard = accessGuard;
        // Phase 4: registered in production. ponytail: legacy unit tests run
        // without it and then see the pre-Phase-4 candidate set.
        _eligibility = eligibility;
        // Phase 4 sweep: the feed showed every senior's postal code and city.
        // Now the central policy decides; without it nobody has consented.
        _locationVisibility = locationVisibility;
    }

    public async Task<Result<IReadOnlyList<MatchingCandidate>>> GetCandidatesForCallerAsync(
        Guid helpRequestId,
        Guid callerUserId,
        CancellationToken cancellationToken = default)
    {
        var access = await EnsureCallerMayMatchAsync(helpRequestId, callerUserId, cancellationToken);
        return access.IsFailure ? access.Error! : await FindCandidatesAsync(helpRequestId, config: null, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<HybridMatchingProposal>>> GetHybridProposalsForCallerAsync(
        HybridMatchingRequest request,
        Guid callerUserId,
        CancellationToken cancellationToken = default)
    {
        var access = await EnsureCallerMayMatchAsync(request.HelpRequestId, callerUserId, cancellationToken);
        return access.IsFailure ? access.Error! : await GetHybridProposalsAsync(request, cancellationToken);
    }

    /// <summary>
    /// Hotfix authorization rule for every human-facing matching view. The
    /// tenant comes from the request row itself, never from the caller. The
    /// senior/creator is deliberately NOT allowed: the list holds volunteer
    /// identities and reliability scores (no public ratings of people).
    /// Community requests (no organization) are matched only by the automatic
    /// tier dispatcher, so no human sees their candidate list. Fails closed
    /// when no guard is wired.
    /// </summary>
    private async Task<Result> EnsureCallerMayMatchAsync(Guid helpRequestId, Guid callerUserId, CancellationToken cancellationToken)
    {
        var row = await _helpDb.HelpRequests
            .AsNoTracking()
            .Where(r => r.Id == helpRequestId && !r.IsDeleted)
            .Select(r => new { r.OrganizationId })
            .FirstOrDefaultAsync(cancellationToken);

        if (row?.OrganizationId is not { } organizationId || _accessGuard is null)
        {
            return Error.NotFound("HelpRequest");
        }

        var staff = await _accessGuard.EnsureStaffAsync(callerUserId, organizationId, cancellationToken);
        if (staff.IsSuccess)
        {
            return Result.Success();
        }

        // Not a member of the request's org: the request is invisible, not forbidden.
        return staff.Error!.Kind == ErrorKind.NotFound ? Error.NotFound("HelpRequest") : staff;
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

        // Phase 4: who may be a candidate at all (discoverable + verified, or
        // on the request organization's own roster; active; no block; no
        // safeguarding discovery/assignment restriction). Replaces the
        // "every accepting volunteer on the platform" pool (hotfix residual 2).
        if (_eligibility is not null)
        {
            var eligible = await _eligibility.FilterEligibleAsync(
                request.SeniorUserId, volunteerUserIds,
                new DiscoveryContext(DiscoveryPurpose.Matching, request.OrganizationId), cancellationToken);
            volunteers = volunteers.Where(v => eligible.Contains(v.UserId)).ToList();
            volunteerUserIds = volunteers.Select(v => v.UserId).ToList();
        }

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

            // Hotfix + Phase 3: BOTH homes (the volunteer's and the senior's)
            // are reduced to their ~1 km grid cell before any distance is
            // computed. Staff see these candidates and may see at most the
            // senior's grid cell (LocationVisibilityPolicy: OrganizationStaff
            // = APPROXIMATE), so no score, band or eligibility flip may be
            // derived from anything finer.
            double distanceKm = UnknownDistanceScoringPriorKm;
            string? distanceBand = null;
            if (v.Latitude.HasValue && v.Longitude.HasValue && request.Latitude.HasValue && request.Longitude.HasValue)
            {
                distanceKm = LocationPrivacy.HaversineKm(
                    LocationPrivacy.SnapToGrid(v.Latitude.Value), LocationPrivacy.SnapToGrid(v.Longitude.Value),
                    LocationPrivacy.SnapToGrid(request.Latitude.Value), LocationPrivacy.SnapToGrid(request.Longitude.Value));
                distanceBand = DistanceBand.FromKm(distanceKm);
                if (distanceKm > v.MaxDistanceKm)
                {
                    ineligibility.Add("Distance exceeds the volunteer's maximum travel distance");
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
                : string.Format(CultureInfo.InvariantCulture, "Nearby volunteer ({0}), reliability {1:P0}", distanceBand ?? "distance unknown", reliability);

            var breakdown = new ScoreBreakdown(
                DistanceScore: distanceScore,
                ContinuityScore: continuityScore,
                ReliabilityScore: reliabilityScore,
                AvailabilityScore: availabilityScore,
                Explanation: explanation);

            candidates.Add(new MatchingCandidate(
                VolunteerUserId: v.UserId,
                TotalScore: Math.Round(totalScore, 3),
                DistanceBand: distanceBand,
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
        double radiusKm = 20,
        CancellationToken cancellationToken = default)
    {
        // P4-06 & P4-16: Query volunteer's effective trust level & block list
        var volunteerTrustLevel = await _trustLevelReader.GetEffectiveTrustLevelAsync(volunteerUserId, cancellationToken);

        // Hotfix: the origin is the volunteer's own stored location, never a
        // client-supplied point. No stored location = no distance at all
        // (DistanceBand null), not a made-up one.
        var origin = await _profileDb.VolunteerProfiles
            .AsNoTracking()
            .Where(v => v.UserId == volunteerUserId && v.Latitude != null && v.Longitude != null)
            .Select(v => new { Latitude = v.Latitude!.Value, Longitude = v.Longitude!.Value })
            .FirstOrDefaultAsync(cancellationToken);

        var openRequests = await _helpDb.HelpRequests
            .Where(r => (r.Status == HelpRequestStatus.Open || r.Status == HelpRequestStatus.Offered || r.Status == HelpRequestStatus.Matching) 
                     && !r.IsDeleted 
                     && r.SeniorUserId != volunteerUserId)
            .OrderBy(r => r.ScheduledStartUtc)
            .ToListAsync(cancellationToken);

        var requestSeniorIds = openRequests.Select(r => r.SeniorUserId).Distinct().ToList();
        var blockedSeniorIds = await _safetyBoundaryReader.GetBlockedUserIdsAsync(volunteerUserId, requestSeniorIds, cancellationToken);
        openRequests = openRequests.Where(r => !blockedSeniorIds.Contains(r.SeniorUserId)).ToList();
        if (_eligibility is not null)
        {
            // Phase 4: safeguarding discovery restrictions, on top of blocks.
            var visible = await _eligibility.FilterEligibleAsync(
                volunteerUserId, requestSeniorIds, new DiscoveryContext(DiscoveryPurpose.OwnedContent), cancellationToken);
            openRequests = openRequests.Where(r => visible.Contains(r.SeniorUserId)).ToList();
        }

        var categories = await _helpDb.ActivityCategories
            .ToDictionaryAsync(c => c.Id, c => c.NameKey, cancellationToken);

        var feedOwners = openRequests.Select(r => r.SeniorUserId).Distinct().ToList();
        var disclosures = _locationVisibility is null
            ? feedOwners.ToDictionary(id => id, _ => LocationDisclosure.DistanceOnly)
            : await _locationVisibility.DecideManyAsync(feedOwners, volunteerUserId, LocationRelationship.DiscoveryViewer, cancellationToken);

        var items = new List<VolunteerFeedItem>();

        foreach (var r in openRequests)
        {
            // The request point (usually a senior's home) is reduced to its
            // ~1 km grid cell before the distance is measured.
            string? distanceBand = null;
            if (origin is not null && r.Latitude.HasValue && r.Longitude.HasValue)
            {
                distanceBand = DistanceBand.FromKm(LocationPrivacy.HaversineKm(
                    origin.Latitude, origin.Longitude,
                    LocationPrivacy.SnapToGrid(r.Latitude.Value), LocationPrivacy.SnapToGrid(r.Longitude.Value)));
            }

            var categoryName = categories.TryGetValue(r.CategoryId, out var name) ? name : "activity.general";

            // P4-06: Check trust level eligibility
            var isTrustEligible = volunteerTrustLevel >= r.RequiredTrustLevel;
            // Unknown distance does not exclude. The radius filter is only as
            // sharp as the bands, so it cannot be binary-searched.
            var isDistEligible = distanceBand is null || DistanceBand.IsWithin(distanceBand, radiusKm);
            var isEligible = isTrustEligible && isDistEligible;

            string? ineligibilityReason = null;
            if (!isTrustEligible)
            {
                ineligibilityReason = string.Format(CultureInfo.InvariantCulture, "Erfordert Vertrauensstufe {0} (Ihr aktueller Status: Stufe {1}). Durch Bestätigung von Identität oder Qualifikationen in Ihrem Profil können Sie diese Stufe erreichen.", r.RequiredTrustLevel, volunteerTrustLevel);
            }
            else if (!isDistEligible)
            {
                ineligibilityReason = "Außerhalb Ihres bevorzugten Umkreises.";
            }

            items.Add(new VolunteerFeedItem(
                HelpRequestId: r.Id,
                CategoryNameKey: categoryName,
                RequiredSafetyLevel: r.RequiredSafetyLevel,
                ScheduledStartUtc: r.ScheduledStartUtc,
                DurationMinutes: r.DurationMinutes,
                LocationCity: disclosures[r.SeniorUserId].ShowsArea() ? r.LocationCity ?? "" : "",
                LocationPostalCode: disclosures[r.SeniorUserId].ShowsArea() ? r.LocationPostalCode : null,
                DistanceBand: distanceBand,
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
                : string.Format(CultureInfo.InvariantCulture, "Optimal proximity ({0}) and active reliability. Predicted completion: {1:P0}", candidate.DistanceBand ?? "distance unknown", completionProbability);

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
        var now = _time.GetUtcNow();
        var cutoff = now - TierEscalationInterval;

        // P3-13: picks up (a) tiers that sat stale for the interval, (b) legacy
        // rows created before the tier clock was set on creation (null), and
        // (c) fresh tier-1 requests whose first 3 candidates were never notified.
        var sweepRequests = await _helpDb.HelpRequests
            .Where(r => !r.IsDeleted
                     && (r.Status == HelpRequestStatus.Open || r.Status == HelpRequestStatus.Matching || r.Status == HelpRequestStatus.Offered)
                     && r.EscalatedToCoordinatorAtUtc == null
                     && (r.TierAdvancedAtUtc == null
                         || r.TierAdvancedAtUtc <= cutoff
                         || (r.OfferTier == 1 && r.OfferedVolunteersJson == "[]")))
            .ToListAsync(cancellationToken);

        var actedOn = 0;

        foreach (var request in sweepRequests)
        {
            var isStale = request.TierAdvancedAtUtc is null || request.TierAdvancedAtUtc <= cutoff;

            if (!isStale)
            {
                // Tier 1 dispatch: first 3 eligible candidates.
                var firstBatch = await NextOfferBatchAsync(request, tier: 1, cancellationToken);
                if (firstBatch.Count == 0 || request.RecordInitialOffers(firstBatch).IsFailure)
                {
                    continue;
                }

                actedOn++;
                await NotifyVolunteersOfOfferAsync(firstBatch, cancellationToken);
                continue;
            }

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

            var batch = await NextOfferBatchAsync(request, request.OfferTier + 1, cancellationToken);

            var advanceResult = request.AdvanceOfferTier(batch);
            if (advanceResult.IsFailure)
            {
                continue;
            }

            actedOn++;
            await NotifyVolunteersOfOfferAsync(batch, cancellationToken);
        }

        if (actedOn > 0)
        {
            await _helpDb.SaveChangesAsync(cancellationToken);
        }

        return actedOn;
    }

    /// <summary>
    /// The top-N eligible candidates for <paramref name="tier"/>, minus anyone
    /// already offered this request in an earlier tier — nobody is pinged twice.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> NextOfferBatchAsync(HelpRequest request, int tier, CancellationToken cancellationToken)
    {
        var candidatesResult = await FindCandidatesAsync(request.Id, config: null, cancellationToken);
        if (candidatesResult.IsFailure)
        {
            return [];
        }

        var alreadyOffered = request.GetOfferedVolunteerIds();
        return candidatesResult.Value!
            .Where(c => c.IsEligible)
            .Take(TierCandidateCount(tier))
            .Select(c => c.VolunteerUserId)
            .Where(id => !alreadyOffered.Contains(id))
            .ToList();
    }

    private async Task NotifyVolunteersOfOfferAsync(IReadOnlyList<Guid> volunteerIds, CancellationToken cancellationToken)
    {
        foreach (var volunteerId in volunteerIds)
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
}
