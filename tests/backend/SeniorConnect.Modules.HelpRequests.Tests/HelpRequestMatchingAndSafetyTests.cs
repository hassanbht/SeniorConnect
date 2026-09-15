using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.HelpRequests.Application;
using SeniorConnect.Modules.HelpRequests.Domain;
using SeniorConnect.Modules.HelpRequests.Infrastructure;
using SeniorConnect.Modules.Identity.Domain;
using SeniorConnect.Modules.Matching.Application;
using SeniorConnect.Modules.Matching.Domain;
using SeniorConnect.Modules.Matching.Infrastructure;
using SeniorConnect.Modules.Profiles.Domain;
using SeniorConnect.Modules.TrustSafety.Domain;
using Xunit;

namespace SeniorConnect.Modules.HelpRequests.Tests;

public sealed class HelpRequestMatchingAndSafetyTests
{
    private sealed class TestTenantContext : ITenantContext
    {
        public Guid? OrganizationId => null;
        public bool IsPlatformScope => true;
    }

    private static SeniorConnectDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SeniorConnectDbContext(options, new TestTenantContext());
    }

    [Fact]
    public async Task AcceptHelpRequest_WhenVolunteerTrustLevelIsInsufficient_ReturnsForbidden()
    {
        using var db = CreateInMemoryDb();
        var seniorId = Guid.NewGuid();
        var volunteerId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var request = HelpRequest.Create(
            organizationId: null,
            seniorUserId: seniorId,
            createdByUserId: seniorId,
            categoryId: categoryId,
            safetyLevel: 3,
            trustLevel: 3,
            scheduledStartUtc: now.AddHours(2),
            scheduledEndUtc: now.AddHours(3),
            durationMinutes: 60,
            locationType: LocationType.SeniorHome).Value!;

        db.HelpRequests.Add(request);

        // Volunteer has only Trust Level 1
        var snapshot = TrustLevelSnapshot.Create(volunteerId, 1, "{\"reason\":\"basic_phone\"}");
        db.TrustLevelSnapshots.Add(snapshot);
        await db.SaveChangesAsync();

        var trustReader = new SeniorConnect.Modules.Identity.Infrastructure.TrustLevelReader(db);
        var safetyReader = new SeniorConnect.Modules.TrustSafety.Infrastructure.SafetyBoundaryReader(db);
        var service = new HelpRequestService(db, new ActivitySafetyPolicy(), trustReader, safetyReader, new SeniorConnect.Modules.Identity.Infrastructure.UserContactReader(db), new SeniorConnect.Modules.Profiles.Infrastructure.VolunteerReliabilityUpdater(db));

        var result = await service.AcceptHelpRequestAsync(
            request.Id,
            volunteerId,
            new AcceptHelpRequestRequest(ExpectedRowVersion: 1));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("TRUST_LEVEL_INSUFFICIENT");
        result.Error.Kind.Should().Be(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task AcceptHelpRequest_WhenBuddyIsRequiredForLevel3_ReturnsForbidden()
    {
        using var db = CreateInMemoryDb();
        var seniorId = Guid.NewGuid();
        var volunteerId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var request = HelpRequest.Create(
            organizationId: null,
            seniorUserId: seniorId,
            createdByUserId: seniorId,
            categoryId: categoryId,
            safetyLevel: 3,
            trustLevel: 3,
            scheduledStartUtc: now.AddHours(2),
            scheduledEndUtc: now.AddHours(3),
            durationMinutes: 60,
            locationType: LocationType.SeniorHome).Value!;

        db.HelpRequests.Add(request);

        // Volunteer has Trust Level 3 but is new (0 level-3 completed, no buddy assigned)
        var snapshot = TrustLevelSnapshot.Create(volunteerId, 3, "{\"reason\":\"police_record_check\"}");
        db.TrustLevelSnapshots.Add(snapshot);
        await db.SaveChangesAsync();

        var trustReader2 = new SeniorConnect.Modules.Identity.Infrastructure.TrustLevelReader(db);
        var safetyReader2 = new SeniorConnect.Modules.TrustSafety.Infrastructure.SafetyBoundaryReader(db);
        var service = new HelpRequestService(db, new ActivitySafetyPolicy(), trustReader2, safetyReader2, new SeniorConnect.Modules.Identity.Infrastructure.UserContactReader(db), new SeniorConnect.Modules.Profiles.Infrastructure.VolunteerReliabilityUpdater(db));

        var result = await service.AcceptHelpRequestAsync(
            request.Id,
            volunteerId,
            new AcceptHelpRequestRequest(ExpectedRowVersion: 1));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("BUDDY_REQUIRED");
        result.Error.Kind.Should().Be(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task FindCandidates_ExcludesBuddyRequiredVolunteer_FromSafetyLevel3PlusRequest()
    {
        using var db = CreateInMemoryDb();
        var seniorId = Guid.NewGuid();
        var volunteerId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var request = HelpRequest.Create(
            organizationId: null,
            seniorUserId: seniorId,
            createdByUserId: seniorId,
            categoryId: categoryId,
            safetyLevel: 3,
            trustLevel: 3,
            scheduledStartUtc: now.AddHours(2),
            scheduledEndUtc: now.AddHours(3),
            durationMinutes: 60,
            locationType: LocationType.SeniorHome).Value!;
        db.HelpRequests.Add(request);

        // Trust level satisfied, but brand new — no buddy history yet.
        db.TrustLevelSnapshots.Add(TrustLevelSnapshot.Create(volunteerId, 3, "{}"));
        db.VolunteerProfiles.Add(SeniorConnect.Modules.Profiles.Domain.VolunteerProfile.Create(volunteerId));
        await db.SaveChangesAsync();

        var matchingService = new SeniorConnect.Modules.Matching.Infrastructure.MatchingService(
            db, db,
            new SeniorConnect.Modules.Identity.Infrastructure.TrustLevelReader(db),
            new SeniorConnect.Modules.TrustSafety.Infrastructure.SafetyBoundaryReader(db),
            Microsoft.Extensions.Options.Options.Create(new SeniorConnect.Modules.Matching.Domain.MatchingConfig()),
            new SeniorConnect.Modules.Notifications.Infrastructure.NotificationService(db),
            new SeniorConnect.Modules.Organizations.Infrastructure.OrganizationCoordinatorReader(db));

        var result = await matchingService.FindCandidatesAsync(request.Id);

        result.IsSuccess.Should().BeTrue();
        var candidate = result.Value!.Single(c => c.VolunteerUserId == volunteerId);
        candidate.IsEligible.Should().BeFalse("P4-07: a volunteer needing a buddy must never be offered a Safety Level 3+ request");
        candidate.IneligibilityReasons.Should().Contain(r => r.Contains("buddy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptHelpRequest_WhenUserIsBlocked_ReturnsForbidden()
    {
        using var db = CreateInMemoryDb();
        var seniorId = Guid.NewGuid();
        var volunteerId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var request = HelpRequest.Create(
            organizationId: null,
            seniorUserId: seniorId,
            createdByUserId: seniorId,
            categoryId: categoryId,
            safetyLevel: 1,
            trustLevel: 1,
            scheduledStartUtc: now.AddHours(2),
            scheduledEndUtc: now.AddHours(3),
            durationMinutes: 60,
            locationType: LocationType.PublicPlace).Value!;

        db.HelpRequests.Add(request);

        var snapshot = TrustLevelSnapshot.Create(volunteerId, 2, "{}");
        db.TrustLevelSnapshots.Add(snapshot);

        // Senior has blocked volunteer
        var block = UserBlock.Create(seniorId, volunteerId, "Personal boundary preference");
        db.UserBlocks.Add(block);
        await db.SaveChangesAsync();

        var trustReader3 = new SeniorConnect.Modules.Identity.Infrastructure.TrustLevelReader(db);
        var safetyReader3 = new SeniorConnect.Modules.TrustSafety.Infrastructure.SafetyBoundaryReader(db);
        var service = new HelpRequestService(db, new ActivitySafetyPolicy(), trustReader3, safetyReader3, new SeniorConnect.Modules.Identity.Infrastructure.UserContactReader(db), new SeniorConnect.Modules.Profiles.Infrastructure.VolunteerReliabilityUpdater(db));

        var result = await service.AcceptHelpRequestAsync(
            request.Id,
            volunteerId,
            new AcceptHelpRequestRequest(ExpectedRowVersion: 1));

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task MatchingService_ExcludesBlockedVolunteers_AndFlagsLowTrust()
    {
        using var db = CreateInMemoryDb();
        var seniorId = Guid.NewGuid();
        var blockedVolId = Guid.NewGuid();
        var lowTrustVolId = Guid.NewGuid();
        var eligibleVolId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var request = HelpRequest.Create(
            organizationId: null,
            seniorUserId: seniorId,
            createdByUserId: seniorId,
            categoryId: categoryId,
            safetyLevel: 2,
            trustLevel: 2,
            scheduledStartUtc: now.AddHours(2),
            scheduledEndUtc: now.AddHours(3),
            durationMinutes: 60,
            locationType: LocationType.PublicPlace).Value!;

        db.HelpRequests.Add(request);

        var p1 = VolunteerProfile.Create(blockedVolId, "Blocked Vol");
        p1.UpdateStatus(true);
        var p2 = VolunteerProfile.Create(lowTrustVolId, "Low Trust Vol");
        p2.UpdateStatus(true);
        var p3 = VolunteerProfile.Create(eligibleVolId, "Eligible Vol");
        p3.UpdateStatus(true);

        db.VolunteerProfiles.AddRange(p1, p2, p3);

        // Block record
        db.UserBlocks.Add(UserBlock.Create(seniorId, blockedVolId, "Blocked"));

        // Trust levels: lowTrust = 1, eligible = 3
        db.TrustLevelSnapshots.Add(TrustLevelSnapshot.Create(lowTrustVolId, 1, "{}"));
        db.TrustLevelSnapshots.Add(TrustLevelSnapshot.Create(eligibleVolId, 3, "{}"));
        await db.SaveChangesAsync();

        var trustReader4 = new SeniorConnect.Modules.Identity.Infrastructure.TrustLevelReader(db);
        var safetyReader4 = new SeniorConnect.Modules.TrustSafety.Infrastructure.SafetyBoundaryReader(db);
        var matchingService = new MatchingService(db, db, trustReader4, safetyReader4, Microsoft.Extensions.Options.Options.Create(new MatchingConfig()),
            new SeniorConnect.Modules.Notifications.Infrastructure.NotificationService(db),
            new SeniorConnect.Modules.Organizations.Infrastructure.OrganizationCoordinatorReader(db));

        var candidatesResult = await matchingService.FindCandidatesAsync(request.Id);

        candidatesResult.IsSuccess.Should().BeTrue();
        var candidates = candidatesResult.Value!;

        // Blocked volunteer must not appear at all
        candidates.Should().NotContain(c => c.VolunteerUserId == blockedVolId);

        // Low trust volunteer is marked ineligible
        var lowTrustCandidate = candidates.FirstOrDefault(c => c.VolunteerUserId == lowTrustVolId);
        lowTrustCandidate.Should().NotBeNull();
        lowTrustCandidate!.IsEligible.Should().BeFalse();
        lowTrustCandidate.IneligibilityReasons.Should().Contain(r => r.Contains("below required level"));

        // Eligible volunteer is marked eligible
        var eligibleCandidate = candidates.FirstOrDefault(c => c.VolunteerUserId == eligibleVolId);
        eligibleCandidate.Should().NotBeNull();
        eligibleCandidate!.IsEligible.Should().BeTrue();
    }

    [Fact]
    public async Task MatchingService_UsesInjectedWeights_NotHardcodedDefaults()
    {
        // P3-09 / Gate 3 item 6: matching weights must be configurable
        // without a code change. This proves MatchingService actually reads
        // the injected IOptions<MatchingConfig> value — the same mechanism
        // AddMatchingModule binds from appsettings.json "Matching:Weights" —
        // rather than silently using `new MatchingConfig()`.
        using var db = CreateInMemoryDb();
        var seniorId = Guid.NewGuid();
        var volunteerId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var request = HelpRequest.Create(
            organizationId: null,
            seniorUserId: seniorId,
            createdByUserId: seniorId,
            categoryId: categoryId,
            safetyLevel: 1,
            trustLevel: 1,
            scheduledStartUtc: now.AddHours(2),
            scheduledEndUtc: now.AddHours(3),
            durationMinutes: 60,
            locationType: LocationType.PublicPlace).Value!;

        db.HelpRequests.Add(request);

        var volunteer = VolunteerProfile.Create(volunteerId, "Weighted Vol");
        volunteer.UpdateStatus(true);
        db.VolunteerProfiles.Add(volunteer);
        await db.SaveChangesAsync();

        var trustReader = new SeniorConnect.Modules.Identity.Infrastructure.TrustLevelReader(db);
        var safetyReader = new SeniorConnect.Modules.TrustSafety.Infrastructure.SafetyBoundaryReader(db);

        // No coordinates set on either side, so distanceKm defaults to 5.0
        // and distanceScore = 1 - 5/20 = 0.75 for every weight combination
        // below — only the weight applied to it should change the total.
        var distanceOnlyConfig = new MatchingConfig(
            DistanceWeight: 1.0, ContinuityWeight: 0, ReliabilityWeight: 0, AvailabilityWeight: 0);
        var reliabilityOnlyConfig = new MatchingConfig(
            DistanceWeight: 0, ContinuityWeight: 0, ReliabilityWeight: 1.0, AvailabilityWeight: 0,
            ColdStartReliability: 0.42);

        var notificationService = new SeniorConnect.Modules.Notifications.Infrastructure.NotificationService(db);
        var coordinatorReader = new SeniorConnect.Modules.Organizations.Infrastructure.OrganizationCoordinatorReader(db);

        var distanceWeightedService = new MatchingService(
            db, db, trustReader, safetyReader, Microsoft.Extensions.Options.Options.Create(distanceOnlyConfig),
            notificationService, coordinatorReader);
        var reliabilityWeightedService = new MatchingService(
            db, db, trustReader, safetyReader, Microsoft.Extensions.Options.Options.Create(reliabilityOnlyConfig),
            notificationService, coordinatorReader);

        var distanceResult = await distanceWeightedService.FindCandidatesAsync(request.Id);
        var reliabilityResult = await reliabilityWeightedService.FindCandidatesAsync(request.Id);

        distanceResult.IsSuccess.Should().BeTrue();
        reliabilityResult.IsSuccess.Should().BeTrue();

        var distanceCandidate = distanceResult.Value!.Single();
        var reliabilityCandidate = reliabilityResult.Value!.Single();

        // Same volunteer, same request — different injected config must
        // produce a different total score, proving the weights are live,
        // not compiled in.
        distanceCandidate.TotalScore.Should().Be(0.75);
        reliabilityCandidate.TotalScore.Should().Be(0.42);
        distanceCandidate.TotalScore.Should().NotBe(reliabilityCandidate.TotalScore);
    }

    [Fact]
    public async Task GetHelpRequestById_RevealsSeniorContact_OnlyToAuthorizedCounterparty()
    {
        // BR-COMM-04: the senior's name/phone are revealed only to the
        // senior, the creator, or the assigned volunteer — never to anyone
        // else who happens to know the request id.
        using var db = CreateInMemoryDb();
        var seniorId = Guid.NewGuid();
        var volunteerId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var seniorUser = SeniorConnect.Modules.Identity.Domain.User.CreateWithPhone(
            "+43 664 9999999", "Elisabeth Huber");
        typeof(SeniorConnect.Domain.Entity).GetProperty(nameof(SeniorConnect.Domain.Entity.Id))!
            .SetValue(seniorUser, seniorId);
        db.Users.Add(seniorUser);

        var request = HelpRequest.Create(
            organizationId: null,
            seniorUserId: seniorId,
            createdByUserId: seniorId,
            categoryId: categoryId,
            safetyLevel: 1,
            trustLevel: 1,
            scheduledStartUtc: now.AddHours(2),
            scheduledEndUtc: now.AddHours(3),
            durationMinutes: 60,
            locationType: LocationType.PublicPlace).Value!;
        request.Publish();
        request.Assign(volunteerId, request.RowVersion);
        db.HelpRequests.Add(request);
        await db.SaveChangesAsync();

        var service = new HelpRequestService(
            db, new ActivitySafetyPolicy(),
            new SeniorConnect.Modules.Identity.Infrastructure.TrustLevelReader(db),
            new SeniorConnect.Modules.TrustSafety.Infrastructure.SafetyBoundaryReader(db),
            new SeniorConnect.Modules.Identity.Infrastructure.UserContactReader(db), new SeniorConnect.Modules.Profiles.Infrastructure.VolunteerReliabilityUpdater(db));

        var asVolunteer = await service.GetHelpRequestByIdAsync(request.Id, volunteerId);
        asVolunteer.Value!.SeniorDisplayName.Should().Be("Elisabeth Huber");
        asVolunteer.Value!.SeniorPhone.Should().Be("+43 664 9999999");

        var asStranger = await service.GetHelpRequestByIdAsync(request.Id, strangerId);
        asStranger.Value!.SeniorDisplayName.Should().BeNull();
        asStranger.Value!.SeniorPhone.Should().BeNull();
    }

    [Fact]
    public async Task AdvanceStaleOffers_WithZeroEligibleCandidatesAtTier3_EscalatesToCoordinator()
    {
        // P3-13 / Gate 3 item 7: a request with no eligible volunteers must
        // advance 1 → 2 → 3 and then escalate to the org's coordinators —
        // never sit silently unmatched.
        using var db = CreateInMemoryDb();
        var seniorId = Guid.NewGuid();
        var coordinatorId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var org = SeniorConnect.Modules.Organizations.Domain.Organization.Create("Freiwilligenzentrum Test", SeniorConnect.Modules.Organizations.Domain.OrganizationType.Ngo);
        var organizationId = org.Id;
        db.Organizations.Add(org);

        var membership = SeniorConnect.Modules.Organizations.Domain.OrganizationMembership.Create(
            organizationId, coordinatorId, SeniorConnect.Modules.Organizations.Domain.MembershipRole.Coordinator);
        membership.Activate();
        db.OrganizationMemberships.Add(membership);

        var request = HelpRequest.Create(
            organizationId: organizationId,
            seniorUserId: seniorId,
            createdByUserId: seniorId,
            categoryId: categoryId,
            safetyLevel: 1,
            trustLevel: 1,
            scheduledStartUtc: now.AddHours(5),
            scheduledEndUtc: now.AddHours(6),
            durationMinutes: 60,
            locationType: LocationType.PublicPlace).Value!;
        request.Publish();
        db.HelpRequests.Add(request);
        await db.SaveChangesAsync();

        var trustReader = new SeniorConnect.Modules.Identity.Infrastructure.TrustLevelReader(db);
        var safetyReader = new SeniorConnect.Modules.TrustSafety.Infrastructure.SafetyBoundaryReader(db);
        var notificationService = new SeniorConnect.Modules.Notifications.Infrastructure.NotificationService(db);
        var coordinatorReader = new SeniorConnect.Modules.Organizations.Infrastructure.OrganizationCoordinatorReader(db);
        var matchingService = new MatchingService(
            db, db, trustReader, safetyReader, Microsoft.Extensions.Options.Options.Create(new MatchingConfig()),
            notificationService, coordinatorReader);

        // Force the tier clock into the past so every call below is "stale"
        // immediately, instead of waiting 15 real minutes per tier.
        void MakeStale()
        {
            db.ChangeTracker.Clear();
            var stored = db.HelpRequests.Single(r => r.Id == request.Id);
            typeof(HelpRequest).GetProperty(nameof(HelpRequest.TierAdvancedAtUtc))!
                .SetValue(stored, now.AddMinutes(-30));
            db.SaveChanges();
        }

        MakeStale();
        var advanced1 = await matchingService.AdvanceStaleOffersAsync(); // tier 1 -> 2
        advanced1.Should().Be(1);

        MakeStale();
        var advanced2 = await matchingService.AdvanceStaleOffersAsync(); // tier 2 -> 3
        advanced2.Should().Be(1);

        MakeStale();
        var advanced3 = await matchingService.AdvanceStaleOffersAsync(); // tier 3 stale -> escalate
        advanced3.Should().Be(1);

        var finalState = await db.HelpRequests.SingleAsync(r => r.Id == request.Id);
        finalState.OfferTier.Should().Be(3);
        finalState.EscalatedToCoordinatorAtUtc.Should().NotBeNull();

        var coordinatorNotifications = await db.NotificationMessages
            .Where(m => m.RecipientUserId == coordinatorId)
            .ToListAsync();
        coordinatorNotifications.Should().ContainSingle();
        coordinatorNotifications[0].Priority.Should().Be(SeniorConnect.Modules.Notifications.Domain.NotificationPriority.Urgent);

        // Escalating twice must not happen.
        MakeStale();
        var advanced4 = await matchingService.AdvanceStaleOffersAsync();
        advanced4.Should().Be(0);
    }

    [Fact]
    public async Task DispatchDueAssignmentReminders_SendsExactlyTwoPerAssignment_NeverMore()
    {
        // P3-18 / BR-NOTIFY-01: at most 2 pushes per assignment (T-24h, T-2h).
        using var db = CreateInMemoryDb();
        var seniorId = Guid.NewGuid();
        var volunteerId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var request = HelpRequest.Create(
            organizationId: null,
            seniorUserId: seniorId,
            createdByUserId: seniorId,
            categoryId: categoryId,
            safetyLevel: 1,
            trustLevel: 1,
            scheduledStartUtc: now.AddHours(20), // inside the 24h window, outside the 2h window
            scheduledEndUtc: now.AddHours(21),
            durationMinutes: 60,
            locationType: LocationType.PublicPlace).Value!;
        request.Publish();
        request.Assign(volunteerId, request.RowVersion);
        db.HelpRequests.Add(request);
        await db.SaveChangesAsync();

        var trustReader = new SeniorConnect.Modules.Identity.Infrastructure.TrustLevelReader(db);
        var safetyReader = new SeniorConnect.Modules.TrustSafety.Infrastructure.SafetyBoundaryReader(db);
        var notificationService = new SeniorConnect.Modules.Notifications.Infrastructure.NotificationService(db);
        var coordinatorReader = new SeniorConnect.Modules.Organizations.Infrastructure.OrganizationCoordinatorReader(db);
        var matchingService = new MatchingService(
            db, db, trustReader, safetyReader, Microsoft.Extensions.Options.Options.Create(new MatchingConfig()),
            notificationService, coordinatorReader);

        var firstRun = await matchingService.DispatchDueAssignmentRemindersAsync();
        firstRun.Should().Be(1); // only the 24h reminder is due so far

        var secondRunSamePoll = await matchingService.DispatchDueAssignmentRemindersAsync();
        secondRunSamePoll.Should().Be(0); // idempotent — already sent

        // Move the clock: now within the 2h window too.
        db.ChangeTracker.Clear();
        var stored = db.HelpRequests.Single(r => r.Id == request.Id);
        typeof(HelpRequest).GetProperty(nameof(HelpRequest.ScheduledStartUtc))!
            .SetValue(stored, DateTimeOffset.UtcNow.AddHours(1));
        db.SaveChanges();

        var thirdRun = await matchingService.DispatchDueAssignmentRemindersAsync();
        thirdRun.Should().Be(1); // the 2h reminder fires now

        var fourthRun = await matchingService.DispatchDueAssignmentRemindersAsync();
        fourthRun.Should().Be(0);

        var allNotifications = await db.NotificationMessages
            .Where(m => m.RecipientUserId == volunteerId)
            .ToListAsync();
        allNotifications.Should().HaveCount(2);
    }
}
