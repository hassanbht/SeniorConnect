using SeniorConnect.Domain;
using SeniorConnect.Modules.Identity.Domain;
using SeniorConnect.Modules.Notifications.Domain;
using Xunit;

namespace SeniorConnect.Modules.HelpRequests.Tests;

public sealed class PilotHardeningTests
{
    [Fact]
    public void NotificationBudgetTracker_NonUrgentOverCap_EnforcesMaxDailyLimit()
    {
        var userId = Guid.NewGuid();
        var tracker = NotificationBudgetTracker.Create(userId);
        var now = DateTimeOffset.UtcNow;

        // First non-urgent -> allowed
        Assert.True(tracker.CanSendNonUrgent(now));
        tracker.RecordDispatch(NotificationPriority.Normal, now);

        // Second non-urgent -> allowed
        Assert.True(tracker.CanSendNonUrgent(now));
        tracker.RecordDispatch(NotificationPriority.Normal, now);

        // Third non-urgent -> capped (budget exceeded, BR-NOTIFY-01/02)
        Assert.False(tracker.CanSendNonUrgent(now));

        // Critical safety / urgent dispatch does not count against non-urgent cap
        tracker.RecordDispatch(NotificationPriority.CriticalSafety, now);
        Assert.False(tracker.CanSendNonUrgent(now));

        // 25 hours later -> new window resets count
        var tomorrow = now.AddHours(25);
        Assert.True(tracker.CanSendNonUrgent(tomorrow));
    }

    [Fact]
    public void NotificationPreference_QuietHours_DetectsRestPeriodCorrectly()
    {
        var userId = Guid.NewGuid();
        var pref = NotificationPreference.CreateDefault(userId);

        // Default quiet hours: 20:00 to 08:00
        var daytime = new DateTimeOffset(2026, 8, 22, 14, 0, 0, TimeSpan.Zero); // 14:00
        var nighttime = new DateTimeOffset(2026, 8, 22, 22, 30, 0, TimeSpan.Zero); // 22:30
        var earlyMorning = new DateTimeOffset(2026, 8, 23, 6, 15, 0, TimeSpan.Zero); // 06:15

        Assert.False(pref.IsInQuietHours(daytime));
        Assert.True(pref.IsInQuietHours(nighttime));
        Assert.True(pref.IsInQuietHours(earlyMorning));

        // Category muting
        pref.Update(
            pushEnabled: true,
            smsEnabled: true,
            inAppEnabled: true,
            quietHoursEnabled: true,
            quietHoursStart: new TimeSpan(20, 0, 0),
            quietHoursEnd: new TimeSpan(8, 0, 0),
            helpRequestsCategoryEnabled: true,
            communityCategoryEnabled: false,
            familyWelfareCategoryEnabled: true,
            systemAccountCategoryEnabled: true);

        Assert.True(pref.IsCategoryEnabled(NotificationCategory.HelpRequests));
        Assert.False(pref.IsCategoryEnabled(NotificationCategory.Community));
    }

    [Fact]
    public void VersionedConsent_GrantAndWithdraw_MaintainsAuditTrail()
    {
        var userId = Guid.NewGuid();
        var consent = Consent.Create(
            userId,
            ConsentType.Terms,
            "v1.2-2026",
            granted: true,
            ipHash: "192.168.1.1");

        Assert.True(consent.Granted);
        Assert.Equal("v1.2-2026", consent.DocumentVersion);
        Assert.Null(consent.WithdrawnAtUtc);

        consent.Withdraw();
        Assert.False(consent.Granted);
        Assert.NotNull(consent.WithdrawnAtUtc);
    }

    [Fact]
    public void AccountDeletionRequest_TwoTierLifecycle_RequiresTokenAndSets30DayPurge()
    {
        var userId = Guid.NewGuid();
        var deletionReq = AccountDeletionRequest.Create(userId, "Moving to another country");

        Assert.Equal(DeletionTierStatus.Tier1Requested, deletionReq.Status);
        Assert.Equal(6, deletionReq.ConfirmationToken.Length);
        Assert.True(deletionReq.ScheduledTier2PurgeUtc > DateTimeOffset.UtcNow.AddDays(29));

        // Invalid token rejection
        var failResult = deletionReq.ConfirmAndExecuteTier1("000000");
        Assert.True(failResult.IsFailure);

        // Valid token execution
        var successResult = deletionReq.ConfirmAndExecuteTier1(deletionReq.ConfirmationToken);
        Assert.True(successResult.IsSuccess);
        Assert.Equal(DeletionTierStatus.Tier1Deactivated, deletionReq.Status);
        Assert.NotNull(deletionReq.Tier1ExecutedAtUtc);

        // Tier 2 purge execution after 30 days
        deletionReq.ExecuteTier2Purge();
        Assert.Equal(DeletionTierStatus.Tier2Purged, deletionReq.Status);
        Assert.NotNull(deletionReq.Tier2ExecutedAtUtc);
    }
}
