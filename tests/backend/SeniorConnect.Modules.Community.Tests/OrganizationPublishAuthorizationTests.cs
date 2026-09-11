using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Community.Application;
using SeniorConnect.Modules.Community.Infrastructure;
using SeniorConnect.Modules.Organizations.Domain;
using SeniorConnect.Modules.Organizations.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Community.Tests;

public sealed class OrganizationPublishAuthorizationTests
{
    private static SeniorConnectDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SeniorConnectDbContext(options, new DefaultTenantContext { IsPlatformScope = true });
    }

    private static CommunityService CreateService(SeniorConnectDbContext db) =>
        new(db, new LocalMessageModerationService(), new OrganizationCoordinatorReader(db));

    [Fact]
    public async Task CreateEventAsync_ForOrganization_Fails_WhenCallerIsNotStaff()
    {
        using var db = CreateInMemoryDb();
        var org = Organization.Create("Freiwilligenzentrum Innsbruck-Land", OrganizationType.Ngo);
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var strangerUserId = Guid.NewGuid();

        var result = await service.CreateEventAsync(strangerUserId, new CreateCommunityEventRequest(
            Title: "Herbstfest",
            Description: "Gemeinsames Fest",
            StartsAtUtc: DateTimeOffset.UtcNow.AddDays(7),
            EndsAtUtc: DateTimeOffset.UtcNow.AddDays(7).AddHours(3),
            OrganizationId: org.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(SeniorConnect.Domain.ErrorKind.Forbidden);
    }

    [Fact]
    public async Task CreateEventAsync_ForOrganization_Succeeds_WhenCallerIsActiveCoordinator()
    {
        using var db = CreateInMemoryDb();
        var org = Organization.Create("Freiwilligenzentrum Innsbruck-Land", OrganizationType.Ngo);
        db.Organizations.Add(org);
        var coordinatorUserId = Guid.NewGuid();
        var membership = OrganizationMembership.Create(org.Id, coordinatorUserId, MembershipRole.Coordinator);
        membership.Activate();
        db.OrganizationMemberships.Add(membership);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.CreateEventAsync(coordinatorUserId, new CreateCommunityEventRequest(
            Title: "Herbstfest",
            Description: "Gemeinsames Fest",
            StartsAtUtc: DateTimeOffset.UtcNow.AddDays(7),
            EndsAtUtc: DateTimeOffset.UtcNow.AddDays(7).AddHours(3),
            OrganizationId: org.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value!.OrganizationId.Should().Be(org.Id);
    }

    [Fact]
    public async Task CreateEventAsync_ForOrganization_Fails_WhenMembershipIsNotYetActive()
    {
        using var db = CreateInMemoryDb();
        var org = Organization.Create("Freiwilligenzentrum Innsbruck-Land", OrganizationType.Ngo);
        db.Organizations.Add(org);
        var invitedUserId = Guid.NewGuid();
        db.OrganizationMemberships.Add(OrganizationMembership.Create(org.Id, invitedUserId, MembershipRole.Coordinator));
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.CreateEventAsync(invitedUserId, new CreateCommunityEventRequest(
            Title: "Herbstfest",
            Description: "Gemeinsames Fest",
            StartsAtUtc: DateTimeOffset.UtcNow.AddDays(7),
            EndsAtUtc: DateTimeOffset.UtcNow.AddDays(7).AddHours(3),
            OrganizationId: org.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(SeniorConnect.Domain.ErrorKind.Forbidden);
    }

    [Fact]
    public async Task CreateEventAsync_WithoutOrganizationId_StillSucceeds()
    {
        using var db = CreateInMemoryDb();
        var service = CreateService(db);
        var userId = Guid.NewGuid();

        var result = await service.CreateEventAsync(userId, new CreateCommunityEventRequest(
            Title: "Privater Treff",
            Description: "Kein Organisationsbezug",
            StartsAtUtc: DateTimeOffset.UtcNow.AddDays(1),
            EndsAtUtc: DateTimeOffset.UtcNow.AddDays(1).AddHours(1)));

        result.IsSuccess.Should().BeTrue();
    }
}
