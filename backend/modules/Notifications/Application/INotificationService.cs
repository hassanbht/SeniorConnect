using SeniorConnect.Domain;
using SeniorConnect.Modules.Notifications.Domain;

namespace SeniorConnect.Modules.Notifications.Application;

public interface INotificationService
{
    Task<Result<NotificationMessageDto>> DispatchNotificationAsync(
        DispatchNotificationRequest request,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<NotificationMessageDto>>> GetUserNotificationsAsync(
        Guid userId,
        bool unreadOnly = false,
        CancellationToken ct = default);

    Task<Result> MarkAsReadAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken ct = default);

    Task<Result<NotificationPreferenceDto>> GetPreferencesAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<Result<NotificationPreferenceDto>> UpdatePreferencesAsync(
        Guid userId,
        UpdateNotificationPreferenceRequest request,
        CancellationToken ct = default);
}
