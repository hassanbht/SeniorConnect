using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Notifications.Contracts;

public sealed record NotificationDispatchCommand(
    Guid RecipientUserId,
    string Category,
    string Priority,
    string Title,
    string Body,
    string? PayloadJson = null);

/// <summary>
/// P6-06: Cross-module contract allowing other modules (Family safety alerts) to
/// dispatch notifications to app users and direct SMS to emergency contacts.
/// </summary>
public interface INotificationDispatcher
{
    Task<Result> DispatchAsync(NotificationDispatchCommand command, CancellationToken ct = default);
    Task<Result> DispatchDirectSmsAsync(string phoneNumber, string message, CancellationToken ct = default);
}
