using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SeniorConnect.Domain;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Community.Infrastructure;
using SeniorConnect.Modules.Geography.Infrastructure;
using SeniorConnect.Modules.HelpRequests.Application;
using SeniorConnect.Modules.HelpRequests.Domain;
using SeniorConnect.Modules.HelpRequests.Infrastructure;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Contracts;
using SeniorConnect.Modules.Identity.Domain;
using SeniorConnect.Modules.Identity.Infrastructure;
using SeniorConnect.Modules.Matching.Domain;
using SeniorConnect.Modules.Matching.Infrastructure;
using SeniorConnect.Modules.Notifications.Infrastructure;
using SeniorConnect.Modules.Organizations.Domain;
using SeniorConnect.Modules.Organizations.Infrastructure;
using SeniorConnect.Modules.Profiles.Application;
using SeniorConnect.Modules.Profiles.Domain;
using SeniorConnect.Modules.Profiles.Infrastructure;
using SeniorConnect.Modules.TrustSafety.Application;
using SeniorConnect.Modules.TrustSafety.Domain;
using SeniorConnect.Modules.TrustSafety.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.HelpRequests.Tests;

/// <summary>
/// Phase 4 (docs/security/verification-and-discovery.md,
/// block-report-policy.md, safeguarding-location-policy.md, ADR-026):
/// verification facts, discovery eligibility, blocks, safeguarding
/// restrictions, visit-address hardening and tenant isolation, run through
/// the real services. Tnn refers to the Phase 4 test list, "Scenario X" to
/// its E2E scenarios.
/// </summary>
public sealed class Phase4VerificationDiscoveryTests : IDisposable
{
    private const double HomeLat = 47.26917;
    private const double HomeLon = 11.40433;
    private const double VolunteerLat = 47.27531;
    private const double VolunteerLon = 11.41157;
    private const string VisitAddress = "Maria-Theresien-Straße 7";

    private readonly SeniorConnectDbContext _db;
    private readonly SafeguardingDbContext _sg = Phase4TestSupport.NewSafeguardingDb();
    private readonly Guid _orgA;
    private readonly Guid _orgB;
    private readonly Guid _coordA = Guid.NewGuid();
    private readonly Guid _adminA = Guid.NewGuid();
    private readonly Guid _staffA = Guid.NewGuid();
    private readonly Guid _officerA = Guid.NewGuid();
    private readonly Guid _coordB = Guid.NewGuid();
    private readonly Guid _adminB = Guid.NewGuid();
    private readonly Guid _staffB = Guid.NewGuid();
    private readonly Guid _officerB = Guid.NewGuid();
    private readonly Guid _viewer;
    private int _phoneSeq;

    public Phase4VerificationDiscoveryTests()
    {
        _db = new SeniorConnectDbContext(
            new DbContextOptionsBuilder<SeniorConnectDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new DefaultTenantContext { IsPlatformScope = true });

        var a = Organization.Create("Sozialsprengel A", OrganizationType.Ngo);
        var b = Organization.Create("Verein B", OrganizationType.Ngo);
        _orgA = a.Id;
        _orgB = b.Id;
        _db.Organizations.AddRange(a, b);
        AddMember(_orgA, _coordA, MembershipRole.Coordinator);
        AddMember(_orgA, _adminA, MembershipRole.Admin);
        AddMember(_orgA, _staffA, MembershipRole.Staff);
        AddMember(_orgA, _officerA, MembershipRole.SafeguardingOfficer);
        AddMember(_orgB, _coordB, MembershipRole.Coordinator);
        AddMember(_orgB, _adminB, MembershipRole.Admin);
        AddMember(_orgB, _staffB, MembershipRole.Staff);
        AddMember(_orgB, _officerB, MembershipRole.SafeguardingOfficer);
        _db.SaveChanges();

        // The person browsing: lives at the senior's home point.
        _viewer = NewPerson();
        _db.VolunteerProfiles.Add(VolunteerProfile.Create(_viewer, postalCode: "6020", latitude: HomeLat, longitude: HomeLon));
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _sg.Dispose();
    }

    // --- wiring ---------------------------------------------------------------

    private OrganizationAccessGuard Guard() => new(_db);
    private VerificationFactsReader Facts() => new(_db, Guard());
    private VerificationService Verification() => new(_db, Facts(), Guard(), Guard());
    private DiscoverabilityService Discoverability() => new(_db);
    private SafetyBoundaryReader Blocks() => new(_db);
    private SafeguardingRestrictionReader Restrictions() => new(_sg);
    private LocationSharingService Consent() => new(_db);
    private LocationVisibilityService Location() => new(_db, Blocks(), Restrictions());
    private DiscoveryEligibilityService Eligibility() => new(_db, Facts(), Blocks(), Restrictions(), Guard());
    private TrustSafetyService TrustSafety() => new(_db, new OrganizationCoordinatorReader(_db));
    private SafeguardingService Safeguarding() => new(_sg, new PlatformOfficerOrgReader(new OrganizationCoordinatorReader(_db), _officerA));

    /// <summary>S-4: wraps the real reader and marks one user as an explicit PlatformSafeguardingOfficer, so org-less (platform-level) cases can be administered in tests.</summary>
    private sealed class PlatformOfficerOrgReader(
        OrganizationCoordinatorReader inner,
        Guid platformOfficerUserId) : SeniorConnect.Modules.Organizations.Contracts.IOrganizationCoordinatorReader
    {
        public Task<IReadOnlyList<Guid>> GetActiveCoordinatorUserIdsAsync(Guid organizationId, CancellationToken ct = default)
            => inner.GetActiveCoordinatorUserIdsAsync(organizationId, ct);

        public Task<IReadOnlyList<SeniorConnect.Modules.Organizations.Contracts.StaffOrganizationDto>> GetActiveMembershipsForUserAsync(Guid userId, CancellationToken ct = default)
            => inner.GetActiveMembershipsForUserAsync(userId, ct);

        public Task<IReadOnlyList<Guid>> GetSafeguardingOfficerOrganizationIdsAsync(Guid userId, CancellationToken ct = default)
            => inner.GetSafeguardingOfficerOrganizationIdsAsync(userId, ct);

        public Task<IReadOnlyList<Guid>> GetPlatformSafeguardingOfficerUserIdsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Guid>>([platformOfficerUserId]);
    }

    private HelpRequestService HelpRequests() => new(
        _db, new ActivitySafetyPolicy(), new TrustLevelReader(_db), Blocks(), new UserContactReader(_db),
        new VolunteerReliabilityUpdater(_db), familyPermissionReader: null, accessGuard: Guard(),
        locationVisibility: Location(), restrictions: Restrictions());

    private ProximityService Proximity() => new(
        geoDb: _db, profileDb: _db, orgDb: _db,
        helpRequestReader: new HelpRequestDiscoveryReader(_db),
        communityReader: new CommunityDiscoveryReader(_db),
        locationVisibility: Location(), eligibility: Eligibility());

    private MatchingService Matching() => new(
        _db, _db, new TrustLevelReader(_db), Blocks(), Options.Create(new MatchingConfig()),
        new NotificationService(_db), new OrganizationCoordinatorReader(_db), timeProvider: null,
        accessGuard: Guard(), eligibility: Eligibility(), locationVisibility: Location());

    // --- seeding --------------------------------------------------------------

    private void AddMember(Guid orgId, Guid userId, MembershipRole role)
    {
        var membership = OrganizationMembership.Create(orgId, userId, role);
        membership.Activate();
        _db.OrganizationMemberships.Add(membership);
    }

    private Guid NewPerson(bool email = true, bool phone = true)
    {
        var user = User.CreateWithPhone($"+43660{++_phoneSeq:D7}", "Person");
        if (email) user.VerifyEmail();
        if (phone) user.VerifyPhone();
        _db.Users.Add(user);
        _db.TrustLevelSnapshots.Add(TrustLevelSnapshot.Create(user.Id, 2, "{}"));
        _db.SaveChanges();
        return user.Id;
    }

    /// <summary>A volunteer about 0.8 km from the viewer.</summary>
    private async Task<Guid> VolunteerAsync(bool email = true, bool phone = true, bool discoverable = true, Guid? rosterOf = null)
    {
        var id = NewPerson(email, phone);
        _db.VolunteerProfiles.Add(VolunteerProfile.Create(id, postalCode: "6020", latitude: VolunteerLat, longitude: VolunteerLon));
        if (rosterOf is { } org)
        {
            AddMember(org, id, MembershipRole.Volunteer);
        }
        await _db.SaveChangesAsync();
        if (discoverable)
        {
            (await Discoverability().SetAsync(id, new UpdateDiscoverabilityRequest(true))).IsSuccess.Should().BeTrue();
        }
        return id;
    }

    private async Task<int> NearbyVolunteerCountAsync(Guid? viewer = null) =>
        (await Proximity().FindNearbyVolunteersAsync(viewer ?? _viewer, 25)).Value!.Count;

    private async Task<IReadOnlyCollection<Guid>> CandidatesAsync(HelpRequest request) =>
        (await Matching().FindCandidatesAsync(request.Id)).Value!.Select(c => c.VolunteerUserId).ToList();

    private async Task<HelpRequest> SeedRequestAsync(Guid? orgId, Guid seniorId, Guid? assignedVolunteer = null)
    {
        var start = DateTimeOffset.UtcNow.AddHours(3);
        var request = HelpRequest.Create(
            organizationId: orgId, seniorUserId: seniorId, createdByUserId: seniorId, categoryId: Guid.NewGuid(),
            safetyLevel: 1, trustLevel: 1, scheduledStartUtc: start, scheduledEndUtc: start.AddHours(1),
            durationMinutes: 60, locationType: LocationType.SeniorHome, address: VisitAddress,
            postalCode: "6020", city: "Innsbruck", latitude: HomeLat, longitude: HomeLon).Value!;
        if (assignedVolunteer is { } v)
        {
            request.Assign(v, request.RowVersion).IsSuccess.Should().BeTrue();
        }
        _db.HelpRequests.Add(request);
        await _db.SaveChangesAsync();
        return request;
    }

    private async Task<Guid> OpenCaseAsync(Guid? orgId, Guid subject)
    {
        var sCase = SafeguardingCase.Raise(Guid.NewGuid(), subject, "concern", organizationId: orgId).Value!;
        _sg.SafeguardingCases.Add(sCase);
        await _sg.SaveChangesAsync();
        return sCase.Id;
    }

    private async Task RestrictAsync(Guid? orgId, Guid subject, Guid? counterpart = null,
        bool discovery = false, bool location = false, bool contact = false, bool assignment = false)
    {
        var caseId = await OpenCaseAsync(orgId, subject);
        var officer = orgId == _orgB ? _officerB : _officerA;
        (await Safeguarding().ImposeRestrictionAsync(caseId, officer,
            new ImposeRestrictionRequest(counterpart, discovery, location, contact, assignment))).IsSuccess.Should().BeTrue();
    }

    private async Task<Result<StaffVerificationDto>> RecordAsync(Guid staff, Guid org, Guid volunteer, string type, string method) =>
        await Verification().RecordAsync(staff, org, volunteer, new RecordVerificationRequest(type, method));

    private async Task SetConsentAsync(Guid userId, string level) =>
        (await Consent().SetAsync(userId, new UpdateLocationSharingRequest(level, LocationSharingConsent.CurrentVersion)))
            .IsSuccess.Should().BeTrue();

    private async Task<string> DisclosureAsync(Guid requestId, Guid viewer)
    {
        var result = await HelpRequests().GetHelpRequestLocationAsync(requestId, viewer);
        return result.IsSuccess ? result.Value!.Disclosure : "NOT_FOUND";
    }

    private static void ShouldNotContainExactHome(object value) =>
        JsonSerializer.Serialize(value).Should().NotContain("47.26917").And.NotContain("11.40433");

    // --- verification ----------------------------------------------------------

    [Fact]
    public async Task T01_T02_EmailAndPhoneVerification_ArePersistedFacts()
    {
        var both = NewPerson();
        var none = NewPerson(email: false, phone: false);

        var facts = await Facts().GetManyAsync([both, none]);

        facts[both].EmailVerified.Should().BeTrue();
        facts[both].PhoneVerified.Should().BeTrue();
        facts[none].EmailVerified.Should().BeFalse();
        facts[none].PhoneVerified.Should().BeFalse();
        (await _db.Users.AsNoTracking().SingleAsync(u => u.Id == both)).PhoneVerifiedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task T03_AddressVerification_IsPersisted_WithExplicitSourceVerifierAndOrganization()
    {
        var volunteer = await VolunteerAsync(rosterOf: _orgA);

        var recorded = await RecordAsync(_coordA, _orgA, volunteer, "ADDRESS", "DOCUMENT_SIGHTED");

        recorded.IsSuccess.Should().BeTrue();
        var row = await _db.Verifications.AsNoTracking().SingleAsync(v => v.UserId == volunteer);
        row.Type.Should().Be(VerificationType.Address);
        row.Method.Should().Be(VerificationMethod.DocumentSighted);
        row.VerifiedByUserId.Should().Be(_coordA);
        row.VerifiedByOrganizationId.Should().Be(_orgA);
        row.Provider.Should().Be(VerificationProvider.Organization);
        (await Facts().GetAsync(volunteer)).AddressVerified.Should().BeTrue();
    }

    [Fact]
    public async Task Geocoding_IsNotAddressVerification()
    {
        var volunteer = await VolunteerAsync();

        // The profile has a geocoded home (coordinates), but no staff fact.
        (await Facts().GetAsync(volunteer)).AddressVerified.Should().BeFalse();
    }

    [Fact]
    public async Task T04_OrganizationVerification_IsASeparateFact_ThatImpliesNothingElse()
    {
        var volunteer = await VolunteerAsync(email: true, phone: false, rosterOf: _orgA);

        (await RecordAsync(_coordA, _orgA, volunteer, "ORGANIZATION", "ORGANIZATION_MEMBERSHIP")).IsSuccess.Should().BeTrue();
        var facts = await Facts().GetAsync(volunteer);

        facts.OrganizationVerified.Should().BeTrue();
        facts.VerifiedByOrganizationIds.Should().Equal(_orgA);
        facts.PhoneVerified.Should().BeFalse("organization verification is not a phone check");
        facts.AddressVerified.Should().BeFalse("nor an address check");
        facts.Level.Should().Be(VerificationLevel.Email);
    }

    [Fact]
    public async Task T04_OrganizationVerification_LapsesWhenTheMembershipEnds()
    {
        var volunteer = await VolunteerAsync(rosterOf: _orgA);
        await RecordAsync(_coordA, _orgA, volunteer, "ORGANIZATION", "ORGANIZATION_MEMBERSHIP");

        (await _db.OrganizationMemberships.SingleAsync(m => m.UserId == volunteer)).Leave();
        await _db.SaveChangesAsync();

        (await Facts().GetAsync(volunteer)).OrganizationVerified.Should().BeFalse();
    }

    [Fact]
    public async Task T05_TheLevelIsComputed_FromTheOrderedFacts()
    {
        var none = NewPerson(email: false, phone: false);
        var email = NewPerson(email: true, phone: false);
        var phoneOnly = NewPerson(email: false, phone: true);
        var both = await VolunteerAsync(rosterOf: _orgA);
        var full = await VolunteerAsync(rosterOf: _orgA);
        await RecordAsync(_coordA, _orgA, full, "ADDRESS", "DOCUMENT_SIGHTED");
        var addressNoPhone = await VolunteerAsync(email: true, phone: false, rosterOf: _orgA);
        await RecordAsync(_coordA, _orgA, addressNoPhone, "ADDRESS", "ORGANIZATION_RECORD");

        var facts = await Facts().GetManyAsync([none, email, phoneOnly, both, full, addressNoPhone]);

        facts[none].Level.Should().Be(VerificationLevel.None);
        facts[email].Level.Should().Be(VerificationLevel.Email);
        facts[phoneOnly].Level.Should().Be(VerificationLevel.None, "the path starts with email");
        facts[both].Level.Should().Be(VerificationLevel.EmailPhone);
        facts[full].Level.Should().Be(VerificationLevel.EmailPhoneAddress);
        facts[addressNoPhone].Level.Should().Be(VerificationLevel.Email, "a later step never counts without the earlier ones");
    }

    [Fact]
    public async Task T06_NobodyCanVerifyThemselves_OrSomeoneOutsideTheirOrganization()
    {
        var volunteer = await VolunteerAsync(rosterOf: _orgA);
        var stranger = await VolunteerAsync();

        var self = await RecordAsync(volunteer, _orgA, volunteer, "ADDRESS", "DOCUMENT_SIGHTED");
        var plainStaff = await RecordAsync(_staffA, _orgA, volunteer, "ADDRESS", "DOCUMENT_SIGHTED");
        var otherTenant = await RecordAsync(_coordB, _orgB, volunteer, "ADDRESS", "DOCUMENT_SIGHTED");
        var notOnRoster = await RecordAsync(_coordA, _orgA, stranger, "ADDRESS", "DOCUMENT_SIGHTED");
        var wrongMethod = await RecordAsync(_coordA, _orgA, volunteer, "ADDRESS", "ORGANIZATION_MEMBERSHIP");
        var systemFact = await RecordAsync(_coordA, _orgA, volunteer, "PHONE", "DOCUMENT_SIGHTED");

        self.Error!.Kind.Should().Be(ErrorKind.Forbidden, "a volunteer member is not staff");
        plainStaff.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        otherTenant.Error!.Kind.Should().Be(ErrorKind.NotFound, "another organization's volunteer is invisible");
        notOnRoster.Error!.Kind.Should().Be(ErrorKind.NotFound);
        wrongMethod.Error!.Kind.Should().Be(ErrorKind.Validation);
        systemFact.Error!.Code.Should().Be(VerificationCodes.TypeInvalid);
        (await _db.Verifications.CountAsync()).Should().Be(0);
        (await Facts().GetAsync(volunteer)).Level.Should().Be(VerificationLevel.EmailPhone);
    }

    [Fact]
    public void T06_NoRequestContractCarriesAClientWritableLevelOrVerifiedFlag()
    {
        var forbidden = new[] { "isverified", "verified", "trustlevel", "verificationlevel", "organizationverified", "addressverified" };
        var requestTypes = new[]
        {
            typeof(RecordVerificationRequest), typeof(UpdateDiscoverabilityRequest), typeof(UpdateLocationSharingRequest),
            typeof(UpdateVolunteerProfileRequest), typeof(UpdateSupportProfileRequest), typeof(CreateBlockRequest),
        };

        requestTypes.SelectMany(t => t.GetProperties())
            .Select(p => p.Name.ToLowerInvariant())
            .Where(forbidden.Contains)
            .Should().BeEmpty();
        typeof(UpdateLocationSharingRequest).GetProperty("Level")!.PropertyType.Should().Be(typeof(string),
            "the only 'Level' a client sends is its own location-sharing choice, not a verification level");
    }

    [Fact]
    public async Task T07_BadgesStateTheFact_WhoAndWhen_NeverSafety()
    {
        var volunteer = await VolunteerAsync(rosterOf: _orgA);
        await RecordAsync(_coordA, _orgA, volunteer, "ADDRESS", "DOCUMENT_SIGHTED");
        await RecordAsync(_coordA, _orgA, volunteer, "ORGANIZATION", "ORGANIZATION_MEMBERSHIP");

        var mine = (await Verification().GetMineAsync(volunteer)).Value!;

        mine.Level.Should().Be("EMAIL_PHONE_ADDRESS");
        mine.Badges.Select(b => b.Fact).Should().BeEquivalentTo(
            ["EMAIL_VERIFIED", "PHONE_VERIFIED", "ADDRESS_VERIFIED", "ORGANIZATION_VERIFIED"]);
        var org = mine.Badges.Single(b => b.Fact == "ORGANIZATION_VERIFIED");
        org.OrganizationName.Should().Be("Sozialsprengel A");
        org.VerifiedAtUtc.Should().NotBeNull();
        JsonSerializer.Serialize(mine).ToUpperInvariant().Should().NotContain("SAFE").And.NotContain("TRUSTED");
        mine.NextStep.Should().BeNull();
    }

    [Fact]
    public async Task T08_ARevokedFact_StopsCountingImmediately()
    {
        var volunteer = await VolunteerAsync(rosterOf: _orgA);
        var fact = (await RecordAsync(_coordA, _orgA, volunteer, "ADDRESS", "DOCUMENT_SIGHTED")).Value!;

        var otherTenant = await Verification().RevokeAsync(_coordB, _orgB, fact.Id, new RevokeVerificationRequest("x"));
        var noReason = await Verification().RevokeAsync(_coordA, _orgA, fact.Id, new RevokeVerificationRequest(" "));
        var revoked = await Verification().RevokeAsync(_coordA, _orgA, fact.Id, new RevokeVerificationRequest("moved"));

        otherTenant.Error!.Kind.Should().Be(ErrorKind.NotFound);
        noReason.Error!.Kind.Should().Be(ErrorKind.Validation);
        revoked.Value!.Status.Should().Be("REVOKED");
        (await Facts().GetAsync(volunteer)).Level.Should().Be(VerificationLevel.EmailPhone);
        (await _db.Verifications.AsNoTracking().SingleAsync()).RevocationReason.Should().Be("moved", "kept as history");
    }

    [Fact]
    public async Task T08_MovingHome_InvalidatesTheAddressFact()
    {
        var volunteer = await VolunteerAsync(rosterOf: _orgA);
        await RecordAsync(_coordA, _orgA, volunteer, "ADDRESS", "DOCUMENT_SIGHTED");
        var profiles = new ProfileService(_db, new AddressVerificationInvalidator(_db));

        (await profiles.UpsertVolunteerProfileAsync(volunteer, new UpdateVolunteerProfileRequest(
            10, 3, false, false, true, 47.1, 11.2, "Hall", "6060"))).IsSuccess.Should().BeTrue();

        (await Facts().GetAsync(volunteer)).AddressVerified.Should().BeFalse();
    }

    // --- discovery -------------------------------------------------------------

    [Fact]
    public async Task T09_NewAndExistingUsers_AreNotDiscoverable_ByDefault()
    {
        var volunteer = await VolunteerAsync(discoverable: false);

        (await Discoverability().GetAsync(volunteer)).Value!.IsDiscoverable.Should().BeFalse();
        (await NearbyVolunteerCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task T10_T11_OnlyADiscoverableVerifiedVolunteer_AppearsInOpenDiscovery()
    {
        await VolunteerAsync(discoverable: false);
        (await NearbyVolunteerCountAsync()).Should().Be(0);

        await VolunteerAsync(discoverable: true);
        (await NearbyVolunteerCountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task T11_TurningDiscoveryOff_RemovesThePersonOnTheNextRequest()
    {
        var volunteer = await VolunteerAsync();
        (await NearbyVolunteerCountAsync()).Should().Be(1);

        await Discoverability().SetAsync(volunteer, new UpdateDiscoverabilityRequest(false));

        (await NearbyVolunteerCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task T12_T23_ABlockedVolunteer_NeverAppears_InEitherDirection()
    {
        var blockedByViewer = await VolunteerAsync();
        (await TrustSafety().BlockUserAsync(_viewer, new CreateBlockRequest(blockedByViewer))).IsSuccess.Should().BeTrue();
        (await NearbyVolunteerCountAsync()).Should().Be(0);

        var blockedTheViewer = await VolunteerAsync();
        await TrustSafety().BlockUserAsync(blockedTheViewer, new CreateBlockRequest(_viewer));
        (await NearbyVolunteerCountAsync()).Should().Be(0, "a block hides both people from each other");

        await TrustSafety().UnblockUserAsync(_viewer, blockedByViewer);
        (await NearbyVolunteerCountAsync()).Should().Be(1, "unblocking is the blocker's choice");
        (await TrustSafety().UnblockUserAsync(_viewer, blockedTheViewer)).Error!.Kind.Should().Be(ErrorKind.NotFound,
            "nobody can lift a block someone else created");
    }

    [Fact]
    public async Task Blocks_AreIdempotent_AndOnlyTheBlockersOwnAreListed()
    {
        var other = await VolunteerAsync();
        await TrustSafety().BlockUserAsync(_viewer, new CreateBlockRequest(other));
        await TrustSafety().BlockUserAsync(_viewer, new CreateBlockRequest(other));
        await TrustSafety().BlockUserAsync(other, new CreateBlockRequest(_viewer));

        (await _db.UserBlocks.CountAsync(b => b.BlockingUserId == _viewer)).Should().Be(1);
        (await TrustSafety().GetMyBlocksAsync(_viewer)).Value!.Should().ContainSingle(b => b.BlockedUserId == other);
        (await TrustSafety().GetMyBlocksAsync(other)).Value!.Should().ContainSingle(b => b.BlockedUserId == _viewer,
            "each person sees only the blocks they created");
    }

    [Fact]
    public async Task T13_T27_ADiscoveryRestriction_HidesTheSubject_ForEveryoneOrOnePerson()
    {
        var everyone = await VolunteerAsync();
        await RestrictAsync(_orgA, everyone, discovery: true);
        (await NearbyVolunteerCountAsync()).Should().Be(0);

        var pairOnly = await VolunteerAsync();
        var anotherViewer = NewPerson();
        _db.VolunteerProfiles.Add(VolunteerProfile.Create(anotherViewer, postalCode: "6020", latitude: HomeLat, longitude: HomeLon));
        await _db.SaveChangesAsync();
        await RestrictAsync(_orgA, pairOnly, counterpart: anotherViewer, discovery: true);

        (await NearbyVolunteerCountAsync()).Should().Be(1, "only the pair is restricted");
        (await NearbyVolunteerCountAsync(anotherViewer)).Should().Be(0);
    }

    [Fact]
    public async Task T14_InsufficientVerification_KeepsADiscoverablePersonOutOfOpenDiscovery()
    {
        await VolunteerAsync(email: true, phone: false, discoverable: true);

        (await NearbyVolunteerCountAsync()).Should().Be(0, "open discovery needs email + phone (decision V-1)");
    }

    [Fact]
    public async Task T14_ASuspendedAccount_NeverAppears()
    {
        var volunteer = await VolunteerAsync();
        (await _db.Users.SingleAsync(u => u.Id == volunteer)).Suspend();
        await _db.SaveChangesAsync();

        (await NearbyVolunteerCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task T15_ScenarioB_AnOrganizationVolunteer_FollowsOrganizationRules()
    {
        var senior = NewPerson();
        // On org A's roster, never switched discovery on, only email verified.
        var rosterVolunteer = await VolunteerAsync(email: true, phone: false, discoverable: false, rosterOf: _orgA);
        await RecordAsync(_coordA, _orgA, rosterVolunteer, "ORGANIZATION", "ORGANIZATION_MEMBERSHIP");
        var independent = await VolunteerAsync(discoverable: false);

        var orgARequest = await SeedRequestAsync(_orgA, senior);
        var orgBRequest = await SeedRequestAsync(_orgB, senior);

        (await CandidatesAsync(orgARequest)).Should().Contain(rosterVolunteer).And.NotContain(independent);
        (await CandidatesAsync(orgBRequest)).Should().NotContain(rosterVolunteer,
            "org A's roster is not a candidate pool for org B (no cross-tenant candidates)");

        // Organization-verified + discoverable: eligible in open discovery without the phone step.
        (await NearbyVolunteerCountAsync()).Should().Be(0);
        await Discoverability().SetAsync(rosterVolunteer, new UpdateDiscoverabilityRequest(true));
        (await NearbyVolunteerCountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Matching_UsesDiscoverableVerifiedVolunteers_ForCommunityRequests()
    {
        var senior = NewPerson();
        var eligible = await VolunteerAsync();
        var hidden = await VolunteerAsync(discoverable: false);
        var restricted = await VolunteerAsync();
        await RestrictAsync(null, restricted, assignment: true);

        var candidates = await CandidatesAsync(await SeedRequestAsync(null, senior));

        candidates.Should().Contain(eligible).And.NotContain(hidden).And.NotContain(restricted);
    }

    // --- location ----------------------------------------------------------------

    [Fact]
    public async Task VolunteerFeed_ShowsTheSeniorsArea_OnlyWithTheirConsent()
    {
        var senior = NewPerson();
        await SeedRequestAsync(null, senior);

        var hidden = (await Matching().GetVolunteerFeedAsync(_viewer)).Value!.Single();
        hidden.LocationPostalCode.Should().BeNull();
        hidden.LocationCity.Should().BeEmpty();
        hidden.DistanceBand.Should().NotBeNull("the band is still shown");

        await SetConsentAsync(senior, "APPROXIMATE");
        (await Matching().GetVolunteerFeedAsync(_viewer)).Value!.Single().LocationPostalCode.Should().Be("6020");

        await TrustSafety().BlockUserAsync(senior, new CreateBlockRequest(_viewer));
        (await Matching().GetVolunteerFeedAsync(_viewer)).Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task T16_T17_ScenarioA_FullVerification_OrOrganizationVerification_NeverGrantsPreciseLocation()
    {
        // Scenario A: email ✓ phone ✓ address ✓ (approved process) discoverable ✓.
        var volunteer = await VolunteerAsync(rosterOf: _orgA);
        await RecordAsync(_coordA, _orgA, volunteer, "ADDRESS", "DOCUMENT_SIGHTED");
        await RecordAsync(_coordA, _orgA, volunteer, "ORGANIZATION", "ORGANIZATION_MEMBERSHIP");
        (await NearbyVolunteerCountAsync()).Should().Be(1, "eligible for discovery");

        var senior = NewPerson();
        await SetConsentAsync(senior, "APPROXIMATE");
        var request = await SeedRequestAsync(_orgA, senior, assignedVolunteer: volunteer);

        (await DisclosureAsync(request.Id, volunteer)).Should().Be("APPROXIMATE", "only the senior's consent decides precision");
        var nearby = (await Proximity().FindNearbyVolunteersAsync(_viewer, 25)).Value!;
        nearby.Single().Latitude.Should().BeNull("the volunteer's own home stays hidden");
        JsonSerializer.Serialize(nearby).Should().NotContain("47.27531");
    }

    [Theory]
    [InlineData("coordinator")]
    [InlineData("admin")]
    [InlineData("staff")]
    [InlineData("officer")]
    public async Task T18_T19_T29_NoStaffOrOfficerRole_GetsPreciseLocation_EvenWithPreciseConsent(string role)
    {
        var senior = NewPerson();
        await SetConsentAsync(senior, "PRECISE");
        var request = await SeedRequestAsync(_orgA, senior, assignedVolunteer: await VolunteerAsync());
        var caller = role switch { "coordinator" => _coordA, "admin" => _adminA, "staff" => _staffA, _ => _officerA };

        (await DisclosureAsync(request.Id, caller)).Should().BeOneOf("APPROXIMATE", "NOT_FOUND");
        var detail = await HelpRequests().GetHelpRequestByIdAsync(request.Id, caller);
        if (detail.IsSuccess)
        {
            ShouldNotContainExactHome(detail.Value!);
            detail.Value!.LocationAddress.Should().BeNull("staff are not the assigned counterparty (BR-COMM-04)");
        }
    }

    [Fact]
    public async Task T20_DiscoveryAndLocationConsent_AreIndependent()
    {
        var senior = NewPerson();
        var volunteer = await VolunteerAsync();
        var request = await SeedRequestAsync(_orgA, senior, assignedVolunteer: volunteer);

        (await Discoverability().SetAsync(senior, new UpdateDiscoverabilityRequest(true))).IsSuccess.Should().BeTrue();
        (await DisclosureAsync(request.Id, volunteer)).Should().Be("APPROXIMATE", "being findable shares no location");

        await Discoverability().SetAsync(senior, new UpdateDiscoverabilityRequest(false));
        await SetConsentAsync(senior, "PRECISE");
        (await DisclosureAsync(request.Id, volunteer)).Should().Be("PRECISE");
        (await Discoverability().GetAsync(senior)).Value!.IsDiscoverable.Should().BeFalse("precise sharing makes nobody findable");
    }

    [Fact]
    public async Task T21_ScenarioE_ConsentRevocation_BlocksTheNextRequest()
    {
        var senior = NewPerson();
        var volunteer = await VolunteerAsync();
        await SetConsentAsync(senior, "PRECISE");
        var request = await SeedRequestAsync(_orgA, senior, assignedVolunteer: volunteer);
        (await HelpRequests().GetHelpRequestLocationAsync(request.Id, volunteer)).Value!.Latitude.Should().Be(HomeLat);

        await SetConsentAsync(senior, "NONE");

        var after = (await HelpRequests().GetHelpRequestLocationAsync(request.Id, volunteer)).Value!;
        after.Disclosure.Should().Be("APPROXIMATE");
        ShouldNotContainExactHome(after);
    }

    // --- block / safeguarding on protected data -----------------------------------

    [Fact]
    public async Task T24_ScenarioC_ABlockedViewer_LosesTheRequestAndItsLocation()
    {
        var senior = NewPerson();
        await SetConsentAsync(senior, "APPROXIMATE");
        var request = await SeedRequestAsync(null, senior);
        (await HelpRequests().GetOpenHelpRequestsAsync(viewerUserId: _viewer)).Value!.Should().ContainSingle();
        (await Proximity().FindNearbyRequestsAsync(_viewer, 25)).Value!.Should().ContainSingle();

        await TrustSafety().BlockUserAsync(senior, new CreateBlockRequest(_viewer));

        (await HelpRequests().GetOpenHelpRequestsAsync(viewerUserId: _viewer)).Value!.Should().BeEmpty();
        (await Proximity().FindNearbyRequestsAsync(_viewer, 25)).Value!.Should().BeEmpty();
        (await HelpRequests().GetHelpRequestByIdAsync(request.Id, _viewer)).Error!.Kind.Should().Be(ErrorKind.NotFound);
        (await DisclosureAsync(request.Id, _viewer)).Should().Be("NONE");
        (await Location().DecideAsync(senior, _viewer, LocationRelationship.DiscoveryViewer))
            .Should().Be(LocationDisclosure.None);
    }

    [Fact]
    public async Task T24_T37_ABlockDuringALiveAssignment_WithholdsAddressAndContact()
    {
        var senior = NewPerson();
        var volunteer = await VolunteerAsync();
        await SetConsentAsync(senior, "PRECISE");
        var request = await SeedRequestAsync(_orgA, senior, assignedVolunteer: volunteer);
        (await HelpRequests().GetHelpRequestByIdAsync(request.Id, volunteer)).Value!.LocationAddress.Should().Be(VisitAddress);

        await TrustSafety().BlockUserAsync(senior, new CreateBlockRequest(volunteer));

        var detail = (await HelpRequests().GetHelpRequestByIdAsync(request.Id, volunteer)).Value!;
        detail.LocationAddress.Should().BeNull();
        detail.SeniorPhone.Should().BeNull();
        (await DisclosureAsync(request.Id, volunteer)).Should().Be("NONE");
        var seniorView = (await HelpRequests().GetHelpRequestByIdAsync(request.Id, senior)).Value!;
        seniorView.VolunteerPhone.Should().BeNull("contact is withheld in both directions");
    }

    [Fact]
    public async Task T28_ScenarioD_ALocationRestriction_WithholdsLocation_WithNoRoleBypass()
    {
        var senior = NewPerson();
        var volunteer = await VolunteerAsync();
        await SetConsentAsync(senior, "PRECISE");
        var request = await SeedRequestAsync(_orgA, senior, assignedVolunteer: volunteer);
        (await DisclosureAsync(request.Id, volunteer)).Should().Be("PRECISE");

        await RestrictAsync(_orgA, volunteer, location: true);

        (await DisclosureAsync(request.Id, volunteer)).Should().Be("NONE");
        (await DisclosureAsync(request.Id, _officerA)).Should().NotBe("PRECISE", "the officer role is not a location exception");
        (await DisclosureAsync(request.Id, senior)).Should().Be("PRECISE", "the senior always sees their own request");
    }

    [Fact]
    public async Task T37_AContactRestriction_WithholdsTheVisitAddress()
    {
        var senior = NewPerson();
        var volunteer = await VolunteerAsync();
        var request = await SeedRequestAsync(_orgA, senior, assignedVolunteer: volunteer);

        await RestrictAsync(_orgA, volunteer, counterpart: senior, contact: true);

        var detail = (await HelpRequests().GetHelpRequestByIdAsync(request.Id, volunteer)).Value!;
        detail.LocationAddress.Should().BeNull();
        detail.SeniorPhone.Should().BeNull();
    }

    [Fact]
    public async Task AnAssignmentRestriction_BlocksAccepting_WithoutRevealingWhy()
    {
        var senior = NewPerson();
        var volunteer = await VolunteerAsync();
        var request = await SeedRequestAsync(null, senior);
        await RestrictAsync(null, volunteer, assignment: true);

        var accept = await HelpRequests().AcceptHelpRequestAsync(request.Id, volunteer, new AcceptHelpRequestRequest(request.RowVersion));

        accept.Error!.Code.Should().Be("ASSIGNMENT_NOT_PERMITTED");
        accept.Error.Detail.Should().NotContainAny("safeguarding", "concern", "case");
    }

    [Fact]
    public async Task T30_RestrictionsCannotBeManipulatedAcrossTenants_OrByNonOfficers()
    {
        var subject = await VolunteerAsync();
        var caseA = await OpenCaseAsync(_orgA, subject);

        var byOfficerB = await Safeguarding().ImposeRestrictionAsync(caseA, _officerB,
            new ImposeRestrictionRequest(null, true, true, true, true));
        var byCoordinatorA = await Safeguarding().ImposeRestrictionAsync(caseA, _coordA,
            new ImposeRestrictionRequest(null, true, true, true, true));
        byOfficerB.Error!.Kind.Should().Be(ErrorKind.NotFound);
        byCoordinatorA.Error!.Kind.Should().Be(ErrorKind.NotFound, "a coordinator holds no officer scope (BR-SG-02)");

        var imposed = (await Safeguarding().ImposeRestrictionAsync(caseA, _officerA,
            new ImposeRestrictionRequest(null, true, false, false, false))).Value!;
        (await Safeguarding().LiftRestrictionAsync(imposed.Id, _officerB, new LiftRestrictionRequest("x")))
            .Error!.Kind.Should().Be(ErrorKind.NotFound);
        (await Safeguarding().GetRestrictionsAsync(caseA, _officerB)).Error!.Kind.Should().Be(ErrorKind.NotFound);
        imposed.SubjectUserId.Should().Be(subject, "the subject comes from the case row, never the request body");

        (await Safeguarding().LiftRestrictionAsync(imposed.Id, _officerA, new LiftRestrictionRequest("resolved")))
            .IsSuccess.Should().BeTrue();
        (await Restrictions().GetActiveAsync([subject])).Should().BeEmpty();
        (await _sg.SafeguardingAccessLogs.CountAsync(l => l.CaseId == caseA)).Should().BeGreaterThanOrEqualTo(2, "every officer action is logged");
    }

    // --- visit address (ADR-026: BR-COMM-04 unchanged, hardening only) --------------

    [Fact]
    public async Task T34_ScenarioF_TheLiveAssignee_ReceivesTheVisitAddress_AsBrComm04Says()
    {
        var senior = NewPerson();
        var volunteer = await VolunteerAsync();
        var request = await SeedRequestAsync(_orgA, senior, assignedVolunteer: volunteer);

        var detail = (await HelpRequests().GetHelpRequestByIdAsync(request.Id, volunteer)).Value!;

        detail.LocationAddress.Should().Be(VisitAddress);
        ShouldNotContainExactHome(detail);
    }

    [Fact]
    public async Task T35_NobodyElse_ObtainsTheVisitAddress()
    {
        var senior = NewPerson();
        var request = await SeedRequestAsync(_orgA, senior, assignedVolunteer: await VolunteerAsync());

        foreach (var caller in new[] { _coordA, _adminA, _staffA, _officerA, _coordB, _viewer })
        {
            var detail = await HelpRequests().GetHelpRequestByIdAsync(request.Id, caller);
            if (detail.IsSuccess)
            {
                detail.Value!.LocationAddress.Should().BeNull();
            }
        }
    }

    [Fact]
    public async Task T36_T38_ScenarioF_CancellationEndsAccess_AndBothStepsAreAudited()
    {
        var senior = NewPerson();
        var volunteer = await VolunteerAsync();
        await SetConsentAsync(senior, "PRECISE");
        var request = await SeedRequestAsync(null, senior);

        var accepted = await HelpRequests().AcceptHelpRequestAsync(request.Id, volunteer, new AcceptHelpRequestRequest(request.RowVersion));
        accepted.IsSuccess.Should().BeTrue();
        accepted.Value!.LocationAddress.Should().Be(VisitAddress);
        (await DisclosureAsync(request.Id, volunteer)).Should().Be("PRECISE");

        (await HelpRequests().CancelHelpRequestAsync(request.Id, senior,
            new CancelHelpRequestRequest("Termin nicht mehr nötig", CancellationReasonCode.NeedsMetOtherwise))).IsSuccess.Should().BeTrue();

        var after = await HelpRequests().GetHelpRequestByIdAsync(request.Id, volunteer);
        if (after.IsSuccess)
        {
            after.Value!.LocationAddress.Should().BeNull("a cancelled request has no assigned counterparty");
            after.Value.SeniorPhone.Should().BeNull();
            ShouldNotContainExactHome(after.Value);
        }
        (await DisclosureAsync(request.Id, volunteer)).Should().NotBe("PRECISE");

        var history = await _db.HelpRequestStatusHistories.AsNoTracking().Where(h => h.HelpRequestId == request.Id).ToListAsync();
        history.Should().Contain(h => h.ToStatus == HelpRequestStatus.Assigned);
        history.Should().Contain(h => h.ToStatus == HelpRequestStatus.Cancelled);
    }

    // --- tenant isolation matrix ---------------------------------------------------

    [Fact]
    public async Task TenantMatrix_OrgBStaff_CannotReachOrgA_Discovery_Location_Request_Safeguarding_OrAssignment()
    {
        var senior = NewPerson();
        await SetConsentAsync(senior, "PRECISE");
        var rosterA = await VolunteerAsync(discoverable: false, rosterOf: _orgA);
        var request = await SeedRequestAsync(_orgA, senior, assignedVolunteer: rosterA);
        var caseA = await OpenCaseAsync(_orgA, rosterA);

        foreach (var caller in new[] { _staffB, _coordB, _adminB })
        {
            (await Matching().GetCandidatesForCallerAsync(request.Id, caller)).Error!.Kind.Should().Be(ErrorKind.NotFound);
            (await Matching().GetHybridProposalsForCallerAsync(new HybridMatchingRequest(request.Id), caller))
                .Error!.Kind.Should().Be(ErrorKind.NotFound);
            (await DisclosureAsync(request.Id, caller)).Should().Be("NOT_FOUND");
            (await HelpRequests().GetHelpRequestByIdAsync(request.Id, caller)).Error!.Kind.Should().Be(ErrorKind.NotFound);
            (await Safeguarding().ImposeRestrictionAsync(caseA, caller, new ImposeRestrictionRequest(null, true, false, false, false)))
                .Error!.Kind.Should().Be(ErrorKind.NotFound);
            (await RecordAsync(caller, _orgB, rosterA, "ADDRESS", "DOCUMENT_SIGHTED")).IsSuccess.Should().BeFalse();
        }

        // Org A's own staff: area only, never the exact point.
        foreach (var caller in new[] { _coordA, _adminA })
        {
            (await DisclosureAsync(request.Id, caller)).Should().Be("APPROXIMATE");
        }
    }
}
