using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Identity.Domain;
using SeniorConnect.Modules.Profiles.Domain;
using SeniorConnect.Modules.TrustSafety.Domain;
using SeniorConnect.Modules.TrustSafety.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.HelpRequests.Tests;

/// <summary>
/// Phase 4 migration against real PostgreSQL, on a database created empty for
/// this class (see <see cref="PostgresTestDatabase"/>). Proves the constraints
/// the InMemory provider cannot: the synced provider check (verification rows
/// could not be inserted before), revocation, the unique block pair, one
/// discovery row per person, and restrictions in the isolated safeguarding
/// schema. Reproducible from a clean database by construction.
/// </summary>
public sealed class Phase4PostgresTests(PostgresTestDatabase pg) : IClassFixture<PostgresTestDatabase>
{
    [PostgresFact]
    public async Task Phase4_Constraints_HoldOnPostgres()
    {
        pg.AppliedMigrations.Should().BeGreaterThan(0);

        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>().UseNpgsql(pg.ConnectionString).Options;
        await using var db = new SeniorConnectDbContext(options, new DefaultTenantContext { IsPlatformScope = true });

        var volunteer = Guid.NewGuid();
        var staff = Guid.NewGuid();
        var org = Guid.NewGuid();

        // A staff-recorded address fact inserts (Provider = 'Organization').
        var fact = Verification.RecordManual(volunteer, VerificationType.Address, VerificationMethod.DocumentSighted, staff, org).Value!;
        db.Verifications.Add(fact);
        await db.SaveChangesAsync();
        fact.Revoke(staff, "moved away").IsSuccess.Should().BeTrue();
        await db.SaveChangesAsync();
        (await db.Verifications.AsNoTracking().SingleAsync(v => v.Id == fact.Id)).Status.Should().Be(VerificationStatus.Revoked);

        // Revoked without a date is rejected by the database itself.
        var bad = async () => await db.Database.ExecuteSqlRawAsync(
            "UPDATE public.verifications SET revoked_at_utc = NULL WHERE id = {0}", fact.Id);
        await bad.Should().ThrowAsync<Exception>();

        // Blocking twice cannot create a second row.
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        db.UserBlocks.Add(UserBlock.Create(a, b));
        await db.SaveChangesAsync();
        db.UserBlocks.Add(UserBlock.Create(a, b));
        var duplicate = async () => await db.SaveChangesAsync();
        await duplicate.Should().ThrowAsync<DbUpdateException>();
        db.ChangeTracker.Clear();

        // One discovery row per person, default off.
        db.DiscoveryPreferences.Add(DiscoveryPreference.Create(volunteer, isDiscoverable: false, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        db.DiscoveryPreferences.Add(DiscoveryPreference.Create(volunteer, isDiscoverable: true, DateTimeOffset.UtcNow));
        var twice = async () => await db.SaveChangesAsync();
        await twice.Should().ThrowAsync<DbUpdateException>();
        db.ChangeTracker.Clear();

        // Restrictions live in the safeguarding schema, bound to their case.
        await using var sg = new SafeguardingDbContext(
            new DbContextOptionsBuilder<SafeguardingDbContext>().UseNpgsql(pg.ConnectionString).Options);
        var sCase = SafeguardingCase.Raise(Guid.NewGuid(), volunteer, "concern").Value!;
        sg.SafeguardingCases.Add(sCase);
        var restriction = SafeguardingRestriction.Impose(sCase, staff, null, false, true, false, false).Value!;
        sg.SafeguardingRestrictions.Add(restriction);
        await sg.SaveChangesAsync();
        (await new SafeguardingRestrictionReader(sg).GetActiveAsync([volunteer])).Should().ContainSingle(r => r.Location);

        restriction.Lift(staff, "resolved").IsSuccess.Should().BeTrue();
        await sg.SaveChangesAsync();
        (await new SafeguardingRestrictionReader(sg).GetActiveAsync([volunteer])).Should().BeEmpty();
    }
}

/// <summary>
/// "Migration down where practical" for the Phase 4 gate: the whole chain must
/// go down to Phase 1 and up again on an empty database. This round-trip gets
/// its <b>own</b> fixture: historical down-migrations re-add validated check
/// constraints, so data written by other tests on a shared database would
/// violate them on the way back up — a down/up replay is only meaningful from
/// a clean database, which is exactly what this class owns.
/// </summary>
public sealed class MigrationRoundTripPostgresTests(PostgresTestDatabase pg) : IClassFixture<PostgresTestDatabase>
{
    [PostgresFact]
    public async Task Migration_Chain_RoundTrips_DownAndUp()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>().UseNpgsql(pg.ConnectionString).Options;
        await using var db = new SeniorConnectDbContext(options, new DefaultTenantContext { IsPlatformScope = true });
        pg.AppliedMigrations.Should().BeGreaterThan(0);

        await db.Database.MigrateAsync("20260821214100_Phase1_Init");
        (await db.Database.GetPendingMigrationsAsync()).Should().Contain("20260925131110_Phase4VerificationDiscoverySafeguarding");

        await db.Database.MigrateAsync(); // up again through Phase 4
        (await db.Database.GetAppliedMigrationsAsync()).Should().HaveCount(pg.AppliedMigrations);

        // The freshly re-created Phase 4 objects still enforce their rules.
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        db.UserBlocks.Add(UserBlock.Create(a, b));
        await db.SaveChangesAsync();
        db.UserBlocks.Add(UserBlock.Create(a, b));
        var duplicate = async () => await db.SaveChangesAsync();
        await duplicate.Should().ThrowAsync<DbUpdateException>();
    }
}
