using Microsoft.Extensions.Logging;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Notifications.Application;
using SeniorConnect.Modules.Notifications.Contracts;
using SeniorConnect.Modules.Notifications.Domain;

namespace SeniorConnect.Modules.Notifications.Infrastructure;

public sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        INotificationService notificationService,
        ILogger<NotificationDispatcher> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<Result> DispatchAsync(
        NotificationDispatchCommand command,
        CancellationToken ct = default)
    {
        var category = Enum.TryParse<NotificationCategory>(command.Category, true, out var parsedCategory)
            ? parsedCategory
            : NotificationCategory.FamilyWelfare;

        var priority = Enum.TryParse<NotificationPriority>(command.Priority, true, out var parsedPriority)
            ? parsedPriority
            : NotificationPriority.CriticalSafety;

        var request = new DispatchNotificationRequest(
            RecipientUserId: command.RecipientUserId,
            Category: category,
            Priority: priority,
            PreferredChannel: NotificationChannel.Push,
            Title: command.Title,
            Body: command.Body,
            PayloadJson: command.PayloadJson);

        var result = await _notificationService.DispatchNotificationAsync(request, ct);
        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to dispatch notification to user {UserId}: {Error}", command.RecipientUserId, result.Error?.Detail);
            return result.Error!;
        }

        return Result.Success();
    }

    public Task<Result> DispatchDirectSmsAsync(
        string phoneNumber,
        string message,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return Task.FromResult(Result.Failure(Error.Validation("Phone number is required.")));
        }

        // No SMS gateway is contracted yet (P7-01). A safety alert must never be
        // reported as delivered when it was not, so the caller sees a failure.
        // Never log the number or the message: they carry safety details.
        _logger.LogError("Emergency SMS alert could not be sent: no SMS gateway configured.");
        return Task.FromResult(Result.Failure(Error.ProviderUnavailable(
            "SMS_PROVIDER_NOT_CONFIGURED", "Emergency text messages cannot be sent right now.")));
    }
}
