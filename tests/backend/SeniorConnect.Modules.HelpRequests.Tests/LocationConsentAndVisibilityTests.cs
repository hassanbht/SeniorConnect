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
using SeniorConnect.Modules.Organizations.Contracts;
using SeniorConnect.Modules.Organizations.Domain;
using SeniorConnect.Modules.Organizations.Infrastructure;
using SeniorConnect.Modules.Profiles.Application;
using SeniorConnect.Modules.Profiles.Domain;
using SeniorConnect.Modules.Profiles.Infrastructure;
using SeniorConnect.Modules.TrustSafety.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.HelpRequests.Tests;

/// <summary>
/// Wave-1 negative tenant-isolation tests (BR-TENANT-03).
///
/// GET /api/v1/help-requests?organizationId=X must NOT treat X as an
/// authorization: it is a client-supplied selector. Only active staff of X may
/// narrow the list to X; everybody else gets 404 so the foreign organization is
/// never confirmed. Without the guard, any authenticated user could enumerate
/// another organization's open help requests — senior ids, schedule and city
/// included.
/// </summary>
public sealed class HelpRequestTenantIsolationTests : IDisposable
{
    private readonly SeniorConnectDbContext _db;
    private readonly TrustSafety.Infrastructure.SafeguardingDbContext _safeguarding =
        Phase4TestSupport.NewSafeguardingDb();

    private readonly Guid _orgId = Organization.Create("Sozialsprengel Test", OrganizationType.Ngo).Id;
    private readonly Guid _otherOrgId = Organization.Create("Anderer Verein", OrganizationType.Ngo).Id;

    private readonly Guid _seniorId = Guid.NewGuid();
    private readonly Guid _coordinatorId = Guid.NewGuid();
    private readonly Guid _strangerId = Guid.NewGuid();
    private readonly Guid _otherOrgCoordinatorId = Guid.NewGuid();

    public HelpRequestTenantIsolationTests()
    {
        _db = new SeniorConnectDbContext(
            new DbContextOptionsBuilder<SeniorConnectDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new DefaultTenantContext { IsPlatformScope = true });

        AddMember(_orgId, _coordinatorId, MembershipRole.Coordinator);
        AddMember(_otherOrgId, _otherOrgCoordinatorId, MembershipRole.Coordinator);
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private void AddMember(Guid orgId, Guid userId, MembershipRole role)
    {
        var membership = OrganizationMembership.Create(orgId, userId, role);
        membership.Activate();
        _db.OrganizationMemberships.Add(membership);
    }

    private HelpRequestService HelpRequests(IOrganizationAccessGuard? accessGuard = null)
    {
        // A guard is registered unless the caller explicitly asks for the
        // fail-closed case (an explicit flag, since null is also the default).
        return new HelpRequestService(
            _db,
            new ActivitySafetyPolicy(),
            new TrustLevelReader(_db),
            new SafetyBoundaryReader(_db),
            new UserContactReader(_db),
            new VolunteerReliabilityUpdater(_db),
            familyPermissionReader: null,
            accessGuard: accessGuard ?? new OrganizationAccessGuard(_db),
            locationVisibility: Phase4TestSupport.LocationVisibility(_db, _safeguarding));
    }

    /// <summary>The service with no access guard registered at all.</summary>
    private HelpRequestService HelpRequestsWithoutGuard() => new(
        _db,
        new ActivitySafetyPolicy(),
        new TrustLevelReader(_db),
        new SafetyBoundaryReader(_db),
        new UserContactReader(_db),
        new VolunteerReliabilityUpdater(_db),
        familyPermissionReader: null,
        accessGuard: null,
        locationVisibility: Phase4TestSupport.LocationVisibility(_db, _safeguarding));

    private async Task SeedOpenRequestAsync()
    {
        var start = DateTimeOffset.UtcNow.AddHours(3);
        var request = HelpRequest.Create(
            organizationId: _orgId,
            seniorUserId: _seniorId,
            createdByUserId: _seniorId,
            categoryId: Guid.NewGuid(),
            safetyLevel: 1,
            trustLevel: 1,
            scheduledStartUtc: start,
            scheduledEndUtc: start.AddHours(1),
            durationMinutes: 60,
            locationType: LocationType.SeniorHome,
            address: "Maria-Theresien-Straße 7",
            postalCode: "6020",
            city: "Innsbruck",
            latitude: 47.26917,
            longitude: 11.40433).Value!;

        _db.HelpRequests.Add(request);
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task A_coordinator_may_narrow_the_list_to_their_own_organization()
    {
        await SeedOpenRequestAsync();

        var result = await HelpRequests().GetOpenHelpRequestsAsync(_orgId, _coordinatorId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
    }

    [Fact]
    public async Task A_coordinator_of_another_organization_gets_not_found_not_the_rows()
    {
        await SeedOpenRequestAsync();

        var result = await HelpRequests().GetOpenHelpRequestsAsync(_orgId, _otherOrgCoordinatorId);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task An_authenticated_stranger_gets_not_found()
    {
        await SeedOpenRequestAsync();

        var result = await HelpRequests().GetOpenHelpRequestsAsync(_orgId, _strangerId);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task It_fails_closed_when_no_access_guard_is_registered()
    {
        await SeedOpenRequestAsync();

        var result = await HelpRequestsWithoutGuard().GetOpenHelpRequestsAsync(_orgId, _coordinatorId);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task Discovery_without_an_organization_selector_is_unaffected()
    {
        await SeedOpenRequestAsync();

        // The public discovery path carries no organizationId and must keep working.
        var result = await HelpRequests().GetOpenHelpRequestsAsync(viewerUserId: _strangerId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
    }
}

/// <summary>
/// Phase 3 (docs/security/location-consent-and-visibility.md): consent,
/// the central disclosure policy, and help-request coordinate protection.
/// Tnn in a test name refers to the Phase 3 test list.
/// </summary>
public sealed class LocationConsentAndVisibilityTests : IDisposable
{
    // The senior's exact home — these digits must never leave the server
    // except through an authorized PRECISE decision.
    private const double HomeLat = 47.26917;
    private const double HomeLon = 11.40433;

    // A volunteer's exact home, also secret.
    private const double VolunteerLat = 47.27531;
    private const double VolunteerLon = 11.41157;

    private readonly SeniorConnectDbContext _db;
    private readonly TrustSafety.Infrastructure.SafeguardingDbContext _safeguarding = Phase4TestSupport.NewSafeguardingDb();
    private readonly Guid _orgId;
    private readonly Guid _seniorId = Guid.NewGuid();
    private readonly Guid _assignedVolunteerId = Guid.NewGuid();
    private readonly Guid _coordinatorId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _plainStaffId = Guid.NewGuid();
    private readonly Guid _orgVolunteerMemberId = Guid.NewGuid();
    private readonly Guid _otherOrgCoordinatorId = Guid.NewGuid();
    private readonly Guid _strangerId = Guid.NewGuid();

    public LocationConsentAndVisibilityTests()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new SeniorConnectDbContext(options, new DefaultTenantContext { IsPlatformScope = true });

        var org = Organization.Create("Sozialsprengel Test", OrganizationType.Ngo);
        var otherOrg = Organization.Create("Anderer Verein", OrganizationType.Ngo);
        _orgId = org.Id;
        _db.Organizations.AddRange(org, otherOrg);
        AddMember(_orgId, _coordinatorId, MembershipRole.Coordinator);
        AddMember(_orgId, _adminId, MembershipRole.Admin);
        AddMember(_orgId, _plainStaffId, MembershipRole.Staff);
        AddMember(_orgId, _orgVolunteerMemberId, MembershipRole.Volunteer);
        AddMember(otherOrg.Id, _otherOrgCoordinatorId, MembershipRole.Coordinator);

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

    private LocationSharingService Consent() => new(_db);

    private HelpRequestService HelpRequests() => new(
        _db,
        new ActivitySafetyPolicy(),
        new TrustLevelReader(_db),
        new SafetyBoundaryReader(_db),
        new UserContactReader(_db),
        new VolunteerReliabilityUpdater(_db),
        familyPermissionReader: null,
        accessGuard: new OrganizationAccessGuard(_db),
        locationVisibility: Phase4TestSupport.LocationVisibility(_db, _safeguarding));

    private ProximityService Proximity() => new(
        geoDb: _db, profileDb: _db, orgDb: _db,
        helpRequestReader: new HelpRequestDiscoveryReader(_db),
        communityReader: new CommunityDiscoveryReader(_db),
        locationVisibility: Phase4TestSupport.LocationVisibility(_db, _safeguarding));

    private async Task SetConsentAsync(Guid userId, string level)
    {
        var result = await Consent().SetAsync(userId, new UpdateLocationSharingRequest(level, LocationSharingConsent.CurrentVersion));
        result.IsSuccess.Should().BeTrue();
    }

    private async Task<HelpRequest> SeedRequestAsync(bool assigned = true)
    {
        var start = DateTimeOffset.UtcNow.AddHours(3);
        var request = HelpRequest.Create(
            organizationId: _orgId,
            seniorUserId: _seniorId,
            createdByUserId: _seniorId,
            categoryId: Guid.NewGuid(),
            safetyLevel: 1,
            trustLevel: 1,
            scheduledStartUtc: start,
            scheduledEndUtc: start.AddHours(1),
            durationMinutes: 60,
            locationType: LocationType.SeniorHome,
            address: "Maria-Theresien-Straße 7",
            postalCode: "6020",
            city: "Innsbruck",
            latitude: HomeLat,
            longitude: HomeLon).Value!;
        if (assigned)
        {
            request.Assign(_assignedVolunteerId, request.RowVersion).IsSuccess.Should().BeTrue();
        }

        _db.HelpRequests.Add(request);
        await _db.SaveChangesAsync();
        return request;
    }

    private async Task<HelpRequestLocationDto> LocationAsAsync(Guid requestId, Guid viewer)
    {
        var result = await HelpRequests().GetHelpRequestLocationAsync(requestId, viewer);
        result.IsSuccess.Should().BeTrue();
        return result.Value!;
    }

    private static void ShouldNotContainExactHome(object value)
    {
        var json = JsonSerializer.Serialize(value);
        json.Should().NotContain("47.26917").And.NotContain("11.40433", "the senior's exact home never leaves in this context");
    }

    private static void ShouldNotContainExactVolunteerHome(object value)
    {
        var json = JsonSerializer.Serialize(value);
        json.Should().NotContain("47.27531").And.NotContain("11.41157");
    }

    // --- consent model ------------------------------------------------------

    [Fact]
    public async Task T01_DefaultLocationSharing_IsNone_ForEveryExistingOrNewUser()
    {
        var result = await Consent().GetAsync(Guid.NewGuid());

        result.Value!.Level.Should().Be("NONE");
        result.Value.GrantedAtUtc.Should().BeNull("nobody is migrated into sharing");
        result.Value.CurrentConsentVersion.Should().Be(LocationSharingConsent.CurrentVersion);
    }

    [Fact]
    public async Task Consent_ChangesAreAppendOnlyHistory_WithVersionAndUtcTimestamps()
    {
        await SetConsentAsync(_seniorId, "PRECISE");
        await SetConsentAsync(_seniorId, "NONE");

        var rows = await _db.LocationSharingConsents.Where(c => c.UserId == _seniorId).OrderBy(c => c.GrantedAtUtc).ToListAsync();
        rows.Should().HaveCount(2);
        rows[0].Level.Should().Be(LocationSharingLevel.Precise);
        rows[0].RevokedAtUtc.Should().NotBeNull();
        rows[0].RevokedAtUtc!.Value.Offset.Should().Be(TimeSpan.Zero);
        rows[1].Level.Should().Be(LocationSharingLevel.None);
        rows[1].IsCurrent.Should().BeTrue();
        rows.Should().OnlyContain(r => r.ConsentVersion == LocationSharingConsent.CurrentVersion && r.GrantedAtUtc.Offset == TimeSpan.Zero);
    }

    [Fact]
    public async Task Consent_SettingTheSameLevelAgain_AddsNoHistoryRow()
    {
        await SetConsentAsync(_seniorId, "APPROXIMATE");
        await SetConsentAsync(_seniorId, "APPROXIMATE");

        (await _db.LocationSharingConsents.CountAsync(c => c.UserId == _seniorId)).Should().Be(1);
    }

    [Fact]
    public async Task Consent_RequiresTheCurrentExplanationVersion_ExceptToTurnSharingOff()
    {
        var outdated = await Consent().SetAsync(_seniorId, new UpdateLocationSharingRequest("PRECISE", "2020-old"));
        outdated.Error!.Code.Should().Be("CONSENT_VERSION_OUTDATED");

        var invalid = await Consent().SetAsync(_seniorId, new UpdateLocationSharingRequest("EVERYTHING", LocationSharingConsent.CurrentVersion));
        invalid.Error!.Code.Should().Be("LOCATION_SHARING_LEVEL_INVALID");

        var off = await Consent().SetAsync(_seniorId, new UpdateLocationSharingRequest("NONE", ""));
        off.IsSuccess.Should().BeTrue();
    }

    // --- the central policy --------------------------------------------------

    [Theory]
    [InlineData(LocationRelationship.None)]
    [InlineData(LocationRelationship.OrganizationStaff)]
    [InlineData(LocationRelationship.DiscoveryViewer)]
    public void T05_T06_T07_T08_OnlyARelationshipPlusConsentReachesPrecise(LocationRelationship relationship)
    {
        // Membership, coordinator, admin, verification: none is a policy input
        // that could lift these relationships to PRECISE, whatever the consent.
        foreach (var consent in Enum.GetValues<LocationSharingLevel>())
        {
            LocationVisibilityPolicy.Decide(relationship, consent).Should().NotBe(LocationDisclosure.Precise);
        }
    }

    [Fact]
    public void T02_T03_T04_ConsentMatrix_ForTheAssignedVolunteerAndDiscovery()
    {
        LocationVisibilityPolicy.Decide(LocationRelationship.AssignedVolunteer, LocationSharingLevel.Precise).Should().Be(LocationDisclosure.Precise);
        LocationVisibilityPolicy.Decide(LocationRelationship.AssignedVolunteer, LocationSharingLevel.Approximate).Should().Be(LocationDisclosure.Approximate);
        LocationVisibilityPolicy.Decide(LocationRelationship.AssignedVolunteer, LocationSharingLevel.None).Should().Be(LocationDisclosure.Approximate);
        LocationVisibilityPolicy.Decide(LocationRelationship.DiscoveryViewer, LocationSharingLevel.None).Should().Be(LocationDisclosure.DistanceOnly);
        LocationVisibilityPolicy.Decide(LocationRelationship.DiscoveryViewer, LocationSharingLevel.Approximate).Should().Be(LocationDisclosure.Approximate);
        LocationVisibilityPolicy.Decide(LocationRelationship.DiscoveryViewer, LocationSharingLevel.Precise).Should().Be(LocationDisclosure.Approximate);
    }

    [Fact]
    public void Policy_ShapesCoordinates_ExactGridOrNothing()
    {
        LocationVisibilityPolicy.Shape(LocationDisclosure.Precise, HomeLat, HomeLon).Should().Be((HomeLat, HomeLon));
        LocationVisibilityPolicy.Shape(LocationDisclosure.Approximate, HomeLat, HomeLon).Should().Be((47.27, 11.4));
        LocationVisibilityPolicy.Shape(LocationDisclosure.DistanceOnly, HomeLat, HomeLon).Should().Be(((double?)null, (double?)null));
        LocationVisibilityPolicy.Shape(LocationDisclosure.None, HomeLat, HomeLon).Should().Be(((double?)null, (double?)null));
    }

    // --- help request coordinates ---------------------------------------------

    [Fact]
    public async Task T03_T14_ScenarioC_AssignedVolunteer_WithPreciseConsent_GetsExactCoordinates()
    {
        var request = await SeedRequestAsync();
        await SetConsentAsync(_seniorId, "PRECISE");

        var location = await LocationAsAsync(request.Id, _assignedVolunteerId);

        location.Disclosure.Should().Be("PRECISE");
        location.Latitude.Should().Be(HomeLat);
        location.Longitude.Should().Be(HomeLon);
    }

    [Fact]
    public async Task T14_AssignedVolunteer_WithoutPreciseConsent_GetsOnlyTheArea()
    {
        var request = await SeedRequestAsync();
        await SetConsentAsync(_seniorId, "APPROXIMATE");

        var location = await LocationAsAsync(request.Id, _assignedVolunteerId);

        location.Disclosure.Should().Be("APPROXIMATE");
        location.Latitude.Should().Be(47.27);
        location.PostalCode.Should().Be("6020");
        ShouldNotContainExactHome(location);
    }

    [Fact]
    public async Task T04_T27_ScenarioD_RevokingPreciseConsent_TakesEffectOnTheVeryNextRequest()
    {
        var request = await SeedRequestAsync();
        await SetConsentAsync(_seniorId, "PRECISE");
        (await LocationAsAsync(request.Id, _assignedVolunteerId)).Disclosure.Should().Be("PRECISE");

        await SetConsentAsync(_seniorId, "NONE");

        var after = await LocationAsAsync(request.Id, _assignedVolunteerId);
        after.Disclosure.Should().NotBe("PRECISE");
        ShouldNotContainExactHome(after);
    }

    [Theory]
    [InlineData("coordinator")]
    [InlineData("admin")]
    public async Task T05_T06_T07_T12_T13_ScenarioE_StaffNeverGetExactCoordinates_EvenWithPreciseConsent(string who)
    {
        var request = await SeedRequestAsync();
        await SetConsentAsync(_seniorId, "PRECISE");
        var viewer = who == "coordinator" ? _coordinatorId : _adminId;

        var location = await LocationAsAsync(request.Id, viewer);
        var generic = (await HelpRequests().GetHelpRequestByIdAsync(request.Id, viewer)).Value!;

        location.Disclosure.Should().Be("APPROXIMATE");
        ShouldNotContainExactHome(location);
        ShouldNotContainExactHome(generic);
        generic.LocationAddress.Should().BeNull("staff do not get the visit address either");
    }

    [Fact]
    public async Task T05_T11_PlainStaffAndVolunteerMembers_OfTheSameOrg_GetNothingForAnAssignedRequest()
    {
        var request = await SeedRequestAsync();
        await SetConsentAsync(_seniorId, "PRECISE");

        foreach (var member in new[] { _plainStaffId, _orgVolunteerMemberId })
        {
            (await HelpRequests().GetHelpRequestLocationAsync(request.Id, member)).Error!.Kind.Should().Be(ErrorKind.NotFound);
            (await HelpRequests().GetHelpRequestByIdAsync(request.Id, member)).Error!.Kind.Should().Be(ErrorKind.NotFound);
        }
    }

    [Fact]
    public async Task T08_ScenarioB_AVerifiedVolunteer_IsStillShownOnlyAsABand_WithoutConsent()
    {
        var volunteer = VolunteerProfile.Create(Guid.NewGuid(), postalCode: "6020", latitude: VolunteerLat, longitude: VolunteerLon, maxDistanceKm: 15);
        _db.VolunteerProfiles.Add(volunteer);
        _db.TrustLevelSnapshots.Add(TrustLevelSnapshot.Create(volunteer.UserId, 5, "{}")); // fully verified
        _db.SupportProfiles.Add(SupportProfile.Create(_strangerId, postalCode: "6020", latitude: HomeLat, longitude: HomeLon));
        await _db.SaveChangesAsync();

        var match = (await Proximity().FindNearbyVolunteersAsync(_strangerId, radiusKm: 10)).Value!.Should().ContainSingle().Subject;

        match.GemeindeName.Should().BeEmpty();
        match.PostalCode.Should().BeEmpty();
        match.Latitude.Should().BeNull();
    }

    [Fact]
    public async Task T09_T26_UnrelatedCaller_CannotReachTheLocationOfAnAssignedRequest()
    {
        var request = await SeedRequestAsync();
        await SetConsentAsync(_seniorId, "PRECISE");

        var result = await HelpRequests().GetHelpRequestLocationAsync(request.Id, _strangerId);

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
    }

    [Fact]
    public async Task T10_ScenarioF_CrossTenantCoordinator_IsDenied()
    {
        var request = await SeedRequestAsync();
        await SetConsentAsync(_seniorId, "PRECISE");

        (await HelpRequests().GetHelpRequestLocationAsync(request.Id, _otherOrgCoordinatorId)).Error!.Kind.Should().Be(ErrorKind.NotFound);
        (await HelpRequests().GetHelpRequestByIdAsync(request.Id, _otherOrgCoordinatorId)).Error!.Kind.Should().Be(ErrorKind.NotFound);
    }

    [Fact]
    public async Task T10_CrossTenantCoordinator_OnAnOpenRequest_IsJustADiscoveryViewer()
    {
        var request = await SeedRequestAsync(assigned: false);
        await SetConsentAsync(_seniorId, "PRECISE");

        var location = await LocationAsAsync(request.Id, _otherOrgCoordinatorId);

        location.Disclosure.Should().Be("APPROXIMATE");
        ShouldNotContainExactHome(location);
    }

    [Fact]
    public async Task T15_GenericHelpRequestDto_NeverCarriesExactCoordinates_ForAnyone()
    {
        var request = await SeedRequestAsync();
        await SetConsentAsync(_seniorId, "PRECISE");

        foreach (var viewer in new[] { _seniorId, _assignedVolunteerId, _coordinatorId, _adminId })
        {
            var dto = (await HelpRequests().GetHelpRequestByIdAsync(request.Id, viewer)).Value!;
            ShouldNotContainExactHome(dto);
        }

        ShouldNotContainExactHome((await HelpRequests().GetSeniorHelpRequestsAsync(_seniorId)).Value!);
    }

    [Fact]
    public async Task T15_TheVisitAddress_StaysWithTheAssignedVolunteerAndTheSenior_Only()
    {
        var request = await SeedRequestAsync();

        (await HelpRequests().GetHelpRequestByIdAsync(request.Id, _assignedVolunteerId)).Value!.LocationAddress.Should().Be("Maria-Theresien-Straße 7");
        (await HelpRequests().GetHelpRequestByIdAsync(request.Id, _seniorId)).Value!.LocationAddress.Should().Be("Maria-Theresien-Straße 7");
        (await HelpRequests().GetHelpRequestByIdAsync(request.Id, _coordinatorId)).Value!.LocationAddress.Should().BeNull();
    }

    [Fact]
    public async Task OwnLocation_IsExact_ForTheSeniorThroughTheLocationEndpoint()
    {
        var request = await SeedRequestAsync();

        var location = await LocationAsAsync(request.Id, _seniorId);

        location.Disclosure.Should().Be("PRECISE");
        location.Latitude.Should().Be(HomeLat);
    }

    // --- discovery -------------------------------------------------------------

    [Fact]
    public async Task T02_T23_T24_ScenarioA_Discovery_ShowsAreaOnlyWithConsent_AndNeverCoordinatesOrKm()
    {
        var consenting = VolunteerProfile.Create(Guid.NewGuid(), postalCode: "6020", latitude: VolunteerLat, longitude: VolunteerLon, maxDistanceKm: 15);
        _db.VolunteerProfiles.Add(consenting);
        _db.SupportProfiles.Add(SupportProfile.Create(_strangerId, postalCode: "6020", latitude: HomeLat, longitude: HomeLon));
        await _db.SaveChangesAsync();
        await SetConsentAsync(consenting.UserId, "PRECISE"); // even PRECISE: discovery has no relationship

        var results = (await Proximity().FindNearbyVolunteersAsync(_strangerId, radiusKm: 10)).Value!;

        var match = results.Should().ContainSingle().Subject;
        match.GemeindeName.Should().Be("Innsbruck");
        match.Latitude.Should().BeNull();
        match.DistanceKm.Should().BeNull();
        match.DistanceBand.Should().StartWith("KM_");
        ShouldNotContainExactVolunteerHome(results);
    }

    [Fact]
    public async Task T23_OpenRequestList_FollowsTheSeniorsConsent()
    {
        await SeedRequestAsync(assigned: false);

        var withoutConsent = (await HelpRequests().GetOpenHelpRequestsAsync()).Value!.Single();
        withoutConsent.Latitude.Should().BeNull();
        withoutConsent.LocationPostalCode.Should().BeNull();

        await SetConsentAsync(_seniorId, "APPROXIMATE");
        var withConsent = (await HelpRequests().GetOpenHelpRequestsAsync()).Value!.Single();
        withConsent.Latitude.Should().Be(47.27);
        withConsent.LocationPostalCode.Should().Be("6020");
        ShouldNotContainExactHome(withConsent);
    }

    // --- coordinate validation ---------------------------------------------------

    [Theory]
    [InlineData(47.2, 11.4, true)]
    [InlineData(null, null, true)]
    [InlineData(91.0, 11.4, false)]
    [InlineData(47.2, 181.0, false)]
    [InlineData(47.2, null, false)]
    [InlineData(double.NaN, 11.4, false)]
    [InlineData(double.PositiveInfinity, 11.4, false)]
    public void T22_CoordinatePairValidation(double? lat, double? lon, bool valid) =>
        GeoCoordinate.IsValidPair(lat, lon).Should().Be(valid);

    [Fact]
    public void T22_AnInvertedPairFallsOutsideAustria()
    {
        GeoCoordinate.IsInAustria(47.2692, 11.4041).Should().BeTrue();
        GeoCoordinate.IsInAustria(11.4041, 47.2692).Should().BeFalse();
    }

    [Fact]
    public async Task T22_ProfileAndRequestWrites_RejectInvalidCoordinates()
    {
        var profile = await new ProfileService(_db).UpsertVolunteerProfileAsync(Guid.NewGuid(),
            new UpdateVolunteerProfileRequest(10, 5, false, false, true, 95.0, 11.4, "Innsbruck", "6020"));
        profile.Error!.Code.Should().Be("LOCATION_INVALID_COORDINATES");

        var start = DateTimeOffset.UtcNow.AddHours(4);
        var request = await HelpRequests().CreateHelpRequestAsync(_seniorId, _seniorId, new CreateHelpRequestRequest(
            OrganizationId: null, CategoryId: Guid.NewGuid(), ScheduledStartUtc: start, ScheduledEndUtc: start.AddHours(1),
            DurationMinutes: 60, LocationType: LocationType.SeniorHome, Latitude: 47.2));
        request.Error!.Code.Should().Be("LOCATION_INVALID_COORDINATES");
    }
}
