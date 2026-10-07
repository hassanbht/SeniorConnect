using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Organizations.Domain;
using SeniorConnect.Modules.Organizations.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Organizations.Tests;

/// <summary>
/// Wave-3 / ADR-029: "may manage staff two-factor" is an active
/// Coordinator/Admin membership in any organization — never the
/// authentication method, TOTP presence, or any client flag.
///
/// The matrix below pins the fail-closed boundary: every non-staff shape
/// (ordinary member, invited/suspended/left, unknown, empty, platform-only
/// roles) resolves to false. Tenant scoping of the managed devices themselves
/// happens at the calling endpoint via organizationId; this capability only
/// answers whether the account is staff at all.
/// </summary>
public sealed class StaffTotpAuthorizationTests
{
    private static SeniorConnectDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<SeniorConnectDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            new DefaultTenantContext { IsPlatformScope = true });

    private static void AddMember(
        SeniorConnectDbContext db, Guid orgId, Guid userId, MembershipRole role, bool active = true)
    {
        var membership = OrganizationMembership.Create(orgId, userId, role);
        if (active)
        {
            membership.Activate();
        }

        db.OrganizationMemberships.Add(membership);
    }

    // --- positive -----------------------------------------------------------

    [Fact]
    public async Task ActiveCoordinator_CanManageStaffSecurity()
    {
        using var db = CreateDb();
        var userId = Guid.NewGuid();
        AddMember(db, Guid.NewGuid(), userId, MembershipRole.Coordinator);
        await db.SaveChangesAsync();
        var authorizer = new OrganizationStaffSecurityAuthorizer(db);

        (await authorizer.CanManageStaffSecurityAsync(userId)).Should().BeTrue();
    }

    [Fact]
    public async Task ActiveAdmin_CanManageStaffSecurity()
    {
        using var db = CreateDb();
        var userId = Guid.NewGuid();
        AddMember(db, Guid.NewGuid(), userId, MembershipRole.Admin);
        await db.SaveChangesAsync();
        var authorizer = new OrganizationStaffSecurityAuthorizer(db);

        (await authorizer.CanManageStaffSecurityAsync(userId)).Should().BeTrue();
    }

    [Fact]
    public async Task MembershipInAnyOrganization_GrantsTheCapability()
    {
        using var db = CreateDb();
        var userId = Guid.NewGuid();
        AddMember(db, Guid.NewGuid(), userId, MembershipRole.Volunteer);
        AddMember(db, Guid.NewGuid(), userId, MembershipRole.Coordinator);
        await db.SaveChangesAsync();
        var authorizer = new OrganizationStaffSecurityAuthorizer(db);

        (await authorizer.CanManageStaffSecurityAsync(userId)).Should().BeTrue(
            "staff-ness is organization-agnostic; which tenants they may act on is enforced by the caller");
    }

    // --- negative: ordinary and inactive shapes ------------------------------

    [Fact]
    public async Task OrdinaryVolunteer_IsDenied()
    {
        using var db = CreateDb();
        var userId = Guid.NewGuid();
        AddMember(db, Guid.NewGuid(), userId, MembershipRole.Volunteer);
        await db.SaveChangesAsync();
        var authorizer = new OrganizationStaffSecurityAuthorizer(db);

        (await authorizer.CanManageStaffSecurityAsync(userId)).Should().BeFalse();
    }

    [Fact]
    public async Task StaffRoleWithoutCoordinatorOrAdmin_IsDenied()
    {
        using var db = CreateDb();
        var userId = Guid.NewGuid();
        AddMember(db, Guid.NewGuid(), userId, MembershipRole.Staff);
        await db.SaveChangesAsync();
        var authorizer = new OrganizationStaffSecurityAuthorizer(db);

        (await authorizer.CanManageStaffSecurityAsync(userId)).Should().BeFalse(
            "the generic Staff role is not a Coordinator/Admin grant");
    }

    [Fact]
    public async Task InvitedCoordinator_IsDeniedUntilActive()
    {
        using var db = CreateDb();
        var userId = Guid.NewGuid();
        AddMember(db, Guid.NewGuid(), userId, MembershipRole.Coordinator, active: false);
        await db.SaveChangesAsync();
        var authorizer = new OrganizationStaffSecurityAuthorizer(db);

        (await authorizer.CanManageStaffSecurityAsync(userId)).Should().BeFalse();
    }

    [Fact]
    public async Task SuspendedCoordinator_IsDenied()
    {
        using var db = CreateDb();
        var userId = Guid.NewGuid();
        var membership = OrganizationMembership.Create(Guid.NewGuid(), userId, MembershipRole.Coordinator);
        membership.Activate();
        membership.Suspend();
        db.OrganizationMemberships.Add(membership);
        await db.SaveChangesAsync();
        var authorizer = new OrganizationStaffSecurityAuthorizer(db);

        (await authorizer.CanManageStaffSecurityAsync(userId)).Should().BeFalse();
    }

    [Fact]
    public async Task DepartedAdmin_IsDenied()
    {
        using var db = CreateDb();
        var userId = Guid.NewGuid();
        var membership = OrganizationMembership.Create(Guid.NewGuid(), userId, MembershipRole.Admin);
        membership.Activate();
        membership.Leave();
        db.OrganizationMemberships.Add(membership);
        await db.SaveChangesAsync();
        var authorizer = new OrganizationStaffSecurityAuthorizer(db);

        (await authorizer.CanManageStaffSecurityAsync(userId)).Should().BeFalse();
    }

    [Fact]
    public async Task UnknownUser_IsDenied()
    {
        using var db = CreateDb();
        var authorizer = new OrganizationStaffSecurityAuthorizer(db);

        (await authorizer.CanManageStaffSecurityAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task EmptyUserId_IsDenied()
    {
        using var db = CreateDb();
        var authorizer = new OrganizationStaffSecurityAuthorizer(db);

        (await authorizer.CanManageStaffSecurityAsync(Guid.Empty)).Should().BeFalse();
    }

    // --- negative: platform roles are out of scope ----------------------------

    [Fact]
    public async Task PlatformSafeguardingOfficer_IsDeniedByThisAuthorizer()
    {
        using var db = CreateDb();
        var userId = Guid.NewGuid();
        AddMember(db, Guid.NewGuid(), userId, MembershipRole.PlatformSafeguardingOfficer);
        await db.SaveChangesAsync();
        var authorizer = new OrganizationStaffSecurityAuthorizer(db);

        (await authorizer.CanManageStaffSecurityAsync(userId)).Should().BeFalse(
            "ADR-029 covers Coordinator/Admin staff only; platform safeguarding roles are authorized elsewhere");
    }
}
