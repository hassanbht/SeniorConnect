using SeniorConnect.Modules.Notifications.Domain;

namespace SeniorConnect.Modules.Notifications.Application;

public sealed record NotificationMessageDto(
    Guid Id,
    Guid RecipientUserId,
    NotificationCategory Category,
    NotificationPriority Priority,
    NotificationChannel Channel,
    NotificationStatus Status,
    string Title,
    string Body,
    string? PayloadJson,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DispatchedAtUtc,
    DateTimeOffset? ReadAtUtc,
    bool IsRead);

public sealed record NotificationPreferenceDto(
    Guid Id,
    Guid UserId,
    bool PushEnabled,
    bool SmsEnabled,
    bool InAppEnabled,
    bool QuietHoursEnabled,
    TimeSpan QuietHoursStart,
    TimeSpan QuietHoursEnd,
    bool HelpRequestsCategoryEnabled,
    bool CommunityCategoryEnabled,
    bool FamilyWelfareCategoryEnabled,
    bool SystemAccountCategoryEnabled);

public sealed record UpdateNotificationPreferenceRequest(
    bool PushEnabled,
    bool SmsEnabled,
    bool InAppEnabled,
    bool QuietHoursEnabled,
    TimeSpan QuietHoursStart,
    TimeSpan QuietHoursEnd,
    bool HelpRequestsCategoryEnabled,
    bool CommunityCategoryEnabled,
    bool FamilyWelfareCategoryEnabled,
    bool SystemAccountCategoryEnabled);

public sealed record DispatchNotificationRequest(
    Guid RecipientUserId,
    NotificationCategory Category,
    NotificationPriority Priority,
    NotificationChannel PreferredChannel,
    string Title,
    string Body,
    string? PayloadJson = null);
