namespace SeniorConnect.Modules.Notifications.Domain;

public sealed class NotificationBudgetTracker
{
    public const int MaxDailyNonUrgentNotifications = 2;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset WindowStartUtc { get; private set; }
    public int NonUrgentCount { get; private set; }

    private NotificationBudgetTracker() { }

    public static NotificationBudgetTracker Create(Guid userId, DateTimeOffset? initialWindowStart = null)
    {
        return new NotificationBudgetTracker
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            WindowStartUtc = initialWindowStart ?? DateTimeOffset.UtcNow,
            NonUrgentCount = 0
        };
    }

    public bool CanSendNonUrgent(DateTimeOffset nowUtc)
    {
        ResetWindowIfExpired(nowUtc);
        return NonUrgentCount < MaxDailyNonUrgentNotifications;
    }

    public void RecordDispatch(NotificationPriority priority, DateTimeOffset nowUtc)
    {
        ResetWindowIfExpired(nowUtc);

        // Only count non-urgent/normal notifications against budget
        if (priority == NotificationPriority.Normal)
        {
            NonUrgentCount++;
        }
    }

    private void ResetWindowIfExpired(DateTimeOffset nowUtc)
    {
        if (nowUtc < WindowStartUtc || nowUtc - WindowStartUtc >= TimeSpan.FromHours(24))
        {
            WindowStartUtc = nowUtc;
            NonUrgentCount = 0;
        }
    }
}
