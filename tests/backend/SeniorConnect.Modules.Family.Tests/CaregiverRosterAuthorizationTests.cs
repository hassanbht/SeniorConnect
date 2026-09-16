using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Family.Domain;
using SeniorConnect.Modules.Family.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Family.Tests;

// Gate 6 item 2: "a family member with no permissions sees literally
// nothing." GetCaregiversForSeniorAsync exposes every other caregiver's
// full permission matrix plus pending invitation codes — mere presence of
// an Active relationship must not be enough to read it.
public sealed class CaregiverRosterAuthorizationTests
{
    private static SeniorConnectDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SeniorConnectDbContext(options, new DefaultTenantContext());
    }

    [Fact]
    public async Task GetCaregiversForSeniorAsync_WhenCaregiverLacksManageSettings_ReturnsForbidden()
    {
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();

        var rel = FamilyRelationship.CreateActive(seniorId, caregiverId, RelationshipType.Child);
        rel.UpdatePermission(PermissionType.ManageSettings, false);
        db.FamilyRelationships.Add(rel);
        await db.SaveChangesAsync();

        var result = await service.GetCaregiversForSeniorAsync(seniorId, caregiverId);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(SeniorConnect.Domain.ErrorKind.Forbidden);
    }

    [Fact]
    public async Task GetCaregiversForSeniorAsync_WhenCaregiverHasManageSettings_SucceedsAndLogsAccess()
    {
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();

        var rel = FamilyRelationship.CreateActive(seniorId, caregiverId, RelationshipType.LegalGuardian);
        rel.UpdatePermission(PermissionType.ManageSettings, true);
        db.FamilyRelationships.Add(rel);
        await db.SaveChangesAsync();

        var result = await service.GetCaregiversForSeniorAsync(seniorId, caregiverId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(r => r.CaregiverUserId == caregiverId);

        var log = await db.SeniorAccessLogs
            .FirstOrDefaultAsync(l => l.SeniorUserId == seniorId && l.Action == "VIEW_CAREGIVER_ROSTER");
        log.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCaregiversForSeniorAsync_WhenSeniorThemselves_SucceedsWithoutRequiringPermission()
    {
        using var db = CreateInMemoryDb();
        var service = new FamilyService(db);
        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();

        var rel = FamilyRelationship.CreateActive(seniorId, caregiverId, RelationshipType.Child);
        db.FamilyRelationships.Add(rel);
        await db.SaveChangesAsync();

        var result = await service.GetCaregiversForSeniorAsync(seniorId, seniorId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
    }
}
