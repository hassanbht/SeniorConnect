using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Notifications.Application;
using SeniorConnect.Modules.Notifications.Domain;

namespace SeniorConnect.Infrastructure.BackgroundJobs;

/// <summary>
/// P2-16: shared logic for the silent-volunteer monthly reminder, called
/// both from the coordinator's manual "send now" endpoint and from
/// MonthlyVolunteerReminderHostedService's daily sweep. Idempotency lives on
/// VolunteerProfile itself (TryMarkMonthlyReminderSent), so calling this
/// from either or both places never sends more than one reminder per
/// volunteer per month.
/// </summary>
public static class VolunteerEngagementJobs
{
    public static async Task<(int SilentCount, int DispatchedCount)> SendMonthlySilentVolunteerRemindersAsync(
        SeniorConnectDbContext db,
        INotificationService notificationService,
        Guid organizationId,
        CancellationToken ct)
    {
        var currentMonth = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstOfMonth = new DateOnly(currentMonth.Year, currentMonth.Month, 1);

        var profiles = await db.VolunteerProfiles.ToListAsync(ct);

        var activeThisMonth = await db.Activities
            .Where(a => a.OrganizationId == organizationId && a.OccurredOn >= firstOfMonth && !a.IsDeleted)
            .Select(a => a.VolunteerUserId)
            .Distinct()
            .ToListAsync(ct);

        var silentVolunteers = profiles
            .Where(p => !activeThisMonth.Contains(p.UserId))
            .ToList();

        var dispatchedCount = 0;

        foreach (var profile in silentVolunteers)
        {
            if (!profile.TryMarkMonthlyReminderSent(firstOfMonth))
            {
                continue;
            }

            var dispatchResult = await notificationService.DispatchNotificationAsync(new DispatchNotificationRequest(
                RecipientUserId: profile.UserId,
                Category: NotificationCategory.HelpRequests,
                Priority: NotificationPriority.Normal,
                PreferredChannel: NotificationChannel.InApp,
                Title: "Monatsrückblick: Stunden erfassen",
                Body: "Hast du diesen Monat Nachbarschaftshilfe geleistet? Trage deine Stunden unkompliziert ein."), ct);

            if (dispatchResult.IsSuccess) dispatchedCount++;
        }

        if (dispatchedCount > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return (silentVolunteers.Count, dispatchedCount);
    }
}
