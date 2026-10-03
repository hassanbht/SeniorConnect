using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Notifications.Application;
using SeniorConnect.Modules.Notifications.Domain;
using SeniorConnect.Modules.Organizations.Contracts;
using SeniorConnect.Modules.Organizations.Domain;

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
        // Austrian calendar month, not UTC. "Silent" = no activity logged for
        // this org during the whole PREVIOUS month nor so far this month —
        // judging only the current month flagged everyone on the 1st.
        var today = RosterStatusPolicy.TodayInVienna();
        var firstOfMonth = new DateOnly(today.Year, today.Month, 1);
        var firstOfPreviousMonth = firstOfMonth.AddMonths(-1);

        // Only this organization's own active volunteers — never platform-wide.
        var volunteerIds = await db.OrganizationMemberships
            .Where(m => m.OrganizationId == organizationId
                     && m.Role == MembershipRole.Volunteer
                     && m.Status == MembershipStatus.Active)
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(ct);

        var profiles = await db.VolunteerProfiles
            .Where(p => volunteerIds.Contains(p.UserId))
            .ToListAsync(ct);

        var recentlyActive = await db.Activities
            .Where(a => a.OrganizationId == organizationId && a.OccurredOn >= firstOfPreviousMonth && !a.IsDeleted)
            .Select(a => a.VolunteerUserId)
            .Distinct()
            .ToListAsync(ct);

        var silentVolunteers = profiles
            .Where(p => !recentlyActive.Contains(p.UserId))
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
