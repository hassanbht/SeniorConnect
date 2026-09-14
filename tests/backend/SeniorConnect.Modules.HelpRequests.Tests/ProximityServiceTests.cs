using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Infrastructure;
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
        var sut = new ProximityService(geoDb: db, profileDb: db, orgDb: db, helpRequestReader: reader);

        var result = await sut.FindNearbyRequestsAsync(QueryLat, QueryLon, radiusKm: 5, ct: default);

        Assert.True(result.IsSuccess);
        var match = Assert.Single(result.Value!);
        Assert.Equal(nearOpen.Latitude, match.Latitude);
        Assert.Equal(nearOpen.Longitude, match.Longitude);
        Assert.True(match.IsFuzzed);
        Assert.Equal(Guid.Empty, match.UserId);
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
