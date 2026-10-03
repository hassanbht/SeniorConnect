using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Profiles.Application;
using SeniorConnect.Modules.Profiles.Domain;
using SeniorConnect.Modules.Profiles.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.HelpRequests.Tests;

/// <summary>
/// Phase 3 migration + consent service against real PostgreSQL, on a database
/// created empty for this class (see <see cref="PostgresTestDatabase"/>): the
/// whole migration chain applies, the "one current consent" unique index and
/// the level check hold, and a change (revoke, then append) never trips them.
/// Reproducible from a clean database by construction.
/// </summary>
public sealed class LocationConsentPostgresTests(PostgresTestDatabase pg) : IClassFixture<PostgresTestDatabase>
{
    [PostgresFact]
    public async Task Migration_Applies_AndConsentChangesRespectTheConstraints()
    {
        pg.AppliedMigrations.Should().BeGreaterThan(0);

        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseNpgsql(pg.ConnectionString)
            .Options;
        await using var db = new SeniorConnectDbContext(options, new DefaultTenantContext { IsPlatformScope = true });

        var userId = Guid.NewGuid();
        var service = new LocationSharingService(db);
        foreach (var level in new[] { "PRECISE", "NONE", "APPROXIMATE" })
        {
            (await service.SetAsync(userId, new UpdateLocationSharingRequest(level, LocationSharingConsent.CurrentVersion))).IsSuccess.Should().BeTrue();
        }

        var rows = await db.LocationSharingConsents.AsNoTracking().Where(c => c.UserId == userId).ToListAsync();
        rows.Should().HaveCount(3);
        rows.Count(r => r.RevokedAtUtc == null).Should().Be(1);
        rows.Single(r => r.RevokedAtUtc == null).Level.Should().Be(LocationSharingLevel.Approximate);
        (await service.GetAsync(userId)).Value!.Level.Should().Be("APPROXIMATE");

        var visibility = Phase4TestSupport.LocationVisibility(db, Phase4TestSupport.NewSafeguardingDb());
        (await visibility.DecideAsync(userId, Guid.NewGuid(), LocationRelationship.AssignedVolunteer)).Should().Be(LocationDisclosure.Approximate);
    }
}
