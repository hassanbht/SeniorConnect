using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Organizations.Contracts;
using SeniorConnect.Modules.Organizations.Domain;
using SeniorConnect.Modules.Organizations.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Organizations.Tests;

/// <summary>
/// authorization.md §2: non-member → 404, member without role → 403
/// CAPABILITY_MISSING, staff → success. Backs every coordinator, reporting,
/// intake-form and onboarding endpoint.
/// </summary>
public sealed class OrganizationAccessGuardTests
{
    internal static SeniorConnectDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<SeniorConnectDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            new DefaultTenantContext { IsPlatformScope = true });

    internal static OrganizationMembership AddMember(
        SeniorConnectDbContext db, Guid orgId, Guid userId, MembershipRole role, bool active = true)
    {
        var membership = OrganizationMembership.Create(orgId, userId, role);
        if (active) membership.Activate();
        db.OrganizationMemberships.Add(membership);
        return membership;
    }

    [Fact]
    public async Task EnsureStaff_ReturnsNotFound_ForNonMember()
    {
        // Arrange
        using var db = CreateDb();
        var org = Organization.Create("Org", OrganizationType.Ngo);
        db.Organizations.Add(org);
        await db.SaveChangesAsync();
        var guard = new OrganizationAccessGuard(db);

        // Act
        var result = await guard.EnsureStaffAsync(Guid.NewGuid(), org.Id);

        // Assert
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
    }

    [Fact]
    public async Task EnsureStaff_ReturnsNotFound_ForStaffOfAnotherOrganization()
    {
        using var db = CreateDb();
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        var coordinator = Guid.NewGuid();
        AddMember(db, orgA, coordinator, MembershipRole.Coordinator);
        await db.SaveChangesAsync();

        var result = await new OrganizationAccessGuard(db).EnsureStaffAsync(coordinator, orgB);

        result.Error!.Kind.Should().Be(ErrorKind.NotFound, "a cross-tenant miss is 404, never 403");
    }

    [Fact]
    public async Task EnsureStaff_ReturnsForbiddenWithCapability_ForVolunteerMember()
    {
        using var db = CreateDb();
        var orgId = Guid.NewGuid();
        var volunteer = Guid.NewGuid();
        AddMember(db, orgId, volunteer, MembershipRole.Volunteer);
        await db.SaveChangesAsync();

        var result = await new OrganizationAccessGuard(db).EnsureStaffAsync(volunteer, orgId);

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be("CAPABILITY_MISSING");
        result.Error.Extensions!["capability"].Should().Be(OrganizationAccessGuard.StaffCapability);
    }

    [Fact]
    public async Task EnsureStaff_ReturnsNotFound_ForSuspendedCoordinator()
    {
        using var db = CreateDb();
        var orgId = Guid.NewGuid();
        var coordinator = Guid.NewGuid();
        AddMember(db, orgId, coordinator, MembershipRole.Coordinator, active: false);
        await db.SaveChangesAsync();

        var result = await new OrganizationAccessGuard(db).EnsureStaffAsync(coordinator, orgId);

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
    }

    [Theory]
    [InlineData(MembershipRole.Coordinator)]
    [InlineData(MembershipRole.Admin)]
    public async Task EnsureStaff_Succeeds_ForCoordinatorOrAdmin(MembershipRole role)
    {
        using var db = CreateDb();
        var orgId = Guid.NewGuid();
        var staff = Guid.NewGuid();
        AddMember(db, orgId, staff, role);
        await db.SaveChangesAsync();

        var result = await new OrganizationAccessGuard(db).EnsureStaffAsync(staff, orgId);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureAdmin_ReturnsForbidden_ForCoordinator()
    {
        using var db = CreateDb();
        var orgId = Guid.NewGuid();
        var coordinator = Guid.NewGuid();
        AddMember(db, orgId, coordinator, MembershipRole.Coordinator);
        await db.SaveChangesAsync();

        var result = await new OrganizationAccessGuard(db).EnsureAdminAsync(coordinator, orgId);

        result.Error!.Code.Should().Be("CAPABILITY_MISSING");
    }

    [Fact]
    public async Task GetActiveVolunteerUserIds_ReturnsOnlyThisOrgsActiveVolunteers()
    {
        using var db = CreateDb();
        var orgId = Guid.NewGuid();
        var active = Guid.NewGuid();
        var invited = Guid.NewGuid();
        var coordinator = Guid.NewGuid();
        var otherOrgVolunteer = Guid.NewGuid();
        AddMember(db, orgId, active, MembershipRole.Volunteer);
        AddMember(db, orgId, invited, MembershipRole.Volunteer, active: false);
        AddMember(db, orgId, coordinator, MembershipRole.Coordinator);
        AddMember(db, Guid.NewGuid(), otherOrgVolunteer, MembershipRole.Volunteer);
        await db.SaveChangesAsync();

        var ids = await new OrganizationAccessGuard(db).GetActiveVolunteerUserIdsAsync(orgId);

        ids.Should().BeEquivalentTo([active]);
    }

    [Fact]
    public async Task GetRosterThresholds_ReadsOrganizationPolicy_AndFallsBackToBrDefaults()
    {
        using var db = CreateDb();
        var orgWithPolicy = Guid.NewGuid();
        db.OrganizationPolicies.Add(OrganizationPolicy.Create(
            orgWithPolicy, RosterThresholds.PolicyKey, """{"activeDays":30,"dormantDays":90}"""));
        await db.SaveChangesAsync();
        var guard = new OrganizationAccessGuard(db);

        (await guard.GetRosterThresholdsAsync(orgWithPolicy)).Should().Be(new RosterThresholds(30, 90));
        (await guard.GetRosterThresholdsAsync(Guid.NewGuid())).Should().Be(RosterThresholds.Default);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"activeDays":120,"dormantDays":60}""")]
    [InlineData("""{"activeDays":-5}""")]
    public void ParseThresholds_RejectsInvalidPolicies(string json)
    {
        OrganizationAccessGuard.ParseThresholds(json).Should().Be(RosterThresholds.Default);
    }
}

/// <summary>BR-ROSTER-01: completed activity within 60 / 120 days.</summary>
public sealed class RosterStatusPolicyTests
{
    private static readonly DateOnly Today = new(2026, 9, 24);

    [Theory]
    [InlineData(0, RosterStatusPolicy.Active)]
    [InlineData(60, RosterStatusPolicy.Active)]
    [InlineData(61, RosterStatusPolicy.Dormant)]
    [InlineData(120, RosterStatusPolicy.Dormant)]
    [InlineData(121, RosterStatusPolicy.Inactive)]
    public void Compute_UsesDayThresholds(int daysAgo, string expected)
    {
        RosterStatusPolicy.Compute(Today.AddDays(-daysAgo), Today, RosterThresholds.Default).Should().Be(expected);
    }

    [Fact]
    public void Compute_IsNeverActivated_WithoutCompletedActivity()
    {
        RosterStatusPolicy.Compute(null, Today, RosterThresholds.Default).Should().Be(RosterStatusPolicy.NeverActivated);
    }

    [Fact]
    public void TodayInVienna_IsTheAustrianCalendarDate_NotUtc()
    {
        // 23:30 UTC on 24 Sep is 01:30 on 25 Sep in Vienna (CEST, UTC+2).
        var utc = new DateTimeOffset(2026, 9, 24, 23, 30, 0, TimeSpan.Zero);

        RosterStatusPolicy.TodayInVienna(utc).Should().Be(new DateOnly(2026, 9, 25));
    }
}
