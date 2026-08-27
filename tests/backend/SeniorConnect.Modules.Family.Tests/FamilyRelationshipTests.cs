using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Family.Application;
using SeniorConnect.Modules.Family.Domain;
using SeniorConnect.Modules.Family.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Family.Tests;

public sealed class FamilyRelationshipTests
{
    private static SeniorConnectDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SeniorConnectDbContext(options, new DefaultTenantContext());
    }

    [Fact]
    public void CreateActive_SetsStatusToActive_AndInitializesDefaultPermissions()
    {
        // Arrange
        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();

        // Act
        var relationship = FamilyRelationship.CreateActive(
            seniorId,
            caregiverId,
            RelationshipType.Child);

        // Assert
        relationship.SeniorUserId.Should().Be(seniorId);
        relationship.CaregiverUserId.Should().Be(caregiverId);
        relationship.RelationshipType.Should().Be(RelationshipType.Child);
        relationship.Status.Should().Be(RelationshipStatus.Active);
        relationship.ConfirmedAtUtc.Should().NotBeNull();
        relationship.Permissions.Should().HaveCount(5);
        relationship.HasPermission(PermissionType.ViewActivities).Should().BeTrue();
        relationship.HasPermission(PermissionType.ReceiveSafetyAlerts).Should().BeTrue();
    }

    [Fact]
    public void CreateInvitation_SetsStatusToInvited_WithInvitationCodeAndExpiry()
    {
        // Arrange
        var seniorId = Guid.NewGuid();
        var code = "INV-987654";
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

        // Act
        var relationship = FamilyRelationship.CreateInvitation(
            seniorId,
            RelationshipType.Sibling,
            code,
            expiresAt);

        // Assert
        relationship.SeniorUserId.Should().Be(seniorId);
        relationship.Status.Should().Be(RelationshipStatus.Invited);
        relationship.InvitationCode.Should().Be(code);
        relationship.InvitationExpiresAtUtc.Should().Be(expiresAt);
        relationship.CaregiverUserId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void Accept_ActiveStatusAndAssignsCaregiver()
    {
        // Arrange
        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();
        var relationship = FamilyRelationship.CreateInvitation(
            seniorId,
            RelationshipType.Child,
            "CODE123",
            DateTimeOffset.UtcNow.AddDays(1));

        // Act
        var result = relationship.Accept(caregiverId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        relationship.Status.Should().Be(RelationshipStatus.Active);
        relationship.CaregiverUserId.Should().Be(caregiverId);
        relationship.ConfirmedAtUtc.Should().NotBeNull();
        relationship.InvitationCode.Should().BeNull();
    }

    [Fact]
    public void Accept_WhenExpired_FailsWithConflict()
    {
        // Arrange
        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();
        var relationship = FamilyRelationship.CreateInvitation(
            seniorId,
            RelationshipType.Child,
            "CODE123",
            DateTimeOffset.UtcNow.AddDays(-1)); // Expired

        // Act
        var result = relationship.Accept(caregiverId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("INVITATION_EXPIRED");
        relationship.Status.Should().Be(RelationshipStatus.Invited);
    }

    [Fact]
    public void Revoke_RevokesRelationshipAndAllGrantedPermissionsImmediately()
    {
        // Arrange
        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();
        var relationship = FamilyRelationship.CreateActive(seniorId, caregiverId, RelationshipType.LegalGuardian);
        relationship.UpdatePermission(PermissionType.CreateHelpRequestsOnBehalf, true);

        // Act
        var result = relationship.Revoke(seniorId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        relationship.Status.Should().Be(RelationshipStatus.Revoked);
        relationship.RevokedAtUtc.Should().NotBeNull();
        relationship.RevokedByUserId.Should().Be(seniorId);
        relationship.HasPermission(PermissionType.ViewActivities).Should().BeFalse();
        relationship.HasPermission(PermissionType.CreateHelpRequestsOnBehalf).Should().BeFalse();
        relationship.HasPermission(PermissionType.ReceiveSafetyAlerts).Should().BeFalse();
    }
}
