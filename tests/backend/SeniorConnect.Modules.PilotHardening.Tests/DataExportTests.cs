using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Domain;
using SeniorConnect.Modules.Identity.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.PilotHardening.Tests;

public sealed class DataExportTests
{
    private SeniorConnectDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SeniorConnectDbContext(options, new DefaultTenantContext());
    }

    [Fact]
    public async Task ExportUserData_ReturnsCompleteMachineReadablePackage_BR_GDPR_03()
    {
        using var db = CreateDbContext();
        var service = new PrivacyService(db);

        var user = User.CreateWithPhone("+41791112233", "Anna Schmidt", "de", seniorModeDefault: true);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await service.RecordConsentAsync(user.Id, new RecordConsentRequest(
            ConsentType.Privacy,
            "v1.0",
            Granted: true
        ));

        var exportResult = await service.ExportUserDataAsync(user.Id);
        exportResult.IsSuccess.Should().BeTrue();

        var data = exportResult.Value;
        data.UserId.Should().Be(user.Id);
        data.ExportFormat.Should().Be("JSON-GDPR-PORTABLE-v1");
        data.UserProfile.Should().NotBeNull();
        data.Consents.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExportUserData_ForUnknownUser_ReturnsNotFound_BR_GDPR_03()
    {
        using var db = CreateDbContext();
        var service = new PrivacyService(db);

        var exportResult = await service.ExportUserDataAsync(Guid.NewGuid());
        exportResult.IsFailure.Should().BeTrue();
    }
}
