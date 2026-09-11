using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Community.Application;
using SeniorConnect.Modules.Community.Infrastructure;
using SeniorConnect.Modules.Organizations.Domain;
using SeniorConnect.Modules.Organizations.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Community.Tests;

public sealed class EventEditingAndCancellationTests
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

    private static async Task<(CommunityService service, Guid coordinatorId, Guid eventId)> SeedOrgEventAsync()
    {
        var db = CreateInMemoryDb();
        var org = Organization.Create("Freiwilligenzentrum Innsbruck-Land", OrganizationType.Ngo);
        db.Organizations.Add(org);
        var coordinatorId = Guid.NewGuid();
        var membership = OrganizationMembership.Create(org.Id, coordinatorId, MembershipRole.Coordinator);
        membership.Activate();
        db.OrganizationMemberships.Add(membership);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var created = await service.CreateEventAsync(coordinatorId, new CreateCommunityEventRequest(
            Title: "Herbstfest",
            Description: "Gemeinsames Fest",
            StartsAtUtc: DateTimeOffset.UtcNow.AddDays(7),
            EndsAtUtc: DateTimeOffset.UtcNow.AddDays(7).AddHours(3),
            OrganizationId: org.Id));

        return (service, coordinatorId, created.Value!.Id);
    }

    [Fact]
    public async Task UpdateEventAsync_ByOrgStaff_UpdatesTitleAndDescription()
    {
        var (service, coordinatorId, eventId) = await SeedOrgEventAsync();

        var result = await service.UpdateEventAsync(eventId, coordinatorId, new UpdateCommunityEventRequest(
            Title: "Herbstfest 2026",
            Description: "Aktualisierte Beschreibung",
            Category: "general",
            LocationAddress: "Hauptplatz 1",
            LocationPostalCode: "6060",
            Capacity: 50));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Title.Should().Be("Herbstfest 2026");
        result.Value!.Description.Should().Be("Aktualisierte Beschreibung");
        result.Value!.Capacity.Should().Be(50);
    }

    [Fact]
    public async Task UpdateEventAsync_ByNonStaff_Fails()
    {
        var (service, _, eventId) = await SeedOrgEventAsync();
        var stranger = Guid.NewGuid();

        var result = await service.UpdateEventAsync(eventId, stranger, new UpdateCommunityEventRequest(
            Title: "Hijacked",
            Description: "x",
            Category: "general",
            LocationAddress: null,
            LocationPostalCode: null,
            Capacity: null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(SeniorConnect.Domain.ErrorKind.Forbidden);
    }

    [Fact]
    public async Task CancelEventAsync_ByOrgStaff_SetsIsCancelledAndReason()
    {
        var (service, coordinatorId, eventId) = await SeedOrgEventAsync();

        var result = await service.CancelEventAsync(eventId, coordinatorId, "Witterung", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var stored = await service.GetEventByIdAsync(eventId);
        stored.Value!.IsCancelled.Should().BeTrue();
        stored.Value!.CancellationReason.Should().Be("Witterung");
    }

    [Fact]
    public async Task CancelEventAsync_ByNonStaff_Fails()
    {
        var (service, _, eventId) = await SeedOrgEventAsync();
        var stranger = Guid.NewGuid();

        var result = await service.CancelEventAsync(eventId, stranger, "not allowed", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(SeniorConnect.Domain.ErrorKind.Forbidden);
    }
}
