using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Organizations.Domain;
using SeniorConnect.Modules.Organizations.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Organizations.Tests;

public sealed class OrganizationCoordinatorReaderTests
{
    private static SeniorConnectDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SeniorConnectDbContext(options, new DefaultTenantContext { IsPlatformScope = true });
    }

    [Fact]
    public async Task GetActiveMembershipsForUserAsync_ReturnsOnlyActiveCoordinatorOrAdminOrgs()
    {
        using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();

        var orgA = Organization.Create("Testverein Musterstadt", OrganizationType.Ngo);
        var orgB = Organization.Create("Other Org", OrganizationType.Association);
        var orgC = Organization.Create("Suspended-Membership Org", OrganizationType.Company);
        db.Organizations.AddRange(orgA, orgB, orgC);

        var activeCoordinator = OrganizationMembership.Create(orgA.Id, userId, MembershipRole.Coordinator);
        activeCoordinator.Activate();
        var activeAdmin = OrganizationMembership.Create(orgB.Id, userId, MembershipRole.Admin);
        activeAdmin.Activate();
        var activeVolunteer = OrganizationMembership.Create(orgB.Id, userId, MembershipRole.Volunteer);
        activeVolunteer.Activate(); // active but not staff — must not add a duplicate/extra row for orgB
        var suspendedCoordinator = OrganizationMembership.Create(orgC.Id, userId, MembershipRole.Coordinator);
        // left Invited/never activated on purpose

        db.OrganizationMemberships.AddRange(activeCoordinator, activeAdmin, activeVolunteer, suspendedCoordinator);
        await db.SaveChangesAsync();

        var reader = new OrganizationCoordinatorReader(db);

        var result = await reader.GetActiveMembershipsForUserAsync(userId);

        result.Should().HaveCount(2);
        result.Should().Contain(m => m.OrganizationId == orgA.Id && m.OrganizationName == orgA.Name);
        result.Should().Contain(m => m.OrganizationId == orgB.Id && m.OrganizationName == orgB.Name);
        result.Should().NotContain(m => m.OrganizationId == orgC.Id);
    }

    [Fact]
    public async Task GetSafeguardingOfficerOrganizationIdsAsync_IgnoresAdminAndCoordinatorRoles()
    {
        // BR-SG-02: only an active SafeguardingOfficer membership scopes case access.
        using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var officerOrg = Organization.Create("Officer Org", OrganizationType.Ngo);
        var adminOrg = Organization.Create("Admin Org", OrganizationType.Ngo);
        db.Organizations.AddRange(officerOrg, adminOrg);

        var officer = OrganizationMembership.Create(officerOrg.Id, userId, MembershipRole.SafeguardingOfficer);
        officer.Activate();
        var admin = OrganizationMembership.Create(adminOrg.Id, userId, MembershipRole.Admin);
        admin.Activate();
        db.OrganizationMemberships.AddRange(officer, admin);
        await db.SaveChangesAsync();

        var result = await new OrganizationCoordinatorReader(db).GetSafeguardingOfficerOrganizationIdsAsync(userId);

        result.Should().ContainSingle().Which.Should().Be(officerOrg.Id);
    }
}
