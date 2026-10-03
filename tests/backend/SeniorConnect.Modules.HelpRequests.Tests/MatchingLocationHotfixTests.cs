using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Community.Infrastructure;
using SeniorConnect.Modules.Geography.Infrastructure;
using SeniorConnect.Modules.HelpRequests.Application;
using SeniorConnect.Modules.HelpRequests.Domain;
using SeniorConnect.Modules.HelpRequests.Infrastructure;
using SeniorConnect.Modules.Identity.Domain;
using SeniorConnect.Modules.Identity.Infrastructure;
using SeniorConnect.Modules.Matching.Domain;
using SeniorConnect.Modules.Matching.Infrastructure;
using SeniorConnect.Modules.Notifications.Infrastructure;
using SeniorConnect.Modules.Organizations.Domain;
using SeniorConnect.Modules.Organizations.Infrastructure;
using SeniorConnect.Modules.Profiles.Contracts;
using SeniorConnect.Modules.Profiles.Domain;
using SeniorConnect.Modules.TrustSafety.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.HelpRequests.Tests;

/// <summary>
/// Security hotfix regression tests (docs/security/hotfix-matching-location.md):
/// matching authorization, no client-supplied discovery origin, banded
/// distances, no exact coordinates for other people.
/// </summary>
public sealed class MatchingLocationHotfixTests : IDisposable
{
    // Innsbruck city centre — the caller's / senior's stored home.
    private const double HomeLat = 47.2692;
    private const double HomeLon = 11.4041;

    // A volunteer ~1.3 km away; exact values that must never leave the server.
    private const double VolunteerLat = 47.27531;
    private const double VolunteerLon = 11.41157;

    private static readonly string[] Bands =
    [
        DistanceBand.Under1, DistanceBand.From1To3, DistanceBand.From3To5,
        DistanceBand.From5To10, DistanceBand.From10To20, DistanceBand.Over20,
    ];

    private readonly SeniorConnectDbContext _db;
    private readonly Guid _orgId;
    private readonly Guid _otherOrgId;
    private readonly Guid _coordinatorId = Guid.NewGuid();
    private readonly Guid _orgVolunteerMemberId = Guid.NewGuid();
    private readonly Guid _otherOrgCoordinatorId = Guid.NewGuid();
    private readonly Guid _strangerId = Guid.NewGuid();
    private readonly Guid _seniorId = Guid.NewGuid();
    private readonly Guid _volunteerId = Guid.NewGuid();

    public MatchingLocationHotfixTests()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new SeniorConnectDbContext(options, new DefaultTenantContext { IsPlatformScope = true });

        var org = Organization.Create("Sozialsprengel Test", OrganizationType.Ngo);
        var otherOrg = Organization.Create("Anderer Verein", OrganizationType.Ngo);
        _orgId = org.Id;
        _otherOrgId = otherOrg.Id;
        _db.Organizations.AddRange(org, otherOrg);
        AddMember(_orgId, _coordinatorId, MembershipRole.Coordinator);
        AddMember(_orgId, _orgVolunteerMemberId, MembershipRole.Volunteer);
        AddMember(_otherOrgId, _otherOrgCoordinatorId, MembershipRole.Coordinator);

        var volunteer = VolunteerProfile.Create(_volunteerId, postalCode: "6020",
            latitude: VolunteerLat, longitude: VolunteerLon, maxDistanceKm: 15);
        volunteer.UpdateReliability(0.9m);
        _db.VolunteerProfiles.Add(volunteer);
        _db.TrustLevelSnapshots.Add(TrustLevelSnapshot.Create(_volunteerId, 3, "{}"));

        _db.AustrianAdministrativeUnits.Add(Geography.Domain.AustrianAdministrativeUnit.Create(
            bundeslandCode: "7", bundeslandName: "Tirol",
            bezirkCode: "701", bezirkName: "Innsbruck",
            gemeindeCode: "70101", gemeindeName: "Innsbruck",
            postalCode: "6020", localityName: "Innsbruck", latitude: 47.2692, longitude: 11.4041));

        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private void AddMember(Guid orgId, Guid userId, MembershipRole role)
    {
        var membership = OrganizationMembership.Create(orgId, userId, role);
        membership.Activate();
        _db.OrganizationMemberships.Add(membership);
    }

    private MatchingService Matching(bool withGuard = true) => new(
        _db, _db, new TrustLevelReader(_db), new SafetyBoundaryReader(_db),
        Microsoft.Extensions.Options.Options.Create(new MatchingConfig()),
        new NotificationService(_db),
        new OrganizationCoordinatorReader(_db),
        accessGuard: withGuard ? new OrganizationAccessGuard(_db) : null);

    private ProximityService Proximity() => new(
        geoDb: _db, profileDb: _db, orgDb: _db,
        helpRequestReader: new HelpRequestDiscoveryReader(_db),
        communityReader: new CommunityDiscoveryReader(_db));

    private async Task<HelpRequest> SeedRequestAsync(Guid? organizationId, double lat = HomeLat, double lon = HomeLon)
    {
        var start = DateTimeOffset.UtcNow.AddHours(3);
        var request = HelpRequest.Create(
            organizationId: organizationId,
            seniorUserId: _seniorId,
            createdByUserId: _seniorId,
            categoryId: Guid.NewGuid(),
            safetyLevel: 1,
            trustLevel: 1,
            scheduledStartUtc: start,
            scheduledEndUtc: start.AddHours(1),
            durationMinutes: 60,
            locationType: LocationType.SeniorHome,
            latitude: lat,
            longitude: lon).Value!;
        _db.HelpRequests.Add(request);
        await _db.SaveChangesAsync();
        return request;
    }

    private async Task GiveCallerAHomeAsync(Guid userId, double lat = HomeLat, double lon = HomeLon)
    {
        _db.SupportProfiles.Add(SupportProfile.Create(userId, postalCode: "6020", latitude: lat, longitude: lon));
        await _db.SaveChangesAsync();
    }

    private static void ShouldNotContainExactCoordinates(object value)
    {
        var json = JsonSerializer.Serialize(value);
        json.Should().NotContain("47.2753").And.NotContain("11.4115", "exact volunteer coordinates never leave the server");
        json.Should().NotContain("47.2692").And.NotContain("11.4041", "exact senior/request coordinates never leave the server");
    }

    // --- 1. candidates authorization --------------------------------------

    [Fact]
    public async Task Candidates_SameTenantCoordinator_GetsBandedCandidates()
    {
        var request = await SeedRequestAsync(_orgId);

        var result = await Matching().GetCandidatesForCallerAsync(request.Id, _coordinatorId);

        result.IsSuccess.Should().BeTrue();
        var candidate = result.Value!.Should().ContainSingle(c => c.VolunteerUserId == _volunteerId).Subject;
        candidate.IsEligible.Should().BeTrue();
        candidate.DistanceBand.Should().BeOneOf(Bands);
        candidate.Breakdown.Explanation.Should().NotMatchRegex(@"\d+[.,]\d+\s*km");
    }

    [Fact]
    public async Task Candidates_Scores_RevealNothingFinerThanTheSeniorsGridCell()
    {
        // Phase 3 security review: two homes inside the same ~1 km cell must
        // give staff the same distance score — otherwise the continuous score
        // would let them triangulate the senior's exact home.
        var a = await SeedRequestAsync(_orgId, lat: 47.26612, lon: 11.40011);
        var b = await SeedRequestAsync(_orgId, lat: 47.27388, lon: 11.40449);

        var scoreA = (await Matching().GetCandidatesForCallerAsync(a.Id, _coordinatorId)).Value!
            .Single(c => c.VolunteerUserId == _volunteerId).Breakdown.DistanceScore;
        var scoreB = (await Matching().GetCandidatesForCallerAsync(b.Id, _coordinatorId)).Value!
            .Single(c => c.VolunteerUserId == _volunteerId).Breakdown.DistanceScore;

        scoreA.Should().Be(scoreB);
    }

    [Fact]
    public async Task Candidates_CrossTenantCoordinator_Gets404()
    {
        var request = await SeedRequestAsync(_orgId);

        var result = await Matching().GetCandidatesForCallerAsync(request.Id, _otherOrgCoordinatorId);

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
    }

    [Fact]
    public async Task Candidates_SameTenantMemberWithoutStaffRole_Gets403()
    {
        var request = await SeedRequestAsync(_orgId);

        var result = await Matching().GetCandidatesForCallerAsync(request.Id, _orgVolunteerMemberId);

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task Candidates_UnrelatedAuthenticatedUser_Gets404()
    {
        var request = await SeedRequestAsync(_orgId);

        var result = await Matching().GetCandidatesForCallerAsync(request.Id, _strangerId);

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
    }

    [Fact]
    public async Task Candidates_RequestOwner_IsNotAllowed_NoVolunteerIdentitiesForSeniors()
    {
        var request = await SeedRequestAsync(_orgId);

        var result = await Matching().GetCandidatesForCallerAsync(request.Id, _seniorId);

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
    }

    [Fact]
    public async Task Candidates_CommunityRequestWithoutOrganization_IsNotVisibleToAnyHuman()
    {
        // The trilateration attack: create your own request at a chosen point,
        // then ask for the candidates' distances.
        var request = await SeedRequestAsync(organizationId: null);

        var asOwner = await Matching().GetCandidatesForCallerAsync(request.Id, _seniorId);
        var asCoordinator = await Matching().GetCandidatesForCallerAsync(request.Id, _coordinatorId);

        asOwner.Error!.Kind.Should().Be(ErrorKind.NotFound);
        asCoordinator.Error!.Kind.Should().Be(ErrorKind.NotFound);
    }

    [Fact]
    public async Task Candidates_UnknownRequest_Gets404()
    {
        var result = await Matching().GetCandidatesForCallerAsync(Guid.NewGuid(), _coordinatorId);

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
    }

    [Fact]
    public async Task Candidates_WithoutAccessGuard_FailsClosed()
    {
        var request = await SeedRequestAsync(_orgId);

        var result = await Matching(withGuard: false).GetCandidatesForCallerAsync(request.Id, _coordinatorId);

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
    }

    // --- 2. proposals:hybrid authorization --------------------------------

    [Fact]
    public async Task HybridProposals_AreProtectedByTheSameRule()
    {
        var request = await SeedRequestAsync(_orgId);
        var body = new HybridMatchingRequest(request.Id, MinConfidenceThreshold: 0);

        var crossTenant = await Matching().GetHybridProposalsForCallerAsync(body, _otherOrgCoordinatorId);
        var stranger = await Matching().GetHybridProposalsForCallerAsync(body, _strangerId);
        var member = await Matching().GetHybridProposalsForCallerAsync(body, _orgVolunteerMemberId);
        var coordinator = await Matching().GetHybridProposalsForCallerAsync(body, _coordinatorId);

        crossTenant.Error!.Kind.Should().Be(ErrorKind.NotFound);
        stranger.Error!.Kind.Should().Be(ErrorKind.NotFound);
        member.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        coordinator.IsSuccess.Should().BeTrue();
        var proposal = coordinator.Value!.Should().ContainSingle().Subject;
        proposal.CandidateVolunteerUserId.Should().Be(_volunteerId);
        proposal.AiRecommendationReason.Should().NotMatchRegex(@"\d+[.,]\d+\s*km");
    }

    // --- 3. feed origin & banding -----------------------------------------

    [Fact]
    public async Task Feed_UsesTheVolunteersStoredLocation_AndReturnsOnlyBands()
    {
        await SeedRequestAsync(organizationId: null, lat: 47.2800, lon: 11.4200);

        var items = (await Matching().GetVolunteerFeedAsync(_volunteerId)).Value!;

        var item = items.Should().ContainSingle().Subject;
        var expected = DistanceBand.FromKm(LocationPrivacy.HaversineKm(
            VolunteerLat, VolunteerLon, LocationPrivacy.SnapToGrid(47.2800), LocationPrivacy.SnapToGrid(11.4200)));
        item.DistanceBand.Should().Be(expected);
        ShouldNotContainExactCoordinates(items);
    }

    [Fact]
    public async Task Feed_WithoutAStoredLocation_ReturnsNoDistance_NotAFakeOne()
    {
        await SeedRequestAsync(organizationId: null);

        var items = (await Matching().GetVolunteerFeedAsync(Guid.NewGuid())).Value!;

        items.Should().ContainSingle().Which.DistanceBand.Should().BeNull();
    }

    [Fact]
    public async Task Feed_RadiusCannotBeBinarySearched_WithinOneBand()
    {
        // ~4 km away: every radius that snaps to the same band edge gives the
        // same answer, so the radius reveals no more than the band.
        await SeedRequestAsync(organizationId: null, lat: 47.3100, lon: 11.4400);

        var at31 = (await Matching().GetVolunteerFeedAsync(_volunteerId, radiusKm: 3.1)).Value!.Single();
        var at49 = (await Matching().GetVolunteerFeedAsync(_volunteerId, radiusKm: 4.9)).Value!.Single();

        at31.IsEligible.Should().Be(at49.IsEligible);
    }

    // --- 4./5. nearby-volunteers & nearby-requests ------------------------

    [Fact]
    public async Task NearbyVolunteers_WithoutAStoredCallerLocation_ReturnsLocationUnavailable()
    {
        var result = await Proximity().FindNearbyVolunteersAsync(_strangerId, radiusKm: 10);

        result.Error!.Code.Should().Be("LOCATION_UNAVAILABLE");
    }

    [Fact]
    public async Task NearbyVolunteers_ReturnsAreaAndBand_NeverCoordinatesOrIdentity()
    {
        await GiveCallerAHomeAsync(_strangerId);

        var results = (await Proximity().FindNearbyVolunteersAsync(_strangerId, radiusKm: 10)).Value!;

        var match = results.Should().ContainSingle().Subject;
        match.Latitude.Should().BeNull();
        match.Longitude.Should().BeNull();
        match.DistanceKm.Should().BeNull();
        match.UserId.Should().Be(Guid.Empty);
        match.DistanceBand.Should().BeOneOf(Bands);
        // Phase 3: without the volunteer's consent (default NONE) not even
        // the area is shown — see LocationConsentAndVisibilityTests.
        match.GemeindeName.Should().BeEmpty();
        match.PostalCode.Should().BeEmpty();
        ShouldNotContainExactCoordinates(results);
    }

    [Fact]
    public async Task NearbyRequests_WithoutAStoredCallerLocation_ReturnsLocationUnavailable()
    {
        await SeedRequestAsync(organizationId: null);

        var result = await Proximity().FindNearbyRequestsAsync(_strangerId, radiusKm: 10);

        result.Error!.Code.Should().Be("LOCATION_UNAVAILABLE");
    }

    [Fact]
    public async Task NearbyRequests_ReturnsBandOnly_NeverTheSeniorsCoordinates()
    {
        await SeedRequestAsync(organizationId: null);
        await GiveCallerAHomeAsync(_strangerId, lat: 47.2800, lon: 11.4200);

        var results = (await Proximity().FindNearbyRequestsAsync(_strangerId, radiusKm: 10)).Value!;

        var match = results.Should().ContainSingle().Subject;
        match.Latitude.Should().BeNull();
        match.Longitude.Should().BeNull();
        match.DistanceKm.Should().BeNull();
        match.DistanceBand.Should().BeOneOf(Bands);
        ShouldNotContainExactCoordinates(results);
    }

    [Fact]
    public async Task NearbyRequests_OriginIsTheCallersHome_NotAPointTheyChoose()
    {
        // A far-away caller sees nothing nearby — there is no way to pass
        // another origin.
        await SeedRequestAsync(organizationId: null);
        await GiveCallerAHomeAsync(_strangerId, lat: 48.2082, lon: 16.3738); // Vienna

        var results = (await Proximity().FindNearbyRequestsAsync(_strangerId, radiusKm: 20)).Value!;

        results.Should().BeEmpty();
    }

    // --- 10. existing help-request flow still works ------------------------

    [Fact]
    public async Task HelpRequestAcceptFlow_StillWorks_AfterTheHotfix()
    {
        var request = await SeedRequestAsync(organizationId: null);
        var service = new HelpRequestService(
            _db,
            new ActivitySafetyPolicy(),
            new TrustLevelReader(_db),
            new SafetyBoundaryReader(_db),
            new UserContactReader(_db),
            new NullReliabilityUpdater(),
            familyPermissionReader: null,
            accessGuard: new OrganizationAccessGuard(_db));

        var accepted = await service.AcceptHelpRequestAsync(
            request.Id, _volunteerId, new AcceptHelpRequestRequest(request.RowVersion));

        accepted.IsSuccess.Should().BeTrue();
        accepted.Value!.Status.Should().Be(HelpRequestStatus.Assigned);
    }

    private sealed class NullReliabilityUpdater : IVolunteerReliabilityUpdater
    {
        public Task<decimal?> RecordOutcomeAsync(Guid volunteerUserId, bool wasReliable, CancellationToken cancellationToken = default) =>
            Task.FromResult<decimal?>(null);

        public Task RestoreScoreAsync(Guid volunteerUserId, decimal? score, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
