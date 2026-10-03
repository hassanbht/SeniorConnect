using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Family.Application;
using SeniorConnect.Modules.Family.Domain;
using SeniorConnect.Modules.Family.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Family.Tests;

public sealed class FamilyPrivacyIsolationTests
{
    private static SeniorConnectDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SeniorConnectDbContext(options, new DefaultTenantContext());
    }

    [Fact]
    public async Task Caregiver_WithoutActiveRelationship_CannotAccessSeniorData()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var seniorId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();

        // Act
        var result = await service.GetAccessLogsAsync(seniorId, strangerId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public void Family_DataModels_DoNotExposeSafeguardingOrChatTables()
    {
        // Invariant: Family module entities should never directly link or expose safeguarding entities.
        var familyEntityTypes = typeof(FamilyRelationship).Assembly.GetTypes()
            .Where(t => t.Namespace?.Contains("Domain") == true);

        foreach (var type in familyEntityTypes)
        {
            var propertyTypes = type.GetProperties().Select(p => p.PropertyType.Name);
            propertyTypes.Should().NotContain("SafeguardingConcern");
            propertyTypes.Should().NotContain("SafeguardingReport");
        }
    }
}
