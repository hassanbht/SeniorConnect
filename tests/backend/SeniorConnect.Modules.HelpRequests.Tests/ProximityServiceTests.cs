using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Community.Infrastructure;
using SeniorConnect.Modules.Geography.Infrastructure;
using SeniorConnect.Modules.HelpRequests.Domain;
using SeniorConnect.Modules.HelpRequests.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.HelpRequests.Tests;

/// <summary>BR-GEO-05: nearby-request discovery for independent volunteers.</summary>
public sealed class ProximityServiceTests
{
    private sealed class TestTenantContext : ITenantContext
    {
        public Guid? OrganizationId => null;
        public bool IsPlatformScope => true;
    }

    // Innsbruck city centre.
    private const double QueryLat = 47.2692;
    private const double QueryLon = 11.4041;

    [Fact]
    public async Task FindNearbyRequestsAsync_ReturnsOnlyNearOpenRequests()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(databaseName: $"ProximityServiceTests_{Guid.NewGuid()}")
            .Options;

        using var db = new SeniorConnectDbContext(options, new TestTenantContext());

        var nearOpen = CreateHelpRequest(latitude: 47.2700, longitude: 11.4050); // ~0.1km away
        var farOpen = CreateHelpRequest(latitude: 48.2082, longitude: 16.3738); // Vienna, >400km away
        var nearAssigned = CreateHelpRequest(latitude: 47.2695, longitude: 11.4045);
        var assignResult = nearAssigned.Assign(Guid.NewGuid(), expectedRowVersion: 1);
        Assert.True(assignResult.IsSuccess);

        db.HelpRequests.AddRange(nearOpen, farOpen, nearAssigned);
        await db.SaveChangesAsync();

        var reader = new HelpRequestDiscoveryReader(db);
        var communityReader = new CommunityDiscoveryReader(db);
        var sut = new ProximityService(geoDb: db, profileDb: db, orgDb: db, helpRequestReader: reader, communityReader: communityReader);

        var result = await sut.FindNearbyRequestsAsync(QueryLat, QueryLon, radiusKm: 5, ct: default);

        Assert.True(result.IsSuccess);
        var match = Assert.Single(result.Value!);
        Assert.Equal(nearOpen.Latitude, match.Latitude);
        Assert.Equal(nearOpen.Longitude, match.Longitude);
        Assert.True(match.IsFuzzed);
        Assert.Equal(Guid.Empty, match.UserId);
    }

    // P5-06: community event discovery by distance + interest.
    [Fact]
    public async Task FindNearbyCommunityEventsAsync_ReturnsOnlyEventsWithinRadius()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(databaseName: $"ProximityServiceTests_{Guid.NewGuid()}")
            .Options;

        using var db = new SeniorConnectDbContext(options, new TestTenantContext());

        // Innsbruck, PLZ 6020 — seeded near QueryLat/QueryLon.
        db.AustrianAdministrativeUnits.Add(SeniorConnect.Modules.Geography.Domain.AustrianAdministrativeUnit.Create(
            bundeslandCode: "7", bundeslandName: "Tirol",
            bezirkCode: "701", bezirkName: "Innsbruck",
            gemeindeCode: "70101", gemeindeName: "Innsbruck",
            postalCode: "6020", localityName: "Innsbruck", latitude: 47.2692, longitude: 11.4041));
        // Vienna, PLZ 1010 — far away.
        db.AustrianAdministrativeUnits.Add(SeniorConnect.Modules.Geography.Domain.AustrianAdministrativeUnit.Create(
            bundeslandCode: "9", bundeslandName: "Wien",
            bezirkCode: "900", bezirkName: "Wien",
            gemeindeCode: "90001", gemeindeName: "Wien",
            postalCode: "1010", localityName: "Wien", latitude: 48.2082, longitude: 16.3738));

        var nearEvent = SeniorConnect.Modules.Community.Domain.CommunityEvent.Create(
            hostUserId: Guid.NewGuid(), title: "Innsbrucker Treffen", description: "x",
            startsAtUtc: DateTimeOffset.UtcNow.AddDays(1), endsAtUtc: DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
            category: "sports", locationPostalCode: "6020").Value!;
        var farEvent = SeniorConnect.Modules.Community.Domain.CommunityEvent.Create(
            hostUserId: Guid.NewGuid(), title: "Wiener Treffen", description: "x",
            startsAtUtc: DateTimeOffset.UtcNow.AddDays(1), endsAtUtc: DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
            category: "sports", locationPostalCode: "1010").Value!;
        db.CommunityEvents.AddRange(nearEvent, farEvent);
        await db.SaveChangesAsync();

        var sut = new ProximityService(
            geoDb: db, profileDb: db, orgDb: db,
            helpRequestReader: new HelpRequestDiscoveryReader(db),
            communityReader: new CommunityDiscoveryReader(db));

        var result = await sut.FindNearbyCommunityEventsAsync(
            QueryLat, QueryLon, radiusKm: 20, callerUserId: null, matchMyInterests: false, ct: default);

        Assert.True(result.IsSuccess);
        var match = Assert.Single(result.Value!);
        Assert.Equal(nearEvent.Id, match.EventId);
    }

    [Fact]
    public async Task FindNearbyCommunityEventsAsync_WithInterestFilter_ExcludesNonMatchingCategory()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(databaseName: $"ProximityServiceTests_{Guid.NewGuid()}")
            .Options;

        using var db = new SeniorConnectDbContext(options, new TestTenantContext());

        db.AustrianAdministrativeUnits.Add(SeniorConnect.Modules.Geography.Domain.AustrianAdministrativeUnit.Create(
            bundeslandCode: "7", bundeslandName: "Tirol",
            bezirkCode: "701", bezirkName: "Innsbruck",
            gemeindeCode: "70101", gemeindeName: "Innsbruck",
            postalCode: "6020", localityName: "Innsbruck", latitude: 47.2692, longitude: 11.4041));

        var userId = Guid.NewGuid();
        var interest = SeniorConnect.Modules.Profiles.Domain.Interest.Create("sports", "interest.sports");
        db.Interests.Add(interest);
        db.UserInterests.Add(SeniorConnect.Modules.Profiles.Domain.UserInterest.Create(userId, interest.Id));

        var matchingEvent = SeniorConnect.Modules.Community.Domain.CommunityEvent.Create(
            hostUserId: Guid.NewGuid(), title: "Sport-Treffen", description: "x",
            startsAtUtc: DateTimeOffset.UtcNow.AddDays(1), endsAtUtc: DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
            category: "sports", locationPostalCode: "6020").Value!;
        var nonMatchingEvent = SeniorConnect.Modules.Community.Domain.CommunityEvent.Create(
            hostUserId: Guid.NewGuid(), title: "Kultur-Treffen", description: "x",
            startsAtUtc: DateTimeOffset.UtcNow.AddDays(1), endsAtUtc: DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
            category: "culture", locationPostalCode: "6020").Value!;
        db.CommunityEvents.AddRange(matchingEvent, nonMatchingEvent);
        await db.SaveChangesAsync();

        var sut = new ProximityService(
            geoDb: db, profileDb: db, orgDb: db,
            helpRequestReader: new HelpRequestDiscoveryReader(db),
            communityReader: new CommunityDiscoveryReader(db));

        var result = await sut.FindNearbyCommunityEventsAsync(
            QueryLat, QueryLon, radiusKm: 20, callerUserId: userId, matchMyInterests: true, ct: default);

        Assert.True(result.IsSuccess);
        var match = Assert.Single(result.Value!);
        Assert.Equal(matchingEvent.Id, match.EventId);
    }

    private static HelpRequest CreateHelpRequest(double latitude, double longitude)
    {
        var seniorId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var result = HelpRequest.Create(
            organizationId: null,
            seniorUserId: seniorId,
            createdByUserId: seniorId,
            categoryId: Guid.NewGuid(),
            safetyLevel: 1,
            trustLevel: 1,
            scheduledStartUtc: now.AddHours(2),
            scheduledEndUtc: now.AddHours(3),
            durationMinutes: 60,
            locationType: LocationType.PublicPlace,
            latitude: latitude,
            longitude: longitude);

        Assert.True(result.IsSuccess);
        return result.Value!;
    }
}
