using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Family.Application;
using SeniorConnect.Modules.Family.Domain;
using SeniorConnect.Modules.Family.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Family.Tests;

public sealed class SafetyAlertTests
{
    private static SeniorConnectDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SeniorConnectDbContext(options, new DefaultTenantContext());
    }

    [Fact]
    public async Task TriggerSafetyAlertAsync_CreatesActiveAlertAndLogsAction()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();

        var rel = FamilyRelationship.CreateActive(seniorId, caregiverId, RelationshipType.Child);
        db.FamilyRelationships.Add(rel);
        await db.SaveChangesAsync();

        var request = new TriggerSafetyAlertRequest(
            seniorId,
            SafetyAlertCategory.MissedCheckIn,
            "Senior did not check in after morning walk.");

        // Act
        var result = await service.TriggerSafetyAlertAsync(caregiverId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Category.Should().Be(SafetyAlertCategory.MissedCheckIn);
        result.Value.Status.Should().Be(SafetyAlertStatus.Active);
        result.Value.Details.Should().Be("Senior did not check in after morning walk.");

        var log = await db.SeniorAccessLogs.FirstOrDefaultAsync(l => l.SeniorUserId == seniorId && l.Action == "SAFETY_ALERT_TRIGGERED");
        log.Should().NotBeNull();
    }

    [Fact]
    public async Task AcknowledgeAndResolve_UpdatesAlertStateCorrectly()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();

        var rel = FamilyRelationship.CreateActive(seniorId, caregiverId, RelationshipType.Child);
        rel.UpdatePermission(PermissionType.ReceiveSafetyAlerts, true);
        db.FamilyRelationships.Add(rel);
        await db.SaveChangesAsync();

        var triggerResult = await service.TriggerSafetyAlertAsync(
            caregiverId,
            new TriggerSafetyAlertRequest(seniorId, SafetyAlertCategory.UnusualInactivity, "Inactivity detected."));
        var alertId = triggerResult.Value!.Id;

        // Act 1: Acknowledge
        var ackResult = await service.AcknowledgeSafetyAlertAsync(alertId, caregiverId);

        // Assert 1
        ackResult.IsSuccess.Should().BeTrue();
        ackResult.Value!.Status.Should().Be(SafetyAlertStatus.Acknowledged);
        ackResult.Value.AcknowledgedByUserId.Should().Be(caregiverId);

        // Act 2: Resolve
        var resolveResult = await service.ResolveSafetyAlertAsync(
            alertId,
            caregiverId,
            new ResolveSafetyAlertRequest("Called grandma, she is taking an afternoon nap."));

        // Assert 2
        resolveResult.IsSuccess.Should().BeTrue();
        resolveResult.Value!.Status.Should().Be(SafetyAlertStatus.Resolved);
        resolveResult.Value.ResolvedByUserId.Should().Be(caregiverId);
        resolveResult.Value.ResolutionNotes.Should().Be("Called grandma, she is taking an afternoon nap.");
    }
}
