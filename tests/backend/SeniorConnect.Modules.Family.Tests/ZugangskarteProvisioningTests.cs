using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Family.Application;
using SeniorConnect.Modules.Family.Domain;
using SeniorConnect.Modules.Family.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Family.Tests;

public sealed class ZugangskarteProvisioningTests
{
    private static SeniorConnectDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SeniorConnectDbContext(options, new DefaultTenantContext());
    }

    [Fact]
    public void Generate_Produces6DigitCode_AndQrPayload()
    {
        // Arrange
        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();

        // Act
        var karte = Zugangskarte.Generate(seniorId, caregiverId, TimeSpan.FromDays(7));

        // Assert
        karte.SeniorUserId.Should().Be(seniorId);
        karte.CreatedByCaregiverUserId.Should().Be(caregiverId);
        karte.PairingCode.Should().MatchRegex(@"^\d{6}$");
        karte.QrPayload.Should().StartWith("seniorconnect://onboarding/claim");
        karte.IsClaimed.Should().BeFalse();
        karte.ExpiresAtUtc.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task CreateSeniorWithZugangskarteAsync_CreatesPrepopulatedAccount_AndAccessLog()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var caregiverId = Guid.NewGuid();

        var request = new CreateSeniorWithZugangskarteRequest(
            DisplayName: "Oma Gerda",
            PhoneNumber: "+436641234567",
            PostalCode: "8010",
            City: "Graz",
            RelationshipType: RelationshipType.Child,
            ValidForDays: 14);

        // Act
        var result = await service.CreateSeniorWithZugangskarteAsync(caregiverId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.PairingCode.Should().MatchRegex(@"^\d{6}$");

        var createdSeniorId = result.Value.SeniorUserId;

        // Verify Relationship was created with setup permissions
        var relationship = await db.FamilyRelationships
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.SeniorUserId == createdSeniorId && r.CaregiverUserId == caregiverId);

        relationship.Should().NotBeNull();
        relationship!.Status.Should().Be(RelationshipStatus.Active);
        relationship.HasPermission(PermissionType.ViewActivities).Should().BeTrue();
        relationship.HasPermission(PermissionType.CreateHelpRequestsOnBehalf).Should().BeTrue();

        // Verify initial access log was recorded
        var accessLog = await db.SeniorAccessLogs
            .FirstOrDefaultAsync(l => l.SeniorUserId == createdSeniorId);

        accessLog.Should().NotBeNull();
        accessLog!.Action.Should().Be("ACCOUNT_PROVISIONED");
    }

    [Fact]
    public async Task ClaimZugangskarteAsync_WithValidCode_MarksClaimed()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var caregiverId = Guid.NewGuid();

        var provisionResult = await service.CreateSeniorWithZugangskarteAsync(
            caregiverId,
            new CreateSeniorWithZugangskarteRequest(
                DisplayName: "Opa Hans",
                PhoneNumber: null,
                PostalCode: "8010",
                City: "Graz",
                RelationshipType: RelationshipType.Child));

        var code = provisionResult.Value!.PairingCode;
        var actualSeniorUserId = provisionResult.Value.SeniorUserId;

        // Act
        var claimResult = await service.ClaimZugangskarteAsync(actualSeniorUserId, new ClaimZugangskarteRequest(code));

        // Assert
        claimResult.IsSuccess.Should().BeTrue();
        claimResult.Value!.Zugangskarte.IsClaimed.Should().BeTrue();
        claimResult.Value.Session.Should().NotBeNull();

        var karteInDb = await db.Zugangskarten.FirstAsync(z => z.PairingCode == code);
        karteInDb.IsClaimed.Should().BeTrue();
        karteInDb.ClaimedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ClaimZugangskarteAsync_WhenCodeDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var seniorUserId = Guid.NewGuid();

        // Act
        var claimResult = await service.ClaimZugangskarteAsync(seniorUserId, new ClaimZugangskarteRequest("000000"));

        // Assert
        claimResult.IsSuccess.Should().BeFalse();
        claimResult.Error.Code.Should().Be("NOT_FOUND");
    }
}
