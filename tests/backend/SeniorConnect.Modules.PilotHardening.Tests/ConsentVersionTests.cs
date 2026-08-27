using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Domain;
using SeniorConnect.Modules.Identity.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.PilotHardening.Tests;

public sealed class ConsentVersionTests
{
    private SeniorConnectDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SeniorConnectDbContext(options, new DefaultTenantContext());
    }

    [Fact]
    public async Task RecordConsent_StoresVersion_AndTimestamps_P7_05()
    {
        using var db = CreateDbContext();
        var service = new PrivacyService(db);
        var userId = Guid.NewGuid();

        var request = new RecordConsentRequest(
            ConsentType.Terms,
            "v2.1",
            Granted: true
        );

        var result = await service.RecordConsentAsync(userId, request, "ip-hash-1234");
        result.IsSuccess.Should().BeTrue();
        result.Value.DocumentVersion.Should().Be("v2.1");
        result.Value.Granted.Should().BeTrue();
        result.Value.WithdrawnAtUtc.Should().BeNull();

        var consents = await service.GetUserConsentsAsync(userId);
        consents.IsSuccess.Should().BeTrue();
        consents.Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task WithdrawConsent_UpdatesTimestamp_P7_05()
    {
        using var db = CreateDbContext();
        var service = new PrivacyService(db);
        var userId = Guid.NewGuid();

        await service.RecordConsentAsync(userId, new RecordConsentRequest(
            ConsentType.NotificationsPush,
            "v1.0",
            Granted: true
        ));

        var withdrawResult = await service.WithdrawConsentAsync(userId, ConsentType.NotificationsPush);
        withdrawResult.IsSuccess.Should().BeTrue();
        withdrawResult.Value.Granted.Should().BeFalse();
        withdrawResult.Value.WithdrawnAtUtc.Should().NotBeNull();
    }
}
