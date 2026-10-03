using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Community.Application;
using SeniorConnect.Modules.Community.Domain;
using SeniorConnect.Modules.Community.Infrastructure;
using SeniorConnect.Modules.Organizations.Domain;
using SeniorConnect.Modules.Organizations.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Community.Tests;

/// <summary>
/// Phase 4: private event venues (docs/security/location-consent-and-visibility.md §12)
/// and report audit / non-disclosure (docs/security/block-report-policy.md).
/// Tnn refers to the Phase 4 test list.
/// </summary>
public sealed class Phase4VenueAndReportTests
{
    private const string HomeAddress = "Anichstraße 12, Top 4";

    private readonly SeniorConnectDbContext _db;
    private readonly CommunityService _service;
    private readonly Guid _orgId;
    private readonly Guid _coordinatorId = Guid.NewGuid();

    public Phase4VenueAndReportTests()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new SeniorConnectDbContext(options, new DefaultTenantContext { IsPlatformScope = true });
        _service = new CommunityService(_db, new RuleBasedCommunityModerationService(), new OrganizationCoordinatorReader(_db),
            new FakeUserAccountStatusReader(), new FakeTrustLevelReader(), new FakeSafeguardingCaseReader());

        var org = Organization.Create("Testverein Musterstadt", OrganizationType.Ngo);
        _orgId = org.Id;
        _db.Organizations.Add(org);
        var membership = OrganizationMembership.Create(org.Id, _coordinatorId, MembershipRole.Coordinator);
        membership.Activate();
        _db.OrganizationMemberships.Add(membership);
        _db.SaveChanges();
    }

    private async Task<Guid> CreateOrgEventAsync(EventVenueType venue) =>
        (await _service.CreateEventAsync(_coordinatorId, new CreateCommunityEventRequest(
            Title: "Erzählcafé",
            Description: "Gemeinsam plaudern",
            StartsAtUtc: DateTimeOffset.UtcNow.AddDays(3),
            EndsAtUtc: DateTimeOffset.UtcNow.AddDays(3).AddHours(2),
            OrganizationId: _orgId,
            LocationAddress: HomeAddress,
            LocationPostalCode: "6020",
            VenueType: venue))).Value!.Id;

    [Fact]
    public async Task T31_PrivateVenueAddress_IsHidden_FromViewersWhoAreNotConfirmed()
    {
        var eventId = await CreateOrgEventAsync(EventVenueType.Private);
        var stranger = Guid.NewGuid();

        var strangerView = (await _service.GetEventByIdAsync(eventId, stranger)).Value!;
        var listView = (await _service.GetEventsAsync(requestingUserId: stranger)).Value!.Single();

        strangerView.LocationAddress.Should().BeNull();
        strangerView.LocationPostalCode.Should().Be("6020", "the area stays visible for planning");
        listView.LocationAddress.Should().BeNull();
        JsonSerializer.Serialize(listView).Should().NotContain("Anichstraße");
    }

    [Fact]
    public async Task T31_PrivateVenueAddress_ReachesConfirmedAttendees_AndManagers()
    {
        var eventId = await CreateOrgEventAsync(EventVenueType.Private);
        var attendee = Guid.NewGuid();
        (await _service.RegisterForEventAsync(eventId, attendee, new RegisterEventRequest())).IsSuccess.Should().BeTrue();

        (await _service.GetEventByIdAsync(eventId, attendee)).Value!.LocationAddress.Should().Be(HomeAddress);
        (await _service.GetMyScheduleAsync(attendee)).Value!.Single().LocationAddress.Should().Be(HomeAddress);
        (await _service.GetEventByIdAsync(eventId, _coordinatorId)).Value!.LocationAddress.Should().Be(HomeAddress);
    }

    [Fact]
    public async Task T31_NewEvents_DefaultToPrivate()
    {
        var created = await _service.CreateEventAsync(_coordinatorId, new CreateCommunityEventRequest(
            Title: "Spaziergang", Description: "", StartsAtUtc: DateTimeOffset.UtcNow.AddDays(1),
            EndsAtUtc: DateTimeOffset.UtcNow.AddDays(1).AddHours(1), OrganizationId: _orgId, LocationAddress: HomeAddress));

        created.Value!.VenueType.Should().Be(EventVenueType.Private);
        (await _service.GetEventByIdAsync(created.Value.Id, Guid.NewGuid())).Value!.LocationAddress.Should().BeNull();
    }

    [Fact]
    public async Task T32_PublicVenue_OfAnOrganizationEvent_RemainsUsable()
    {
        var eventId = await CreateOrgEventAsync(EventVenueType.Public);

        (await _service.GetEventByIdAsync(eventId, Guid.NewGuid())).Value!.LocationAddress.Should().Be(HomeAddress);
    }

    [Fact]
    public async Task T33_AnIndividualHost_CannotDeclareTheirPlaceAPublicVenue()
    {
        var hostId = Guid.NewGuid();
        var group = CommunityGroup.Create(hostId, CreatorType.Individual, "Nachbarn", "", GroupType.NeighborhoodGroup,
            GroupVisibility.Public).Value!;
        _db.CommunityGroups.Add(group);
        _db.GroupMemberships.Add(GroupMembership.CreateOwner(group.Id, hostId));
        await _db.SaveChangesAsync();

        var created = await _service.CreateEventAsync(hostId, new CreateCommunityEventRequest(
            Title: "Kaffee bei mir", Description: "", StartsAtUtc: DateTimeOffset.UtcNow.AddDays(2),
            EndsAtUtc: DateTimeOffset.UtcNow.AddDays(2).AddHours(1), GroupId: group.Id,
            LocationAddress: HomeAddress, VenueType: EventVenueType.Public));

        created.IsFailure.Should().BeTrue();
        created.Error!.Code.Should().Be("EVENT_PUBLIC_VENUE_REQUIRES_ORGANIZATION");

        // Nor by editing an existing private event afterwards.
        var privateEvent = (await _service.CreateEventAsync(hostId, new CreateCommunityEventRequest(
            Title: "Kaffee bei mir", Description: "", StartsAtUtc: DateTimeOffset.UtcNow.AddDays(2),
            EndsAtUtc: DateTimeOffset.UtcNow.AddDays(2).AddHours(1), GroupId: group.Id, LocationAddress: HomeAddress))).Value!;
        var edited = await _service.UpdateEventAsync(privateEvent.Id, hostId, new UpdateCommunityEventRequest(
            "Kaffee bei mir", "", "general", HomeAddress, null, null, EventVenueType.Public));
        edited.Error!.Code.Should().Be("EVENT_PUBLIC_VENUE_REQUIRES_ORGANIZATION");
    }

    [Fact]
    public async Task T25_T26_AReportIsAudited_AndNotVisibleToTheReportedPerson()
    {
        var ownerId = Guid.NewGuid();
        var reportedId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();
        var group = CommunityGroup.Create(ownerId, CreatorType.Individual, "Nachbarn", "", GroupType.NeighborhoodGroup,
            GroupVisibility.Public).Value!;
        _db.CommunityGroups.Add(group);
        _db.GroupMemberships.Add(GroupMembership.CreateOwner(group.Id, ownerId));
        _db.GroupMemberships.Add(GroupMembership.JoinDirectly(group.Id, reportedId));
        await _db.SaveChangesAsync();

        var report = await _service.CreateReportAsync(reporterId, new CreateCommunityReportRequest(
            CommunityReportTargetType.Member, reportedId, CommunityReportCategory.Harassment,
            "Schreibt mir ständig private Nachrichten", group.Id));

        report.IsSuccess.Should().BeTrue();
        (await _db.AuditEntries.AnyAsync(a => a.Action == "CREATE_COMMUNITYREPORT" && a.SubjectId == report.Value!.Id))
            .Should().BeTrue("report creation is audited (BR-AUDIT-01)");

        var reportedQueue = await _service.GetModerationQueueAsync(reportedId, isPlatformModerator: false);
        reportedQueue.IsFailure.Should().BeFalse();
        JsonSerializer.Serialize(reportedQueue.Value).Should().NotContain(report.Value!.Id.ToString())
            .And.NotContain(reporterId.ToString()).And.NotContain("private Nachrichten");
    }
}
