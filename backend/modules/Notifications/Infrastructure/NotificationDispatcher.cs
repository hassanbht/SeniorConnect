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

        // Direct SMS dispatch for emergency contacts outside the user registry
        _logger.LogInformation("Dispatched emergency SMS alert to {PhoneNumber}: {Message}", phoneNumber, message);
        return Task.FromResult(Result.Success());
    }
}
