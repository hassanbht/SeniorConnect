using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Family.Application;
using SeniorConnect.Modules.Family.Domain;
using SeniorConnect.Modules.Family.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Family.Tests;

public sealed class SeniorAccessLogTests
{
    private static SeniorConnectDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SeniorConnectDbContext(options, new DefaultTenantContext());
    }

    [Fact]
    public async Task GetAccessLogsAsync_WhenSeniorThemselves_ReturnsPast30DaysLogsInPlainGerman()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();

        // 1 log from 5 days ago (should be included)
        db.SeniorAccessLogs.Add(SeniorAccessLog.Create(
            seniorId,
            caregiverId,
            "Anna Meier (Tochter)",
            "VIEW_ACTIVITIES",
            "HelpRequests",
            "Anna hat die aktuellen Hilfeanfragen eingesehen."));

        // 1 log from 45 days ago (outside 30-day window, should not be included)
        var oldLog = SeniorAccessLog.Create(
            seniorId,
            caregiverId,
            "Anna Meier (Tochter)",
            "VIEW_PROFILE",
            "Profile",
            "Alte Einsicht");
        // Use reflection or EF entry to set old timestamp
        db.SeniorAccessLogs.Add(oldLog);
        await db.SaveChangesAsync();

        db.Entry(oldLog).Property("TimestampUtc").CurrentValue = DateTimeOffset.UtcNow.AddDays(-45);
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetAccessLogsAsync(seniorId, seniorId, days: 30);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].PlainLanguageDescription.Should().Be("Anna hat die aktuellen Hilfeanfragen eingesehen.");
        result.Value[0].AccessedByUserName.Should().Be("Anna Meier (Tochter)");
    }

    [Fact]
    public async Task GetAccessLogsAsync_WhenCaregiverLacksManageSettingsPermission_ReturnsForbidden()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();

        var rel = FamilyRelationship.CreateActive(seniorId, caregiverId, RelationshipType.Child);
        rel.UpdatePermission(PermissionType.ManageSettings, false); // No manage settings
        db.FamilyRelationships.Add(rel);

        db.SeniorAccessLogs.Add(SeniorAccessLog.Create(
            seniorId, caregiverId, "Anna", "VIEW", "HelpRequests", "Einsicht"));
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetAccessLogsAsync(seniorId, caregiverId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("FORBIDDEN");
    }
}
