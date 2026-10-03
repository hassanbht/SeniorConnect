using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SeniorConnect.Domain;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.HelpRequests.Application;
using SeniorConnect.Modules.HelpRequests.Domain;
using SeniorConnect.Modules.HelpRequests.Infrastructure;
using SeniorConnect.Modules.Organizations.Contracts;
using SeniorConnect.Modules.Organizations.Domain;
using SeniorConnect.Modules.Organizations.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.HelpRequests.Tests;

/// <summary>
/// P2-12 (confirmation/dispute actually persisted, reviewed by the right
/// people), P2-10/14/15 (org-scoped logging, Vienna "today", staff as logger).
/// </summary>
public sealed class ActivityConfirmationAndLoggingTests : IDisposable
{
    private sealed class TestTenantContext : ITenantContext
    {
        public Guid? OrganizationId => null;
        public bool IsPlatformScope => true;
    }

    private readonly SeniorConnectDbContext _db;
    private readonly ActivityService _service;
    private readonly Guid _orgId;
    private readonly Guid _coordinatorId = Guid.NewGuid();
    private readonly Guid _volunteerId = Guid.NewGuid();
    private readonly Guid _otherMemberId = Guid.NewGuid();
    private readonly Guid _seniorId = Guid.NewGuid();
    private readonly Guid _strangerId = Guid.NewGuid();
    private readonly ActivityCategory _category;

    public ActivityConfirmationAndLoggingTests()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new SeniorConnectDbContext(options, new TestTenantContext());

        var org = Organization.Create("Testverein Musterstadt", OrganizationType.Ngo);
        _orgId = org.Id;
        _db.Organizations.Add(org);
        AddMember(_coordinatorId, MembershipRole.Coordinator);
        AddMember(_volunteerId, MembershipRole.Volunteer);
        AddMember(_otherMemberId, MembershipRole.Volunteer);

        _category = (ActivityCategory)Activator.CreateInstance(typeof(ActivityCategory), nonPublic: true)!;
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.Id))!.SetValue(_category, Guid.NewGuid());
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.Code))!.SetValue(_category, "shopping");
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.NameKey))!.SetValue(_category, "help.category.shopping");
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.DefaultSafetyLevel))!.SetValue(_category, 1);
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.IsActive))!.SetValue(_category, true);
        _db.ActivityCategories.Add(_category);
        _db.SaveChanges();

        var guard = new OrganizationAccessGuard(_db);
        _service = new ActivityService(_db, new MemoryCache(new MemoryCacheOptions()), guard, guard);
    }

    public void Dispose() => _db.Dispose();

    private void AddMember(Guid userId, MembershipRole role)
    {
        var membership = OrganizationMembership.Create(_orgId, userId, role);
        membership.Activate();
        _db.OrganizationMemberships.Add(membership);
    }

    private LogActivityRequest Request(Guid? organizationId) => new(
        OrganizationId: organizationId,
        SubjectUserId: _seniorId,
        CategoryId: _category.Id,
        OccurredOn: RosterStatusPolicy.TodayInVienna().AddDays(-1),
        DurationMinutes: 90,
        LocationType: LocationType.PublicPlace,
        Notes: null,
        InsuranceContext: InsuranceContext.OrganizationCovered);

    private async Task<Guid> LogOrgActivityAsync()
    {
        var logged = await _service.LogActivityAsync(_volunteerId, Request(_orgId));
        logged.IsSuccess.Should().BeTrue();
        _db.ChangeTracker.Clear();
        return logged.Value!.Id;
    }

    private async Task<ActivityStatus> StoredStatusAsync(Guid activityId)
    {
        _db.ChangeTracker.Clear();
        return (await _db.Activities.AsNoTracking().SingleAsync(a => a.Id == activityId)).Status;
    }

    // --- P2-12 --------------------------------------------------------------

    [Fact]
    public async Task Confirm_ByOrgCoordinator_IsPersisted()
    {
        // Arrange
        var activityId = await LogOrgActivityAsync();

        // Act
        var result = await _service.ConfirmActivityAsync(activityId, _coordinatorId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        (await StoredStatusAsync(activityId)).Should().Be(ActivityStatus.Confirmed);
    }

    [Fact]
    public async Task Confirm_BySubjectCounterpart_IsPersisted()
    {
        var activityId = await LogOrgActivityAsync();

        var result = await _service.ConfirmActivityAsync(activityId, _seniorId);

        result.IsSuccess.Should().BeTrue();
        (await StoredStatusAsync(activityId)).Should().Be(ActivityStatus.Confirmed);
    }

    [Fact]
    public async Task Confirm_ByVolunteerThemself_Is403_ByNonStaffMember_Is403_ByStranger_Is404()
    {
        // Arrange
        var activityId = await LogOrgActivityAsync();

        // Act
        var self = await _service.ConfirmActivityAsync(activityId, _volunteerId);
        var member = await _service.ConfirmActivityAsync(activityId, _otherMemberId);
        var stranger = await _service.ConfirmActivityAsync(activityId, _strangerId);

        // Assert
        self.Error!.Code.Should().Be("SELF_CONFIRMATION_FORBIDDEN");
        member.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        stranger.Error!.Kind.Should().Be(ErrorKind.NotFound);
        (await StoredStatusAsync(activityId)).Should().Be(ActivityStatus.Logged);
    }

    [Fact]
    public async Task Dispute_ByOrgCoordinator_IsPersisted_ByStranger_Is404()
    {
        // Arrange
        var activityId = await LogOrgActivityAsync();

        // Act
        var stranger = await _service.DisputeActivityAsync(activityId, _strangerId, new DisputeActivityRequest("falsch"));
        var coordinator = await _service.DisputeActivityAsync(activityId, _coordinatorId, new DisputeActivityRequest("Dauer stimmt nicht"));

        // Assert
        stranger.Error!.Kind.Should().Be(ErrorKind.NotFound);
        coordinator.IsSuccess.Should().BeTrue();
        (await StoredStatusAsync(activityId)).Should().Be(ActivityStatus.Disputed);
    }

    // --- P2-10 / P2-14 / P2-15 ------------------------------------------------

    [Fact]
    public async Task GetById_VisibleToVolunteerAndStaff_HiddenFromStrangersAndNonStaff()
    {
        var activityId = await LogOrgActivityAsync();

        Assert.True((await _service.GetActivityByIdAsync(activityId, _volunteerId)).IsSuccess);
        Assert.True((await _service.GetActivityByIdAsync(activityId, _coordinatorId)).IsSuccess);
        Assert.Equal(ErrorKind.NotFound, (await _service.GetActivityByIdAsync(activityId, _otherMemberId)).Error!.Kind);
        Assert.Equal(ErrorKind.NotFound, (await _service.GetActivityByIdAsync(activityId, _strangerId)).Error!.Kind);
    }

    [Fact]
    public async Task ListByOrganization_StaffOnly()
    {
        await LogOrgActivityAsync();

        Assert.True((await _service.GetOrganizationActivitiesAsync(_orgId, _coordinatorId)).IsSuccess);
        Assert.Equal(ErrorKind.Forbidden, (await _service.GetOrganizationActivitiesAsync(_orgId, _otherMemberId)).Error!.Kind);
        Assert.Equal(ErrorKind.NotFound, (await _service.GetOrganizationActivitiesAsync(_orgId, _strangerId)).Error!.Kind);
    }

    [Fact]
    public async Task Log_ForOrganizationTheVolunteerIsNotIn_Is404()
    {
        var result = await _service.LogActivityAsync(_strangerId, Request(_orgId));

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        (await _db.Activities.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Log_ByStaffForRosterVolunteer_RecordsStaffAsLogger()
    {
        var result = await _service.LogActivityAsync(
            _volunteerId, Request(_orgId), ActivitySource.CoordinatorLogged, loggedByUserId: _coordinatorId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.VolunteerUserId.Should().Be(_volunteerId);
        result.Value.LoggedByUserId.Should().Be(_coordinatorId);
    }

    [Fact]
    public async Task Log_ByStaffForNonRosterVolunteer_IsRejected_ByNonStaffForOther_Is403()
    {
        var nonRoster = await _service.LogActivityAsync(
            _strangerId, Request(_orgId), ActivitySource.CoordinatorLogged, loggedByUserId: _coordinatorId);
        var byPeer = await _service.LogActivityAsync(
            _volunteerId, Request(_orgId), ActivitySource.CoordinatorLogged, loggedByUserId: _otherMemberId);

        nonRoster.Error!.Code.Should().Be("VOLUNTEER_NOT_IN_ORGANIZATION");
        byPeer.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        (await _db.Activities.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Log_ForToday_UsesViennaCalendarDay()
    {
        var request = Request(organizationId: null) with { OccurredOn = RosterStatusPolicy.TodayInVienna() };

        var result = await _service.LogActivityAsync(_volunteerId, request);

        result.IsSuccess.Should().BeTrue("the Vienna date is never 'in the future' for a Vienna organization");
    }
}
