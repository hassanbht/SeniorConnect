using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.HelpRequests.Tests;

public sealed class DataSeederTests
{
    private sealed class TestTenantContext : ITenantContext
    {
        public Guid? OrganizationId => null;
        public bool IsPlatformScope => true;
    }

    [Fact]
    public async Task DataSeeder_SeedsCategoriesAndPilotOrg_Idempotently()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(databaseName: $"DataSeederTests_{Guid.NewGuid()}")
            .Options;

        using (var db = new SeniorConnectDbContext(options, new TestTenantContext()))
        {
            // Initial seed
            await DataSeeder.SeedInitialDataAsync(db);

            var categoryCount = await db.ActivityCategories.CountAsync();
            var orgCount = await db.Organizations.CountAsync();

            Assert.True(categoryCount >= 8, "All primary activity categories must be seeded.");
            Assert.True(orgCount >= 1, "At least one pilot organization must be seeded.");
        }

        using (var db = new SeniorConnectDbContext(options, new TestTenantContext()))
        {
            // Second seed call must be idempotent and not duplicate data
            await DataSeeder.SeedInitialDataAsync(db);

            var categoryCount = await db.ActivityCategories.CountAsync();
            var orgCount = await db.Organizations.CountAsync();

            Assert.True(categoryCount >= 8);
            Assert.Equal(1, orgCount);
        }
    }
}
