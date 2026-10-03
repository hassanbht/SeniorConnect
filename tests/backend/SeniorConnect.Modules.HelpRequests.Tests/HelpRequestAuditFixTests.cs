using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.HelpRequests.Application;
using SeniorConnect.Modules.HelpRequests.Domain;
using SeniorConnect.Modules.HelpRequests.Infrastructure;
using SeniorConnect.Modules.Identity.Infrastructure;
using SeniorConnect.Modules.Matching.Domain;
using SeniorConnect.Modules.Matching.Infrastructure;
using SeniorConnect.Modules.Organizations.Domain;
using SeniorConnect.Modules.Organizations.Infrastructure;
using SeniorConnect.Modules.Profiles.Contracts;
using SeniorConnect.Modules.Profiles.Domain;
using SeniorConnect.Modules.Identity.Domain;
using SeniorConnect.Modules.TrustSafety.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.HelpRequests.Tests;

/// <summary>
/// Regression tests for the Phase-3 HelpRequests audit fixes
/// (P3-01, P3-02, P3-04, P3-13, P3-17, P3-19, P3-20, P3-21, party authorization).
/// </summary>
public sealed class HelpRequestAuditFixTests
{
    private sealed class TestTenantContext : ITenantContext
    {
        public Guid? OrganizationId => null;
        public bool IsPlatformScope => true;
    }

    private sealed class CountingReliabilityUpdater : IVolunteerReliabilityUpdater
    {
        public int ReliableCalls { get; private set; }
        public int UnreliableCalls { get; private set; }
        public int RestoreCalls { get; private set; }

        public Task<decimal?> RecordOutcomeAsync(Guid volunteerUserId, bool wasReliable, CancellationToken cancellationToken = default)
        {
            if (wasReliable) ReliableCalls++; else UnreliableCalls++;
            return Task.FromResult<decimal?>(0.8m);
        }

        public Task RestoreScoreAsync(Guid volunteerUserId, decimal? score, CancellationToken cancellationToken = default)
        {
            RestoreCalls++;
            return Task.CompletedTask;
        }
    }

    private readonly Guid _seniorId = Guid.NewGuid();
    private readonly Guid _volunteerId = Guid.NewGuid();
    private readonly Guid _strangerId = Guid.NewGuid();

    private static SeniorConnectDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SeniorConnectDbContext(options, new TestTenantContext());
    }

    private static HelpRequestService CreateService(SeniorConnectDbContext db, IVolunteerReliabilityUpdater? reliability = null) => new(
        db,
        new ActivitySafetyPolicy(),
        new TrustLevelReader(db),
        new SafetyBoundaryReader(db),
        new UserContactReader(db),
        reliability ?? new CountingReliabilityUpdater(),
        familyPermissionReader: null,
        accessGuard: new OrganizationAccessGuard(db));

    private static ActivityCategory CreateCategory(string code, int safetyLevel = 1, bool isBlocked = false, string? referralGroup = null)
    {
        var category = (ActivityCategory)Activator.CreateInstance(typeof(ActivityCategory), nonPublic: true)!;
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.Id))!.SetValue(category, Guid.NewGuid());
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.Code))!.SetValue(category, code);
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.NameKey))!.SetValue(category, "help.category." + code);
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.DefaultSafetyLevel))!.SetValue(category, safetyLevel);
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.IsBlocked))!.SetValue(category, isBlocked);
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.ReferralGroup))!.SetValue(category, referralGroup);
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.IsActive))!.SetValue(category, true);
        return category;
    }

    private HelpRequest NewRequest(Guid categoryId, DateTimeOffset start, Guid? organizationId = null, string? notes = null) =>
        HelpRequest.Create(
            organizationId: organizationId,
            seniorUserId: _seniorId,
            createdByUserId: _seniorId,
            categoryId: categoryId,
            safetyLevel: 1,
            trustLevel: 1,
            scheduledStartUtc: start,
            scheduledEndUtc: start.AddHours(1),
            durationMinutes: 60,
            locationType: LocationType.PublicPlace,
            notes: notes,
            latitude: 48.208176,
            longitude: 16.373819).Value!;

    private HelpRequest NewAssignedRequest(Guid categoryId, DateTimeOffset start, Guid? organizationId = null)
    {
        var request = NewRequest(categoryId, start, organizationId);
        request.Assign(_volunteerId, request.RowVersion).IsSuccess.Should().BeTrue();
        return request;
    }

    // --- P3-01 ------------------------------------------------------------

    [Fact]
    public void Cancel_AfterNoShow_IsRejectedAsInvalidTransition()
    {
        // Arrange
        var request = NewAssignedRequest(Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-2));
        request.MarkNoShow(_seniorId).IsSuccess.Should().BeTrue();

        // Act
        var result = request.Cancel(_seniorId, "changed my mind");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("INVALID_STATE_TRANSITION");
        request.Status.Should().Be(HelpRequestStatus.NoShow);
    }

    // --- P3-17 / P3-20 ----------------------------------------------------

    [Fact]
    public async Task Complete_BeforeScheduledDay_LogsActivityOnActualDay_AndRecordsReliabilityOnce()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var category = CreateCategory("shopping");
        db.ActivityCategories.Add(category);
        var request = NewAssignedRequest(category.Id, DateTimeOffset.UtcNow.AddDays(3));
        db.HelpRequests.Add(request);
        await db.SaveChangesAsync();
        var reliability = new CountingReliabilityUpdater();
        var service = CreateService(db, reliability);

        // Act
        var result = await service.CompleteHelpRequestAsync(request.Id, _volunteerId, new CompleteHelpRequestRequest(45));

        // Assert
        result.IsSuccess.Should().BeTrue();
        var activity = await db.Activities.SingleAsync();
        activity.OccurredOn.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));
        activity.DurationMinutes.Should().Be(45);
        reliability.ReliableCalls.Should().Be(1, "P3-20: one completion is one reliability outcome");
    }

    [Fact]
    public async Task Complete_WhenActivityCannotBeLogged_DoesNotPersistCompletion()
    {
        // Arrange: a blocked category can never become an Activity.
        using var db = CreateInMemoryDb();
        var category = CreateCategory("nursing_blocked", 5, isBlocked: true, referralGroup: "Mobile Pflege");
        db.ActivityCategories.Add(category);
        var request = NewAssignedRequest(category.Id, DateTimeOffset.UtcNow.AddHours(-1));
        db.HelpRequests.Add(request);
        await db.SaveChangesAsync();
        var reliability = new CountingReliabilityUpdater();
        var service = CreateService(db, reliability);

        // Act
        var result = await service.CompleteHelpRequestAsync(request.Id, _volunteerId, new CompleteHelpRequestRequest());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CATEGORY_BLOCKED");
        db.ChangeTracker.Clear();
        (await db.HelpRequests.SingleAsync()).Status.Should().Be(HelpRequestStatus.Assigned);
        (await db.Activities.CountAsync()).Should().Be(0);
        reliability.ReliableCalls.Should().Be(0);
    }

    // --- Party authorization ------------------------------------------------

    [Fact]
    public async Task NonParty_GetsNotFound_ForCancelHistoryNoShowDisputeAndAssignedDetail()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var request = NewAssignedRequest(Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-2));
        db.HelpRequests.Add(request);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        // Act
        var cancel = await service.CancelHelpRequestAsync(request.Id, _strangerId, new CancelHelpRequestRequest("x"));
        var history = await service.GetStatusHistoryAsync(request.Id, _strangerId);
        var noShow = await service.MarkNoShowAsync(request.Id, _strangerId, new NoShowHelpRequestRequest());
        var dispute = await service.DisputeNoShowAsync(request.Id, _strangerId, new DisputeNoShowRequest("x"));
        var detail = await service.GetHelpRequestByIdAsync(request.Id, _strangerId);

        // Assert
        foreach (var error in new[] { cancel.Error, history.Error, noShow.Error, dispute.Error, detail.Error })
        {
            error.Should().NotBeNull();
            error!.Kind.Should().Be(ErrorKind.NotFound);
        }

        (await db.HelpRequests.SingleAsync()).Status.Should().Be(HelpRequestStatus.Assigned);
    }

    [Fact]
    public async Task Parties_CanReadHistory_AndOrgCoordinator_CanCancel()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var coordinatorId = Guid.NewGuid();
        var org = Organization.Create("Testverein Musterstadt", OrganizationType.Ngo);
        db.Organizations.Add(org);
        var membership = OrganizationMembership.Create(org.Id, coordinatorId, MembershipRole.Coordinator);
        membership.Activate();
        db.OrganizationMemberships.Add(membership);
        var request = NewAssignedRequest(Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(5), org.Id);
        db.HelpRequests.Add(request);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        // Act
        var seniorHistory = await service.GetStatusHistoryAsync(request.Id, _seniorId);
        var volunteerDetail = await service.GetHelpRequestByIdAsync(request.Id, _volunteerId);
        var coordinatorCancel = await service.CancelHelpRequestAsync(
            request.Id, coordinatorId, new CancelHelpRequestRequest("Senior im Spital", CancellationReasonCode.NeedsMetOtherwise));

        // Assert
        seniorHistory.IsSuccess.Should().BeTrue();
        volunteerDetail.IsSuccess.Should().BeTrue();
        volunteerDetail.Value!.SeniorUserId.Should().Be(_seniorId);
        coordinatorCancel.IsSuccess.Should().BeTrue();
        coordinatorCancel.Value!.Status.Should().Be(HelpRequestStatus.Cancelled);
    }

    [Fact]
    public async Task OpenRequests_AndNonPartyDetail_AreRedacted()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var request = NewRequest(Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(5), notes: "Klingel defekt, bitte anrufen");
        db.HelpRequests.Add(request);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        // Act
        var list = await service.GetOpenHelpRequestsAsync();
        var detail = await service.GetHelpRequestByIdAsync(request.Id, _strangerId);

        // Assert
        foreach (var dto in new[] { list.Value!.Single(), detail.Value! })
        {
            dto.SeniorUserId.Should().Be(Guid.Empty);
            dto.CreatedByUserId.Should().Be(Guid.Empty);
            dto.Notes.Should().BeNull();
            // Phase 3: the senior has not consented (default NONE), so a
            // non-party sees neither the grid cell nor the area.
            dto.Latitude.Should().BeNull();
            dto.Longitude.Should().BeNull();
            dto.LocationPostalCode.Should().BeNull();
        }
    }

    // --- P3-19 --------------------------------------------------------------

    [Fact]
    public async Task NoShow_ReportedByVolunteer_DoesNotPenaliseVolunteer()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var request = NewAssignedRequest(Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-2));
        db.HelpRequests.Add(request);
        await db.SaveChangesAsync();
        var reliability = new CountingReliabilityUpdater();
        var service = CreateService(db, reliability);

        // Act
        var result = await service.MarkNoShowAsync(request.Id, _volunteerId, new NoShowHelpRequestRequest("Niemand hat geöffnet"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        reliability.UnreliableCalls.Should().Be(0);
    }

    [Fact]
    public async Task Dispute_ByReporter_IsForbidden_ByAccusedVolunteer_RestoresScore()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var request = NewAssignedRequest(Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-2));
        db.HelpRequests.Add(request);
        await db.SaveChangesAsync();
        var reliability = new CountingReliabilityUpdater();
        var service = CreateService(db, reliability);
        await service.MarkNoShowAsync(request.Id, _seniorId, new NoShowHelpRequestRequest());

        // Act
        var byReporter = await service.DisputeNoShowAsync(request.Id, _seniorId, new DisputeNoShowRequest("x"));
        var byVolunteer = await service.DisputeNoShowAsync(request.Id, _volunteerId, new DisputeNoShowRequest("Ich war da"));

        // Assert
        byReporter.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        byVolunteer.IsSuccess.Should().BeTrue();
        reliability.UnreliableCalls.Should().Be(1);
        reliability.RestoreCalls.Should().Be(1, "BR-HELP-06: a disputed no-show does not count until resolved");
    }

    // --- BR-HELP-06 resolution -------------------------------------------------

    private async Task<(SeniorConnectDbContext Db, HelpRequest Request, Guid CoordinatorId, Guid OrgMemberId, CountingReliabilityUpdater Reliability)>
        ArrangeDisputedNoShowAsync()
    {
        var db = CreateInMemoryDb();
        var coordinatorId = Guid.NewGuid();
        var orgMemberId = Guid.NewGuid();
        var org = Organization.Create("Testverein Musterstadt", OrganizationType.Ngo);
        db.Organizations.Add(org);
        var coordinator = OrganizationMembership.Create(org.Id, coordinatorId, MembershipRole.Coordinator);
        coordinator.Activate();
        var member = OrganizationMembership.Create(org.Id, orgMemberId, MembershipRole.Volunteer);
        member.Activate();
        db.OrganizationMemberships.AddRange(coordinator, member);
        var request = NewAssignedRequest(Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-2), org.Id);
        db.HelpRequests.Add(request);
        await db.SaveChangesAsync();

        var reliability = new CountingReliabilityUpdater();
        var service = CreateService(db, reliability);
        (await service.MarkNoShowAsync(request.Id, _seniorId, new NoShowHelpRequestRequest())).IsSuccess.Should().BeTrue();
        (await service.DisputeNoShowAsync(request.Id, _volunteerId, new DisputeNoShowRequest("Ich war da"))).IsSuccess.Should().BeTrue();
        return (db, request, coordinatorId, orgMemberId, reliability);
    }

    [Fact]
    public async Task ResolveDispute_Upheld_ReappliesPenalty_AndCanOnlyBeResolvedOnce()
    {
        // Arrange
        var (db, request, coordinatorId, _, reliability) = await ArrangeDisputedNoShowAsync();
        using var ownedDb = db;
        var service = CreateService(db, reliability);

        // Act
        var first = await service.ResolveNoShowDisputeAsync(request.Id, coordinatorId, new ResolveNoShowDisputeRequest(true, "Senior bestätigt"));
        var second = await service.ResolveNoShowDisputeAsync(request.Id, coordinatorId, new ResolveNoShowDisputeRequest(false));

        // Assert
        first.IsSuccess.Should().BeTrue();
        reliability.UnreliableCalls.Should().Be(2, "original penalty + re-applied penalty");
        second.Error!.Code.Should().Be("NO_SHOW_DISPUTE_ALREADY_RESOLVED");
        var stored = await db.HelpRequests.SingleAsync();
        stored.NoShowDisputeUpheld.Should().BeTrue();
        stored.NoShowDisputeResolvedByUserId.Should().Be(coordinatorId);
        (await db.HelpRequestStatusHistories.CountAsync(h => h.Reason!.Contains("resolved (upheld)"))).Should().Be(1);
    }

    [Fact]
    public async Task ResolveDispute_Rejected_LeavesScoreRestored()
    {
        // Arrange
        var (db, request, coordinatorId, _, reliability) = await ArrangeDisputedNoShowAsync();
        using var ownedDb = db;

        // Act
        var result = await CreateService(db, reliability).ResolveNoShowDisputeAsync(request.Id, coordinatorId, new ResolveNoShowDisputeRequest(false));

        // Assert
        result.IsSuccess.Should().BeTrue();
        reliability.UnreliableCalls.Should().Be(1);
        reliability.RestoreCalls.Should().Be(1);
    }

    [Fact]
    public async Task ResolveDispute_ByNonStaffMember_Is403_ByOutsiderOrSenior_Is404()
    {
        // Arrange
        var (db, request, _, orgMemberId, reliability) = await ArrangeDisputedNoShowAsync();
        using var ownedDb = db;
        var service = CreateService(db, reliability);

        // Act
        var member = await service.ResolveNoShowDisputeAsync(request.Id, orgMemberId, new ResolveNoShowDisputeRequest(true));
        var outsider = await service.ResolveNoShowDisputeAsync(request.Id, _strangerId, new ResolveNoShowDisputeRequest(true));
        var senior = await service.ResolveNoShowDisputeAsync(request.Id, _seniorId, new ResolveNoShowDisputeRequest(true));

        // Assert
        member.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        outsider.Error!.Kind.Should().Be(ErrorKind.NotFound);
        senior.Error!.Kind.Should().Be(ErrorKind.NotFound);
        (await db.HelpRequests.SingleAsync()).NoShowDisputeResolvedAtUtc.Should().BeNull();
    }

    // --- P3-04 / P3-21 --------------------------------------------------------

    [Theory]
    [InlineData("Bitte Insulin spritzen am Abend")]
    [InlineData("Wundversorgung am Bein")]
    [InlineData("Verband wechseln")]
    public async Task Create_WithNursingNotes_IsReferredLikeBlockedCategory(string notes)
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var shopping = CreateCategory("shopping");
        db.ActivityCategories.AddRange(shopping, CreateCategory("nursing_blocked", 5, isBlocked: true, referralGroup: "Mobile Pflege"));
        await db.SaveChangesAsync();
        var start = DateTimeOffset.UtcNow.AddDays(1);

        // Act
        var result = await CreateService(db).CreateHelpRequestAsync(_seniorId, _seniorId, new CreateHelpRequestRequest(
            null, shopping.Id, start, start.AddHours(1), 60, LocationType.PublicPlace, Notes: notes));

        // Assert
        result.Error!.Code.Should().Be("CATEGORY_BLOCKED");
        result.Error.Extensions!["referralGroup"].Should().Be("Mobile Pflege");
        (await db.HelpRequests.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_WithoutScheduledEnd_ReturnsStableValidationCode()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var shopping = CreateCategory("shopping");
        db.ActivityCategories.Add(shopping);
        await db.SaveChangesAsync();

        // Act
        var result = await CreateService(db).CreateHelpRequestAsync(_seniorId, _seniorId, new CreateHelpRequestRequest(
            null, shopping.Id, DateTimeOffset.UtcNow.AddDays(1), default, 60, LocationType.PublicPlace));

        // Assert
        result.Error!.Code.Should().Be("SCHEDULED_END_REQUIRED");
        result.Error.Kind.Should().Be(ErrorKind.Validation);
    }

    [Fact]
    public async Task VoiceParser_StillBlocksNursing_ViaSharedKeywordList()
    {
        var result = await new VoiceRequestParser().ParseTranscriptAsync(
            new VoiceParseRequest("Ich brauche jemanden zum Verband wechseln"));

        result.Value!.IsBlockedCategory.Should().BeTrue();
    }

    // --- P3-02 / BR-SAFETY-02 -------------------------------------------------

    [Theory]
    [InlineData(1, LocationType.SeniorHome, 4, 5)]      // home visit: L4 needs trust 5
    [InlineData(3, LocationType.PublicPlace, 3, 3)]     // category default wins over a milder location
    [InlineData(1, LocationType.PublicPlace, 2, 2)]     // location wins over a milder category
    [InlineData(1, LocationType.Other, 1, 1)]
    public void SafetyPolicy_UsesStricterOfCategoryAndContext(int categoryDefault, LocationType location, int expectedLevel, int expectedTrust)
    {
        var result = new ActivitySafetyPolicy().Evaluate(CreateCategory("x", categoryDefault), location, TransportMode.None, isSubjectVulnerable: false);

        result.RequiredSafetyLevel.Should().Be(expectedLevel);
        result.RequiredTrustLevel.Should().Be(expectedTrust);
    }

    // --- P3-13 ------------------------------------------------------------------

    [Fact]
    public async Task OfferSweep_DispatchesTier1Immediately_ThenTier2OnlyToNewVolunteers()
    {
        // Arrange: 4 eligible volunteers, request created through the real factory.
        using var db = CreateInMemoryDb();
        var volunteerIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToList();
        foreach (var id in volunteerIds)
        {
            var profile = VolunteerProfile.Create(id, "Vol");
            profile.UpdateStatus(true);
            db.VolunteerProfiles.Add(profile);
            db.TrustLevelSnapshots.Add(TrustLevelSnapshot.Create(id, 2, "{}"));
        }

        var request = NewRequest(Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(5));
        db.HelpRequests.Add(request);
        await db.SaveChangesAsync();

        var clock = new HelpRequestMatchingAndSafetyTests.ShiftedTimeProvider();
        var matching = new MatchingService(
            db, db, new TrustLevelReader(db), new SafetyBoundaryReader(db),
            Microsoft.Extensions.Options.Options.Create(new MatchingConfig()),
            new SeniorConnect.Modules.Notifications.Infrastructure.NotificationService(db),
            new OrganizationCoordinatorReader(db), clock);

        // Act
        var tier1 = await matching.AdvanceStaleOffersAsync();
        var tier1Again = await matching.AdvanceStaleOffersAsync();
        clock.Offset = TimeSpan.FromMinutes(16);
        var tier2 = await matching.AdvanceStaleOffersAsync();

        // Assert
        tier1.Should().Be(1);
        tier1Again.Should().Be(0, "tier 1 is dispatched once");
        tier2.Should().Be(1);

        var stored = await db.HelpRequests.SingleAsync();
        stored.OfferTier.Should().Be(2);
        stored.GetOfferedVolunteerIds().Should().BeEquivalentTo(volunteerIds);

        var perVolunteer = await db.NotificationMessages
            .GroupBy(m => m.RecipientUserId)
            .Select(g => g.Count())
            .ToListAsync();
        perVolunteer.Should().HaveCount(4).And.OnlyContain(c => c == 1, "nobody is offered the same request twice");
    }
}
