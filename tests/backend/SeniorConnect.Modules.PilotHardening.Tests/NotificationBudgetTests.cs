using FluentAssertions;
using SeniorConnect.Modules.Notifications.Domain;
using Xunit;

namespace SeniorConnect.Modules.PilotHardening.Tests;

public sealed class NotificationBudgetTests
{
    [Fact]
    public void NonUrgentNotifications_AreCappedAtTwoPerDay_BR_NOTIFY_02()
    {
        var userId = Guid.NewGuid();
        var tracker = NotificationBudgetTracker.Create(userId);
        var now = DateTimeOffset.UtcNow;

        // 1st notification
        tracker.CanSendNonUrgent(now).Should().BeTrue();
        tracker.RecordDispatch(NotificationPriority.Normal, now);

        // 2nd notification
        tracker.CanSendNonUrgent(now).Should().BeTrue();
        tracker.RecordDispatch(NotificationPriority.Normal, now);

        // 3rd notification should be blocked by budget
        tracker.CanSendNonUrgent(now).Should().BeFalse();
    }

    [Fact]
    public void UrgentAndSafetyNotifications_BypassBudget_BR_NOTIFY_02()
    {
        var userId = Guid.NewGuid();
        var tracker = NotificationBudgetTracker.Create(userId);
        var now = DateTimeOffset.UtcNow;

        // Exhaust normal budget
        tracker.RecordDispatch(NotificationPriority.Normal, now);
        tracker.RecordDispatch(NotificationPriority.Normal, now);
        tracker.CanSendNonUrgent(now).Should().BeFalse();

        // Urgent dispatches do not increment non-urgent count
        tracker.RecordDispatch(NotificationPriority.Urgent, now);
        tracker.RecordDispatch(NotificationPriority.CriticalSafety, now);
        tracker.NonUrgentCount.Should().Be(2);
    }

    [Fact]
    public void BudgetResets_After24HourWindow_BR_NOTIFY_02()
    {
        var userId = Guid.NewGuid();
        var tracker = NotificationBudgetTracker.Create(userId);
        var t0 = DateTimeOffset.UtcNow;

        tracker.RecordDispatch(NotificationPriority.Normal, t0);
        tracker.RecordDispatch(NotificationPriority.Normal, t0);
        tracker.CanSendNonUrgent(t0).Should().BeFalse();

        // 25 hours later
        var t1 = t0.AddHours(25);
        tracker.CanSendNonUrgent(t1).Should().BeTrue();
    }

    [Theory]
    [InlineData(21, 0, true)]   // 21:00 is in quiet hours (20:00 - 08:00)
    [InlineData(23, 30, true)]  // 23:30 is in quiet hours
    [InlineData(3, 0, true)]    // 03:00 is in quiet hours
    [InlineData(7, 59, true)]   // 07:59 is in quiet hours
    [InlineData(8, 0, false)]   // 08:00 is outside quiet hours
    [InlineData(12, 0, false)]  // 12:00 is outside quiet hours
    [InlineData(19, 59, false)] // 19:59 is outside quiet hours
    public void QuietHours_EvaluationAcrossMidnight_BR_NOTIFY_04_05(int hour, int minute, bool expectedQuiet)
    {
        var userId = Guid.NewGuid();
        var pref = NotificationPreference.CreateDefault(userId);
        // Default is 20:00 to 08:00

        var testTime = new DateTimeOffset(2026, 8, 26, hour, minute, 0, TimeSpan.Zero);
        pref.IsInQuietHours(testTime).Should().Be(expectedQuiet);
    }

    [Fact]
    public void CategoryOptOut_DisablesSpecificNotifications_BR_NOTIFY_04()
    {
        var userId = Guid.NewGuid();
        var pref = NotificationPreference.CreateDefault(userId);

        pref.IsCategoryEnabled(NotificationCategory.Community).Should().BeTrue();

        // User opts out of Community notifications
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
            systemAccountCategoryEnabled: true
        );

        pref.IsCategoryEnabled(NotificationCategory.Community).Should().BeFalse();
        pref.IsCategoryEnabled(NotificationCategory.HelpRequests).Should().BeTrue();
    }
}
