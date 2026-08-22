using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Notifications.Domain;

public sealed class NotificationPreference
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public bool PushEnabled { get; private set; } = true;
    public bool SmsEnabled { get; private set; } = true;
    public bool InAppEnabled { get; private set; } = true;
    public bool QuietHoursEnabled { get; private set; } = true;
    public TimeSpan QuietHoursStart { get; private set; } = new(20, 0, 0); // 20:00 default
    public TimeSpan QuietHoursEnd { get; private set; } = new(8, 0, 0);    // 08:00 default

    public bool HelpRequestsCategoryEnabled { get; private set; } = true;
    public bool CommunityCategoryEnabled { get; private set; } = true;
    public bool FamilyWelfareCategoryEnabled { get; private set; } = true;
    public bool SystemAccountCategoryEnabled { get; private set; } = true;

    private NotificationPreference() { }

    public static NotificationPreference CreateDefault(Guid userId)
    {
        return new NotificationPreference
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PushEnabled = true,
            SmsEnabled = true,
            InAppEnabled = true,
            QuietHoursEnabled = true,
            QuietHoursStart = new TimeSpan(20, 0, 0),
            QuietHoursEnd = new TimeSpan(8, 0, 0),
            HelpRequestsCategoryEnabled = true,
            CommunityCategoryEnabled = true,
            FamilyWelfareCategoryEnabled = true,
            SystemAccountCategoryEnabled = true
        };
    }

    public void Update(
        bool pushEnabled,
        bool smsEnabled,
        bool inAppEnabled,
        bool quietHoursEnabled,
        TimeSpan quietHoursStart,
        TimeSpan quietHoursEnd,
        bool helpRequestsCategoryEnabled,
        bool communityCategoryEnabled,
        bool familyWelfareCategoryEnabled,
        bool systemAccountCategoryEnabled)
    {
        PushEnabled = pushEnabled;
        SmsEnabled = smsEnabled;
        InAppEnabled = inAppEnabled;
        QuietHoursEnabled = quietHoursEnabled;
        QuietHoursStart = quietHoursStart;
        QuietHoursEnd = quietHoursEnd;
        HelpRequestsCategoryEnabled = helpRequestsCategoryEnabled;
        CommunityCategoryEnabled = communityCategoryEnabled;
        FamilyWelfareCategoryEnabled = familyWelfareCategoryEnabled;
        SystemAccountCategoryEnabled = systemAccountCategoryEnabled;
    }

    public bool IsInQuietHours(DateTimeOffset localTime)
    {
        if (!QuietHoursEnabled) return false;

        var time = localTime.TimeOfDay;
        if (QuietHoursStart < QuietHoursEnd)
        {
            return time >= QuietHoursStart && time < QuietHoursEnd;
        }
        else
        {
            // Crosses midnight (e.g. 20:00 to 08:00)
            return time >= QuietHoursStart || time < QuietHoursEnd;
        }
    }

    public bool IsCategoryEnabled(NotificationCategory category) => category switch
    {
        NotificationCategory.HelpRequests => HelpRequestsCategoryEnabled,
        NotificationCategory.Community => CommunityCategoryEnabled,
        NotificationCategory.FamilyWelfare => FamilyWelfareCategoryEnabled,
        NotificationCategory.SystemAccount => SystemAccountCategoryEnabled,
        _ => true
    };
}
