using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SeniorConnect.Domain;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.HelpRequests.Application;
using SeniorConnect.Modules.HelpRequests.Domain;
using SeniorConnect.Modules.HelpRequests.Infrastructure;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Contracts;
using SeniorConnect.Modules.Identity.Domain;
using SeniorConnect.Modules.Identity.Infrastructure;
using SeniorConnect.Modules.Organizations.Domain;
using SeniorConnect.Modules.Organizations.Infrastructure;
using SeniorConnect.Modules.Profiles.Application;
using SeniorConnect.Modules.Profiles.Domain;
using SeniorConnect.Modules.Profiles.Infrastructure;
using SeniorConnect.Modules.TrustSafety.Application;
using SeniorConnect.Modules.TrustSafety.Contracts;
using SeniorConnect.Modules.TrustSafety.Domain;
using SeniorConnect.Modules.TrustSafety.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.HelpRequests.Tests;

/// <summary>
/// Phase 5B: policy closure. These tests pin the outcomes that the existing
/// business rules already determine (classification A) and the documented
/// current behaviour of the decisions that are still open (C), so that a
/// future change cannot silently flip either:
///
/// - V-3  trust level and verification level are two separate ladders (ADR-027).
/// - S-1  neither a staff nor an officer role is a location exception (BR-DISC-06).
/// - S-4  organization cases are tenant-scoped; org-less cases are platform-level.
/// - B-1  (approved Option B, 2026-09-30) a block releases the live assignment;
///        it is idempotent and audited via the status history (BR-DISC-05).
/// - B-2  (approved Option A, 2026-09-30) an escalation is a flag; only a
///        manual officer conversion creates a safeguarding case and links it.
/// - ADR-026 (approved option C, 2026-09-30, legal validation pending):
///        the grant exists but is feature-gated OFF by default.
/// - BR-AUDIT-01: the audited events exist.
///
/// Nothing here changes production behaviour.
/// </summary>
public sealed class Phase5PolicyClosureTests : IDisposable
{
    private const string VisitAddress = "Maria-Theresien-Straße 7";
    private const double HomeLat = 47.26917;
    private const double HomeLon = 11.40433;

    private readonly SeniorConnectDbContext _db;
    private readonly SafeguardingDbContext _sg = Phase4TestSupport.NewSafeguardingDb();
    private readonly Guid _orgA;
    private readonly Guid _orgB;
    private readonly Guid _coordA = Guid.NewGuid();
    private readonly Guid _adminA = Guid.NewGuid();
    private readonly Guid _officerA = Guid.NewGuid();
    private readonly Guid _officerB = Guid.NewGuid();
    private int _phoneSeq;

    public Phase5PolicyClosureTests()
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
        AddMember(_orgA, _officerA, MembershipRole.SafeguardingOfficer);
        AddMember(_orgB, _officerB, MembershipRole.SafeguardingOfficer);
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
    private DiscoverabilityService Discoverability() => new(_db);
    private SafetyBoundaryReader Blocks() => new(_db);
    private SafeguardingRestrictionReader Restrictions() => new(_sg);
    private LocationSharingService Consent() => new(_db);
    private LocationVisibilityService Location() => new(_db, Blocks(), Restrictions());
    private TrustSafetyService TrustSafety(IAssignmentBlockEnforcementPort? enforcement = null) =>
        new(_db, new OrganizationCoordinatorReader(_db), enforcement);
    private SafeguardingService Safeguarding(SeniorConnect.Modules.Organizations.Contracts.IOrganizationCoordinatorReader? orgReader = null) =>
        new(_sg, orgReader ?? new OrganizationCoordinatorReader(_db));
    private HelpRequestService HelpRequests(VisitAddressOptions? visitAddressOptions = null) => new(
        _db, new ActivitySafetyPolicy(), new TrustLevelReader(_db), Blocks(), new UserContactReader(_db),
        new VolunteerReliabilityUpdater(_db), familyPermissionReader: null, accessGuard: Guard(),
        locationVisibility: Location(), restrictions: Restrictions(),
        visitAddressOptions: visitAddressOptions is null ? null : Options.Create(visitAddressOptions));

    // --- seeding --------------------------------------------------------------

    private void AddMember(Guid orgId, Guid userId, MembershipRole role)
    {
        var membership = OrganizationMembership.Create(orgId, userId, role);
        membership.Activate();
        _db.OrganizationMemberships.Add(membership);
    }

    private User NewPerson(bool email = true, bool phone = true)
    {
        var user = User.CreateWithPhone($"+43660{++_phoneSeq:D7}", "Person");
        if (email) user.VerifyEmail();
        if (phone) user.VerifyPhone();
        _db.Users.Add(user);
        _db.TrustLevelSnapshots.Add(TrustLevelSnapshot.Create(user.Id, 2, "{}"));
        _db.SaveChanges();
        return user;
    }

    private async Task<HelpRequest> SeedAssignedRequestAsync(Guid seniorUserId, Guid volunteerUserId)
    {
        var start = DateTimeOffset.UtcNow.AddHours(3);
        var request = HelpRequest.Create(
            organizationId: _orgA, seniorUserId: seniorUserId, createdByUserId: seniorUserId,
            categoryId: Guid.NewGuid(), safetyLevel: 1, trustLevel: 1, scheduledStartUtc: start,
            scheduledEndUtc: start.AddHours(1), durationMinutes: 60, locationType: LocationType.SeniorHome,
            address: VisitAddress, postalCode: "6020", city: "Innsbruck", latitude: HomeLat, longitude: HomeLon).Value!;
        request.Assign(volunteerUserId, request.RowVersion).IsSuccess.Should().BeTrue();
        _db.HelpRequests.Add(request);
        await _db.SaveChangesAsync();
        return request;
    }

    // --- V-3: two separate ladders --------------------------------------------

    [Fact]
    public async Task V3_OrganizationVerification_IsAFact_AndNeverEntersTheComputedTrustLadder()
    {
        var volunteer = NewPerson();
        AddMember(_orgA, volunteer.Id, MembershipRole.Volunteer);
        await _db.SaveChangesAsync();

        var calculator = new TrustLevelCalculator();
        calculator.Evaluate(volunteer, []).Level.Should().Be(2,
            "email and phone are level 2 on the computed ladder (ADR-027)");

        // Staff record an organization fact (BR-DISC-04: a separate fact).
        var guard = Guard();
        var service = new VerificationService(_db, new VerificationFactsReader(_db, guard), guard, guard);
        (await service.RecordAsync(_coordA, _orgA, volunteer.Id,
                new RecordVerificationRequest("ORGANIZATION", "ORGANIZATION_MEMBERSHIP")))
            .IsSuccess.Should().BeTrue();

        var facts = await Facts().GetAsync(volunteer.Id);
        facts.OrganizationVerified.Should().BeTrue("the organization vouches for this person");
        facts.PhoneVerified.Should().BeTrue();
        facts.AddressVerified.Should().BeFalse("organization verification is not an address check");
        facts.Level.Should().Be(VerificationLevel.EmailPhone, "organization is not a step on the self-verification ladder");

        var rows = await _db.Verifications.Where(v => v.UserId == volunteer.Id).ToListAsync();
        calculator.Evaluate(volunteer, rows).Level.Should().Be(2,
            "organization verification never enters the computed ladder (ADR-027: two concepts, never merged)");
    }

    [Fact]
    public async Task V3_IdentityVerification_MovesTheTrustLadder_ButNotTheDiscoveryLadder()
    {
        var person = NewPerson();

        var address = Verification
            .RecordManual(person.Id, VerificationType.Address, VerificationMethod.DocumentSighted, _coordA, _orgA)
            .Value!;
        var identity = Verification.Create(person.Id, VerificationType.Identity);
        identity.Verify(_coordA);
        _db.Verifications.AddRange(address, identity);
        await _db.SaveChangesAsync();

        new TrustLevelCalculator().Evaluate(person, [address, identity]).Level.Should().Be(4,
            "address is step 3 and identity step 4 on the computed ladder");
        (await Facts().GetAsync(person.Id)).Level.Should().Be(VerificationLevel.EmailPhoneAddress,
            "identity is not part of the discovery self-verification ladder: same person, two different concepts");
    }

    // --- S-1: no role is a location exception ---------------------------------

    [Fact]
    public async Task S1_NeitherStaffNorOfficerRole_LiftsLocationConsent()
    {
        var person = NewPerson();
        var viewer = NewPerson();
        await Consent().SetAsync(person.Id, new UpdateLocationSharingRequest("PRECISE", LocationSharingConsent.CurrentVersion));

        // A restriction only withholds (BR-DISC-06); it can never add access.
        var sCase = SafeguardingCase.Raise(_coordA, person.Id, "concern", organizationId: _orgA).Value!;
        _sg.SafeguardingCases.Add(sCase);
        await _sg.SaveChangesAsync();
        (await Safeguarding().ImposeRestrictionAsync(sCase.Id, _officerA,
                new ImposeRestrictionRequest(null, false, true, false, false)))
            .IsSuccess.Should().BeTrue();

        (await Location().DecideAsync(person.Id, viewer.Id, LocationRelationship.DiscoveryViewer))
            .Should().Be(LocationDisclosure.None, "a location restriction withholds for everyone");
        (await Location().DecideAsync(person.Id, _officerA, LocationRelationship.OrganizationStaff))
            .Should().Be(LocationDisclosure.None, "the safeguarding officer role is not a location exception (BR-DISC-06)");
    }

    // --- S-4: scoping boundary ------------------------------------------------

    [Fact]
    public async Task S4_OrganizationCasesAreTenantScoped_OrgLessCasesArePlatformLevel()
    {
        var subject = NewPerson();
        var caseA = SafeguardingCase.Raise(_coordA, subject.Id, "concern in A", organizationId: _orgA).Value!;
        var caseB = SafeguardingCase.Raise(_coordA, subject.Id, "concern in B", organizationId: _orgB).Value!;
        var caseNoOrg = SafeguardingCase.Raise(_coordA, subject.Id, "neighbourly concern").Value!;
        _sg.SafeguardingCases.AddRange(caseA, caseB, caseNoOrg);
        await _sg.SaveChangesAsync();

        var visibleToOfficerA = (await Safeguarding().GetCasesAsync(null, _officerA)).Value!.Select(c => c.Id).ToList();
        visibleToOfficerA.Should().BeEquivalentTo([caseA.Id],
            "an organization case is tenant-scoped (S-4); org-less cases are PLATFORM_LEVEL and are no longer implicitly visible to an organization officer");
        visibleToOfficerA.Should().NotContain(caseB.Id, "organization B's case is invisible to organization A");
        visibleToOfficerA.Should().NotContain(caseNoOrg.Id,
            "S-4 (approved Option B): org-less cases are visible only to the explicit PlatformSafeguardingOfficer role");

        // An explicit platform officer sees the org-less case plus their own org's cases.
        var platformReader = new PlatformOfficerOrgReader(new OrganizationCoordinatorReader(_db), _officerA);
        var visibleToPlatformOfficer = (await Safeguarding(platformReader).GetCasesAsync(null, _officerA))
            .Value!.Select(c => c.Id).ToList();
        visibleToPlatformOfficer.Should().BeEquivalentTo([caseA.Id, caseNoOrg.Id],
            "the PlatformSafeguardingOfficer role grants org-less (platform-level) case visibility (S-4)");
        visibleToPlatformOfficer.Should().NotContain(caseB.Id, "the platform role does not open other tenants' cases");

        (await Safeguarding().ImposeRestrictionAsync(caseB.Id, _officerA,
                new ImposeRestrictionRequest(null, true, false, false, false)))
            .Error!.Kind.Should().Be(ErrorKind.NotFound, "a cross-tenant restriction cannot be imposed");

        // BR-SG-02: OrganizationAdmin is not a safeguarding officer. The service
        // therefore grants an admin no organization scope; the officer capability
        // gate on the endpoint is what refuses the request outright.
        var visibleToAdminA = (await Safeguarding().GetCasesAsync(null, _adminA)).Value!.Select(c => c.Id).ToList();
        visibleToAdminA.Should().NotContain([caseA.Id, caseB.Id],
            "an OrganizationAdmin has no officer scope (BR-SG-02)");
    }

    /// <summary>Wraps the real coordinator reader but marks one user as an explicit PlatformSafeguardingOfficer (S-4).</summary>
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

    // --- B-1: block releases the assignment (approved Option B, 2026-09-30) ---

    [Fact]
    public async Task B1_A_Block_ReleasesTheLiveAssignment_BackToOpen()
    {
        var senior = NewPerson();
        var volunteer = NewPerson();
        var request = await SeedAssignedRequestAsync(senior.Id, volunteer.Id);

        (await HelpRequests().GetHelpRequestByIdAsync(request.Id, volunteer.Id)).Value!.LocationAddress
            .Should().Be(VisitAddress, "the live assignee of a confirmed assignment receives the visit address (BR-COMM-04)");

        (await TrustSafety(new AssignmentBlockEnforcement(_db))
                .BlockUserAsync(volunteer.Id, new CreateBlockRequest(senior.Id, "felt unsafe")))
            .IsSuccess.Should().BeTrue();

        var reloaded = await _db.HelpRequests.AsNoTracking().SingleAsync(r => r.Id == request.Id);
        reloaded.AssignedVolunteerUserId.Should().BeNull(
            "B-1 approved: a block releases the live assignment between the pair (BR-DISC-05)");
        reloaded.Status.Should().Be(HelpRequestStatus.Open,
            "the request returns to the open pool so a safe replacement volunteer can be matched");

        // BR-AUDIT-01: the release is auditable as a status transition; the
        // reason states the safety cause without naming the block counterpart.
        var history = await _db.HelpRequestStatusHistories.AsNoTracking()
            .Where(h => h.HelpRequestId == request.Id).ToListAsync();
        history.Should().Contain(h => h.ToStatus == HelpRequestStatus.Open && h.Reason != null,
            "the safety release leaves a durable status-history audit trail");

        // The released volunteer is no longer a party and is blocked from the
        // senior, so the request disappears for them entirely.
        (await HelpRequests().GetHelpRequestByIdAsync(request.Id, volunteer.Id))
            .IsFailure.Should().BeTrue("the released volunteer no longer sees the request at all");
    }

    [Fact]
    public async Task B1_A_Block_IsIdempotent_AndLeavesNoResidueOnRepeat()
    {
        var senior = NewPerson();
        var volunteer = NewPerson();
        var request = await SeedAssignedRequestAsync(senior.Id, volunteer.Id);

        (await TrustSafety(new AssignmentBlockEnforcement(_db))
                .BlockUserAsync(volunteer.Id, new CreateBlockRequest(senior.Id, "first block")))
            .IsSuccess.Should().BeTrue();

        // A duplicate block (same pair) must not fail and must not double-release.
        (await TrustSafety(new AssignmentBlockEnforcement(_db))
                .BlockUserAsync(volunteer.Id, new CreateBlockRequest(senior.Id, "duplicate block")))
            .IsSuccess.Should().BeTrue();

        var releases = await _db.HelpRequestStatusHistories.AsNoTracking()
            .Where(h => h.HelpRequestId == request.Id
                        && h.FromStatus == HelpRequestStatus.Assigned
                        && h.ToStatus == HelpRequestStatus.Open)
            .CountAsync();
        releases.Should().Be(1, "the release runs exactly once per live assignment, even across duplicate blocks");
    }

    // --- B-2: report escalation is a flag, not a case -------------------------

    [Fact]
    public async Task B2_AnUnsafeBehaviorReport_IsAFlag_OnTheReport_NotASafeguardingCase()
    {
        var reporter = NewPerson();
        var subject = NewPerson();

        // The community module escalates on the report row itself and has no
        // path that writes a safeguarding case (BR-SG-01: safeguarding is not
        // an extension of ordinary reporting).
        var report = SeniorConnect.Modules.Community.Domain.CommunityReport.Create(
            reporter.Id, SeniorConnect.Modules.Community.Domain.CommunityReportTargetType.Member,
            subject.Id, SeniorConnect.Modules.Community.Domain.CommunityReportCategory.UnsafeBehavior,
            "unsafe behaviour").Value!;

        report.Status.Should().Be(SeniorConnect.Modules.Community.Domain.CommunityReportStatus.Open);
        report.EscalateToSafeguarding(reporter.Id, "Auto-escalated based on report category.");
        report.Status.Should().Be(SeniorConnect.Modules.Community.Domain.CommunityReportStatus.EscalatedToSafeguarding);

        (await _sg.SafeguardingCases.CountAsync()).Should().Be(0,
            "B-2: escalation alone never creates a safeguarding case — only the manual officer conversion does");

        // Until an officer converts, the report carries no case link.
        report.SafeguardingCaseId.Should().BeNull("the conversion is a separate manual officer action (approved Option A)");
    }

    // --- ADR-026 (approved option C, legal validation pending) ----------------

    [Fact]
    public async Task VisitAddress_ADR026Grant_IsGatedOffByDefault_SoLiveAssigneeStillSeesTheAddress()
    {
        var senior = NewPerson();
        var volunteer = NewPerson();
        var request = await SeedAssignedRequestAsync(senior.Id, volunteer.Id);

        // No VisitAddressOptions configured → RequireGrant = false. The BR-COMM-04
        // baseline stays in force until the Legal/Data Protection Gate passes.
        var volunteerView = (await HelpRequests().GetHelpRequestByIdAsync(request.Id, volunteer.Id)).Value!;
        volunteerView.LocationAddress.Should().Be(VisitAddress,
            "ADR-026 option C is feature-gated OFF by default: no grant required yet (legal validation pending)");

        // And the request model has no live grants for this assignment.
        _db.VisitAddressGrants.Local.Should().BeEmpty("no grant flow has run");
    }

    [Fact]
    public async Task VisitAddress_WhenTheGateIsOn_OnlyALiveGrantReleasesTheAddress()
    {
        var senior = NewPerson();
        var volunteer = NewPerson();
        var request = await SeedAssignedRequestAsync(senior.Id, volunteer.Id);
        var gated = HelpRequests(new VisitAddressOptions { RequireGrant = true });

        (await gated.GetHelpRequestByIdAsync(request.Id, volunteer.Id)).Value!.LocationAddress
            .Should().BeNull("with the gate on, the volunteer sees no address before the senior grants it");

        (await gated.GrantVisitAddressAsync(request.Id, senior.Id, new GrantVisitAddressRequest()))
            .IsSuccess.Should().BeTrue();

        (await gated.GetHelpRequestByIdAsync(request.Id, volunteer.Id)).Value!.LocationAddress
            .Should().Be(VisitAddress, "a live grant releases the address to the assigned volunteer");
    }

    [Fact]
    public async Task VisitAddress_WhenTheGateIsOn_ABlockStillWithholds_EvenWithALiveGrant()
    {
        var senior = NewPerson();
        var volunteer = NewPerson();
        var request = await SeedAssignedRequestAsync(senior.Id, volunteer.Id);
        var gated = HelpRequests(new VisitAddressOptions { RequireGrant = true });

        (await gated.GrantVisitAddressAsync(request.Id, senior.Id, new GrantVisitAddressRequest()))
            .IsSuccess.Should().BeTrue();

        // B-1 releases the assignment; the (now former) volunteer is blocked
        // and no longer a party, so the request vanishes for them. A grant
        // never overrides the block rule: gates only subtract, never add.
        (await TrustSafety(new AssignmentBlockEnforcement(_db))
                .BlockUserAsync(volunteer.Id, new CreateBlockRequest(senior.Id, "felt unsafe")))
            .IsSuccess.Should().BeTrue();

        (await gated.GetHelpRequestByIdAsync(request.Id, volunteer.Id))
            .IsFailure.Should().BeTrue("a live grant cannot keep a blocked, released volunteer in the request");
    }

    // --- BR-AUDIT-01 ----------------------------------------------------------

    [Fact]
    public async Task Audit_DiscoverabilityConsentVerificationAndBlock_AreAllRecorded()
    {
        var person = NewPerson();
        AddMember(_orgA, person.Id, MembershipRole.Volunteer);
        await _db.SaveChangesAsync();

        (await Discoverability().SetAsync(person.Id, new UpdateDiscoverabilityRequest(true))).IsSuccess.Should().BeTrue();
        (await Consent().SetAsync(person.Id,
            new UpdateLocationSharingRequest("APPROXIMATE", LocationSharingConsent.CurrentVersion))).IsSuccess.Should().BeTrue();

        var guard = Guard();
        var verification = new VerificationService(_db, new VerificationFactsReader(_db, guard), guard, guard);
        var recorded = await verification.RecordAsync(_coordA, _orgA, person.Id,
            new RecordVerificationRequest("ADDRESS", "DOCUMENT_SIGHTED"));
        (await verification.RevokeAsync(_coordA, _orgA, recorded.Value!.Id, new RevokeVerificationRequest("no longer valid")))
            .IsSuccess.Should().BeTrue();

        (await TrustSafety().BlockUserAsync(person.Id, new CreateBlockRequest(_officerA, "test"))).IsSuccess.Should().BeTrue();

        var actions = await _db.AuditEntries.AsNoTracking().Select(a => a.Action).ToListAsync();
        actions.Should().Contain("CREATE_DISCOVERYPREFERENCE", "a discoverability change is audited (BR-AUDIT-01)");
        actions.Should().Contain("CREATE_LOCATIONSHARINGCONSENT", "a location-consent change is audited");
        actions.Should().Contain("CREATE_VERIFICATION", "recording a verification fact is audited");
        actions.Should().Contain("UPDATE_VERIFICATION", "a verification revocation is an audited update");
        actions.Should().Contain("CREATE_USERBLOCK", "a block is audited");
    }
}
