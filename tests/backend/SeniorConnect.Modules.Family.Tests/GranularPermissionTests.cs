using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Family.Application;
using SeniorConnect.Modules.Family.Domain;
using SeniorConnect.Modules.Family.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Family.Tests;

public sealed class GranularPermissionTests
{
    private static SeniorConnectDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SeniorConnectDbContext(options, new DefaultTenantContext());
    }

    [Fact]
    public void UpdatePermission_GrantsAndRevokesSpecificPermissionsImmediately()
    {
        // Arrange
        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();
        var relationship = FamilyRelationship.CreateActive(seniorId, caregiverId, RelationshipType.Child);

        // Act & Assert 1: Initially does not have CreateHelpRequestsOnBehalf
        relationship.HasPermission(PermissionType.CreateHelpRequestsOnBehalf).Should().BeFalse();

        // Act 2: Grant permission
        relationship.UpdatePermission(PermissionType.CreateHelpRequestsOnBehalf, true);
        relationship.HasPermission(PermissionType.CreateHelpRequestsOnBehalf).Should().BeTrue();

        // Act 3: Revoke permission
        relationship.UpdatePermission(PermissionType.CreateHelpRequestsOnBehalf, false);
        relationship.HasPermission(PermissionType.CreateHelpRequestsOnBehalf).Should().BeFalse();

        // Other permissions remain unaffected
        relationship.HasPermission(PermissionType.ViewActivities).Should().BeTrue();
    }

    [Fact]
    public async Task UpdatePermissionsAsync_WhenAuthorizedSenior_UpdatesAndPersistsPermissions()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);

        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();
        var relationship = FamilyRelationship.CreateActive(seniorId, caregiverId, RelationshipType.Child);
        db.FamilyRelationships.Add(relationship);
        await db.SaveChangesAsync();

        var request = new UpdatePermissionsRequest(
            new Dictionary<PermissionType, bool>
            {
                [PermissionType.CreateHelpRequestsOnBehalf] = true,
                [PermissionType.ViewEmergencyContacts] = true,
                [PermissionType.ViewActivities] = false
            });

        // Act
        var result = await service.UpdatePermissionsAsync(relationship.Id, seniorId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var updated = await db.FamilyRelationships
            .Include(r => r.Permissions)
            .FirstAsync(r => r.Id == relationship.Id);

        updated.HasPermission(PermissionType.CreateHelpRequestsOnBehalf).Should().BeTrue();
        updated.HasPermission(PermissionType.ViewEmergencyContacts).Should().BeTrue();
        updated.HasPermission(PermissionType.ViewActivities).Should().BeFalse();
    }

    [Fact]
    public async Task UpdatePermissionsAsync_WhenRequesterIsNotSenior_ReturnsForbidden()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);

        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();
        var intruderId = Guid.NewGuid();
        var relationship = FamilyRelationship.CreateActive(seniorId, caregiverId, RelationshipType.Child);
        db.FamilyRelationships.Add(relationship);
        await db.SaveChangesAsync();

        var request = new UpdatePermissionsRequest(
            new Dictionary<PermissionType, bool>
            {
                [PermissionType.CreateHelpRequestsOnBehalf] = true
            });

        // Act
        var result = await service.UpdatePermissionsAsync(relationship.Id, intruderId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("FORBIDDEN");
    }
}
