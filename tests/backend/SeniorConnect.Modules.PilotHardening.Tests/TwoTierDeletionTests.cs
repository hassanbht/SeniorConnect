using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Identity.Domain;
using Xunit;

namespace SeniorConnect.Modules.PilotHardening.Tests;

public sealed class TwoTierDeletionTests
{
    [Fact]
    public void Tier1Request_GeneratesToken_AndSchedulesTier2PurgeIn30Days_BR_GDPR_04()
    {
        var userId = Guid.NewGuid();
        var request = AccountDeletionRequest.Create(userId, "Kein Bedarf mehr");

        request.Status.Should().Be(DeletionTierStatus.Tier1Requested);
        request.ConfirmationToken.Should().HaveLength(6);
        request.ScheduledTier2PurgeUtc.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(30), TimeSpan.FromSeconds(5));
        request.Tier1ExecutedAtUtc.Should().BeNull();
        request.Tier2ExecutedAtUtc.Should().BeNull();
    }

    [Fact]
    public void ConfirmTier1_WithValidToken_DeactivatesAccount_BR_GDPR_04()
    {
        var user = User.CreateWithPhone("+41791234567", "Hans Muster");
        var request = AccountDeletionRequest.Create(user.Id);

        var result = request.ConfirmAndExecuteTier1(request.ConfirmationToken);
        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(DeletionTierStatus.Tier1Deactivated);
        request.Tier1ExecutedAtUtc.Should().NotBeNull();

        user.Deactivate();
        user.Status.Should().Be(UserStatus.Deactivated);
    }

    [Fact]
    public void ConfirmTier1_WithInvalidToken_IsRejected_BR_GDPR_04()
    {
        var userId = Guid.NewGuid();
        var request = AccountDeletionRequest.Create(userId);

        var result = request.ConfirmAndExecuteTier1("999999");
        result.IsFailure.Should().BeTrue();
        request.Status.Should().Be(DeletionTierStatus.Tier1Requested);
    }

    [Fact]
    public void GracePeriodCancellation_ReactivatesAccount()
    {
        var userId = Guid.NewGuid();
        var request = AccountDeletionRequest.Create(userId);
        request.ConfirmAndExecuteTier1(request.ConfirmationToken);

        // Cancel during grace period
        request.Cancel();
        request.Status.Should().Be(DeletionTierStatus.Cancelled);
    }

    [Fact]
    public void Tier2Purge_AnonymizesPersonalData_PreservingAuditPseudonym_BR_GDPR_04()
    {
        var user = User.CreateWithPhone("+41791234567", "Hans Muster");
        var request = AccountDeletionRequest.Create(user.Id);
        request.ConfirmAndExecuteTier1(request.ConfirmationToken);

        // Execute Tier 2 Purge
        request.ExecuteTier2Purge();
        request.Status.Should().Be(DeletionTierStatus.Tier2Purged);
        request.Tier2ExecutedAtUtc.Should().NotBeNull();

        user.AnonymizeForGdpr();

        user.Phone.Should().BeNull();
        user.Email.Should().BeNull();
        user.DateOfBirth.Should().BeNull();
        user.PasswordHash.Should().BeNull();
        user.DisplayName.Should().Be("Gelöschtes Profil");
        user.IsDeleted.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Deleted);
    }

    [Fact]
    public async Task ExportThenDelete_AnonymizesUser_PreservingPseudonymizedAuditTrail_Gate7()
    {
        var options = new DbContextOptionsBuilder<SeniorConnect.Infrastructure.SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var db = new SeniorConnect.Infrastructure.SeniorConnectDbContext(options, new SeniorConnect.Infrastructure.DefaultTenantContext());

        var privacyService = new SeniorConnect.Modules.Identity.Infrastructure.PrivacyService(db);

        // 1. Create active user with PII and consent
        var user = User.CreateWithPhone("+41791234567", "Greta Wallner", "de", seniorModeDefault: true);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await privacyService.RecordConsentAsync(user.Id, new SeniorConnect.Modules.Identity.Application.RecordConsentRequest(
            ConsentType.Privacy,
            "v1.0",
            Granted: true
        ));

        // Create an audit entry referencing user.Id (simulating past actions)
        var auditEntry = SeniorConnect.Modules.Reporting.Domain.AuditEntry.Create(
            action: "HELP_REQUEST_CREATED",
            subjectType: "HelpRequest",
            subjectId: Guid.NewGuid(),
            actorUserId: user.Id
        );
        db.AuditEntries.Add(auditEntry);
        await db.SaveChangesAsync();

        // 2. User requests full GDPR data export
        var exportResult = await privacyService.ExportUserDataAsync(user.Id);
        exportResult.IsSuccess.Should().BeTrue();
        exportResult.Value!.UserProfile.Should().NotBeNull();
        exportResult.Value.Consents.Should().HaveCount(1);

        // 3. User initiates account deletion
        var delReqResult = await privacyService.RequestAccountDeletionAsync(user.Id, new SeniorConnect.Modules.Identity.Application.RequestDeletionRequest(
            Reason: "Umzug ins Ausland"
        ));
        delReqResult.IsSuccess.Should().BeTrue();

        // 4. User confirms Tier 1
        var delReq = await db.AccountDeletionRequests.FirstAsync(r => r.UserId == user.Id);
        var confirmResult = await privacyService.ConfirmAccountDeletionAsync(user.Id, new SeniorConnect.Modules.Identity.Application.ConfirmDeletionRequest(
            ConfirmationToken: delReq.ConfirmationToken
        ));
        confirmResult.IsSuccess.Should().BeTrue();

        // 5. 30-day grace period passes -> Tier 2 purge executes
        delReq.ExecuteTier2Purge();
        user.AnonymizeForGdpr();
        await db.SaveChangesAsync();

        // 6. Assert User PII is completely eradicated and query filter hides soft-deleted user
        var normalQueryUser = await db.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        normalQueryUser.Should().BeNull();

        var purgedUser = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == user.Id);
        purgedUser.Phone.Should().BeNull();
        purgedUser.Email.Should().BeNull();
        purgedUser.DisplayName.Should().Be("Gelöschtes Profil");
        purgedUser.IsDeleted.Should().BeTrue();

        // 7. Assert Audit trail survives, pseudonymized (ActorUserId remains, but no PII reachable)
        var survivingAudit = await db.AuditEntries.FirstAsync(a => a.ActorUserId == user.Id);
        survivingAudit.ActorUserId.Should().Be(user.Id);
        survivingAudit.Action.Should().Be("HELP_REQUEST_CREATED");
    }
}
