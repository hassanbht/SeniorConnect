using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Notifications.Application;
using SeniorConnect.Modules.Notifications.Domain;

namespace SeniorConnect.Modules.Notifications.Infrastructure;

public sealed class NotificationService : INotificationService
{
    private readonly INotificationsDbContext _db;

    public NotificationService(INotificationsDbContext db)
    {
        _db = db;
    }

    public async Task<Result<NotificationMessageDto>> DispatchNotificationAsync(
        DispatchNotificationRequest request,
        CancellationToken ct = default)
    {
        var pref = await _db.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == request.RecipientUserId, ct);

        if (pref is null)
        {
            pref = NotificationPreference.CreateDefault(request.RecipientUserId);
            _db.NotificationPreferences.Add(pref);
        }

        // Check if category is enabled (unless critical safety)
        if (request.Priority != NotificationPriority.CriticalSafety && !pref.IsCategoryEnabled(request.Category))
        {
            return Error.Conflict("CATEGORY_MUTED", "The recipient has muted this notification category.");
        }

        // Check budget tracker for normal priority notifications
        var tracker = await _db.NotificationBudgetTrackers
            .FirstOrDefaultAsync(t => t.UserId == request.RecipientUserId, ct);

        if (tracker is null)
        {
            tracker = NotificationBudgetTracker.Create(request.RecipientUserId);
            _db.NotificationBudgetTrackers.Add(tracker);
        }

        var now = DateTimeOffset.UtcNow;

        if (request.Priority == NotificationPriority.Normal && !tracker.CanSendNonUrgent(now))
        {
            var suppressedMsgResult = NotificationMessage.Create(
                request.RecipientUserId,
                request.Category,
                request.Priority,
                request.PreferredChannel,
                request.Title,
                request.Body,
                request.PayloadJson);

            if (suppressedMsgResult.IsSuccess)
            {
                var msg = suppressedMsgResult.Value!;
                msg.SuppressBudgetExceeded();
                _db.NotificationMessages.Add(msg);
                await _db.SaveChangesAsync(ct);
                return MapToDto(msg);
            }
        }

        // Check quiet hours
        var inQuietHours = pref.IsInQuietHours(now);

        var msgResult = NotificationMessage.Create(
            request.RecipientUserId,
            request.Category,
            request.Priority,
            request.PreferredChannel,
            request.Title,
            request.Body,
            request.PayloadJson);

        if (msgResult.IsFailure) return msgResult.Error!;

        var notification = msgResult.Value!;

        if (!inQuietHours || request.Priority >= NotificationPriority.Urgent)
        {
            notification.MarkDispatched();
        }

        tracker.RecordDispatch(request.Priority, now);
        _db.NotificationMessages.Add(notification);

        await _db.SaveChangesAsync(ct);
        return MapToDto(notification);
    }

    public async Task<Result<IReadOnlyList<NotificationMessageDto>>> GetUserNotificationsAsync(
        Guid userId,
        bool unreadOnly = false,
        CancellationToken ct = default)
    {
        var query = _db.NotificationMessages
            .Where(m => m.RecipientUserId == userId && m.Status != NotificationStatus.SuppressedBudgetExceeded);

        if (unreadOnly)
        {
            query = query.Where(m => !m.ReadAtUtc.HasValue);
        }

        var list = await query
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(50)
            .ToListAsync(ct);

        return list.Select(MapToDto).ToList();
    }

    public async Task<Result> MarkAsReadAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken ct = default)
    {
        var msg = await _db.NotificationMessages
            .FirstOrDefaultAsync(m => m.Id == notificationId && m.RecipientUserId == userId, ct);

        if (msg is null)
        {
            return Error.NotFound("NotificationMessage");
        }

        msg.MarkRead();
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<NotificationPreferenceDto>> GetPreferencesAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var pref = await _db.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (pref is null)
        {
            pref = NotificationPreference.CreateDefault(userId);
            _db.NotificationPreferences.Add(pref);
            await _db.SaveChangesAsync(ct);
        }

        return MapPrefToDto(pref);
    }

    public async Task<Result<NotificationPreferenceDto>> UpdatePreferencesAsync(
        Guid userId,
        UpdateNotificationPreferenceRequest request,
        CancellationToken ct = default)
    {
        var pref = await _db.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (pref is null)
        {
            pref = NotificationPreference.CreateDefault(userId);
            _db.NotificationPreferences.Add(pref);
        }

        pref.Update(
            request.PushEnabled,
            request.SmsEnabled,
            request.InAppEnabled,
            request.QuietHoursEnabled,
            request.QuietHoursStart,
            request.QuietHoursEnd,
            request.HelpRequestsCategoryEnabled,
            request.CommunityCategoryEnabled,
            request.FamilyWelfareCategoryEnabled,
            request.SystemAccountCategoryEnabled);

        await _db.SaveChangesAsync(ct);
        return MapPrefToDto(pref);
    }

    private static NotificationMessageDto MapToDto(NotificationMessage m) =>
        new(
            m.Id,
            m.RecipientUserId,
            m.Category,
            m.Priority,
            m.Channel,
            m.Status,
            m.Title,
            m.Body,
            m.PayloadJson,
            m.CreatedAtUtc,
            m.DispatchedAtUtc,
            m.ReadAtUtc,
            m.IsRead);

    private static NotificationPreferenceDto MapPrefToDto(NotificationPreference p) =>
        new(
            p.Id,
            p.UserId,
            p.PushEnabled,
            p.SmsEnabled,
            p.InAppEnabled,
            p.QuietHoursEnabled,
            p.QuietHoursStart,
            p.QuietHoursEnd,
            p.HelpRequestsCategoryEnabled,
            p.CommunityCategoryEnabled,
            p.FamilyWelfareCategoryEnabled,
            p.SystemAccountCategoryEnabled);
}
