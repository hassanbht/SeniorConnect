namespace SeniorConnect.Modules.Notifications.Domain;

public enum NotificationChannel
{
    InApp = 0,
    Push = 1,
    Sms = 2
}

public enum NotificationPriority
{
    Normal = 0,
    Urgent = 1,
    CriticalSafety = 2
}

public enum NotificationCategory
{
    HelpRequests = 0,
    Community = 1,
    FamilyWelfare = 2,
    SystemAccount = 3
}

public enum NotificationStatus
{
    Queued = 0,
    Dispatched = 1,
    Delivered = 2,
    Failed = 3,
    Read = 4,
    SuppressedBudgetExceeded = 5
}
