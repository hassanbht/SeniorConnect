using FluentAssertions;
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
}
