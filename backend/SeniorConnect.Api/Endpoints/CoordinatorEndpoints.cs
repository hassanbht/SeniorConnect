using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Api.Common;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.HelpRequests.Application;
using SeniorConnect.Modules.HelpRequests.Domain;
using SeniorConnect.Modules.Notifications.Application;
using SeniorConnect.Modules.Notifications.Domain;
using SeniorConnect.Modules.Profiles.Domain;
using SeniorConnect.Modules.TrustSafety.Domain;

namespace SeniorConnect.Api.Endpoints;

public sealed record CoordinatorTriageDto(
    int UnconfirmedActivitiesCount,
    int DisputedActivitiesCount,
    int PendingApplicationsCount,
    IReadOnlyList<ActivityDto> RecentUnconfirmedActivities);

public sealed record BulkEntryItem(
    Guid VolunteerUserId,
    Guid? SubjectUserId,
    Guid CategoryId,
    DateOnly OccurredOn,
    int DurationMinutes,
    LocationType LocationType,
    string? Notes,
    InsuranceContext InsuranceContext,
    TransportMode TransportMode = TransportMode.None);

public sealed record BulkEntryRequest(
    Guid OrganizationId,
    IReadOnlyList<BulkEntryItem> Activities);

public sealed record VolunteerRosterItemDto(
    Guid UserId,
    string DisplayName,
    string? Phone,
    string? Email,
    string RosterStatus,
    int TotalHoursLogged,
    DateOnly? LastActivityDate);

public sealed record ReactivateVolunteerRequest(
    string? Notes = null);

public sealed record BroadcastVolunteerMessageRequest(
    Guid OrganizationId,
    string Title,
    string Message,
    string? RosterStatusFilter = null);

public static class CoordinatorEndpoints
{
    public static IEndpointRouteBuilder MapCoordinatorEndpoints(this IEndpointRouteBuilder app)
    {
        var coordGroup = app.MapGroup("/api/v1/coordinator")
            .WithTags("Coordinator")
            .RequireAuthorization();

        coordGroup.MapGet("/triage", async (
            Guid organizationId,
            SeniorConnectDbContext db,
            CancellationToken ct) =>
        {
            var unconfirmedQuery = db.Activities
                .Where(a => a.OrganizationId == organizationId && a.Status == ActivityStatus.Logged && !a.IsDeleted);

            var unconfirmedCount = await unconfirmedQuery.CountAsync(ct);

            var disputedCount = await db.Activities
                .Where(a => a.OrganizationId == organizationId && a.Status == ActivityStatus.Disputed && !a.IsDeleted)
                .CountAsync(ct);

            var pendingAppsCount = await db.VolunteerApplications
                .Where(a => a.OrganizationId == organizationId && a.Status == ApplicationStatus.Open)
                .CountAsync(ct);

            var recentUnconfirmed = await unconfirmedQuery
                .OrderByDescending(a => a.LoggedAtUtc)
                .Take(10)
                .Select(a => new ActivityDto(
                    a.Id,
                    a.OrganizationId,
                    a.BranchId,
                    a.VolunteerUserId,
                    a.SubjectUserId,
                    a.CategoryId,
                    a.OccurredOn,
                    a.DurationMinutes,
                    a.LocationType,
                    a.Notes,
                    a.InsuranceContext,
                    a.TransportMode,
                    a.Source,
                    a.LoggedByUserId,
                    a.LoggedAtUtc,
                    a.ConfirmedByUserId,
                    a.ConfirmedAtUtc,
                    a.Status))
                .ToListAsync(ct);

            var triage = new CoordinatorTriageDto(
                UnconfirmedActivitiesCount: unconfirmedCount,
                DisputedActivitiesCount: disputedCount,
                PendingApplicationsCount: pendingAppsCount,
                RecentUnconfirmedActivities: recentUnconfirmed);

            return Results.Ok(triage);
        })
        .WithName("GetCoordinatorTriage")
        .Produces<CoordinatorTriageDto>(StatusCodes.Status200OK);

        coordGroup.MapGet("/volunteers", async (
            Guid organizationId,
            SeniorConnectDbContext db,
            CancellationToken ct) =>
        {
            var users = await db.Users
                .Where(u => !u.IsDeleted)
                .Select(u => new
                {
                    u.Id,
                    u.DisplayName,
                    u.Phone,
                    u.Email,
                    Status = u.Status.ToString()
                })
                .ToListAsync(ct);

            var activities = await db.Activities
                .Where(a => a.OrganizationId == organizationId && !a.IsDeleted)
                .GroupBy(a => a.VolunteerUserId)
                .Select(g => new
                {
                    VolunteerUserId = g.Key,
                    TotalMinutes = g.Sum(a => a.DurationMinutes),
                    LastDate = g.Max(a => (DateOnly?)a.OccurredOn)
                })
                .ToDictionaryAsync(g => g.VolunteerUserId, ct);

            var roster = users.Select(u =>
            {
                activities.TryGetValue(u.Id, out var act);
                var totalHours = (act?.TotalMinutes ?? 0) / 60;
                var lastDate = act?.LastDate;

                string rosterStatus = "NeverActivated";
                if (lastDate.HasValue)
                {
                    var monthsAgo = (DateTime.UtcNow.Year - lastDate.Value.Year) * 12 + DateTime.UtcNow.Month - lastDate.Value.Month;
                    rosterStatus = monthsAgo <= 3 ? "Active" : monthsAgo <= 6 ? "Dormant" : "Inactive";
                }

                return new VolunteerRosterItemDto(
                    UserId: u.Id,
                    DisplayName: u.DisplayName,
                    Phone: u.Phone,
                    Email: u.Email,
                    RosterStatus: rosterStatus,
                    TotalHoursLogged: totalHours,
                    LastActivityDate: lastDate);
            }).ToList();

            return Results.Ok(roster);
        })
        .WithName("GetVolunteerRoster")
        .Produces<IReadOnlyList<VolunteerRosterItemDto>>(StatusCodes.Status200OK);

        coordGroup.MapPost("/volunteers/{volunteerUserId:guid}:reactivate", async (
            Guid volunteerUserId,
            ReactivateVolunteerRequest request,
            SeniorConnectDbContext db,
            INotificationService notificationService,
            CancellationToken ct) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == volunteerUserId && !u.IsDeleted, ct);
            if (user is null) return Results.NotFound();

            var profile = await db.VolunteerProfiles.FirstOrDefaultAsync(p => p.UserId == volunteerUserId, ct);
            if (profile is not null && !profile.IsAcceptingRequests)
            {
                profile.UpdateStatus(true);
            }

            // P2-19: Send reactivation notification invitation to volunteer
            await notificationService.DispatchNotificationAsync(new DispatchNotificationRequest(
                RecipientUserId: volunteerUserId,
                Category: NotificationCategory.HelpRequests,
                Priority: NotificationPriority.Normal,
                PreferredChannel: NotificationChannel.InApp,
                Title: "Reaktivierung: Willkommen zurück!",
                Body: request.Notes ?? "Dein Profil wurde durch den Koordinator wieder auf aktiv gesetzt."), ct);

            await db.SaveChangesAsync(ct);

            return Results.Ok(new { Message = "Volunteer reactivated successfully.", VolunteerUserId = volunteerUserId });
        })
        .WithName("ReactivateVolunteer")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        coordGroup.MapPost("/volunteers/reminders:send-monthly", async (
            Guid organizationId,
            SeniorConnectDbContext db,
            INotificationService notificationService,
            CancellationToken ct) =>
        {
            var currentMonth = DateOnly.FromDateTime(DateTime.UtcNow);
            var firstOfMonth = new DateOnly(currentMonth.Year, currentMonth.Month, 1);

            // Active volunteers with 0 activities in current month
            var activeVolunteerIds = await db.VolunteerProfiles
                .Select(p => p.UserId)
                .Distinct()
                .ToListAsync(ct);

            var activeThisMonth = await db.Activities
                .Where(a => a.OrganizationId == organizationId && a.OccurredOn >= firstOfMonth && !a.IsDeleted)
                .Select(a => a.VolunteerUserId)
                .Distinct()
                .ToListAsync(ct);

            var silentVolunteerIds = activeVolunteerIds.Except(activeThisMonth).ToList();
            int dispatchedCount = 0;

            foreach (var volId in silentVolunteerIds)
            {
                var dispatchResult = await notificationService.DispatchNotificationAsync(new DispatchNotificationRequest(
                    RecipientUserId: volId,
                    Category: NotificationCategory.HelpRequests,
                    Priority: NotificationPriority.Normal,
                    PreferredChannel: NotificationChannel.InApp,
                    Title: "Monatsrückblick: Stunden erfassen",
                    Body: "Hast du diesen Monat Nachbarschaftshilfe geleistet? Trage deine Stunden unkompliziert ein."), ct);

                if (dispatchResult.IsSuccess) dispatchedCount++;
            }

            return Results.Ok(new { SilentVolunteersCount = silentVolunteerIds.Count, RemindersDispatched = dispatchedCount });
        })
        .WithName("SendMonthlyVolunteerReminders")
        .Produces(StatusCodes.Status200OK);

        coordGroup.MapPost("/activities:bulk-entry", async (
            BulkEntryRequest request,
            ClaimsPrincipal user,
            IActivityService activityService,
            CancellationToken ct) =>
        {
            var staffUserId = user.GetUserId();
            if (staffUserId is null) return Results.Unauthorized();

            var loggedList = new List<ActivityDto>();
            foreach (var item in request.Activities)
            {
                var logReq = new LogActivityRequest(
                    OrganizationId: request.OrganizationId,
                    SubjectUserId: item.SubjectUserId,
                    CategoryId: item.CategoryId,
                    OccurredOn: item.OccurredOn,
                    DurationMinutes: item.DurationMinutes,
                    LocationType: item.LocationType,
                    Notes: item.Notes,
                    InsuranceContext: item.InsuranceContext,
                    TransportMode: item.TransportMode);

                var logResult = await activityService.LogActivityAsync(
                    item.VolunteerUserId,
                    logReq,
                    source: ActivitySource.CoordinatorLogged,
                    cancellationToken: ct);

                if (logResult.IsSuccess && logResult.Value is not null)
                {
                    loggedList.Add(logResult.Value);
                }
            }

            return Results.Ok(loggedList);
        })
        .WithName("BulkEntryActivities")
        .Produces<IReadOnlyList<ActivityDto>>(StatusCodes.Status200OK);

        // P2-24 / P2-26: Attention Queue - Expiring Verifications
        coordGroup.MapGet("/attention/expiring-verifications", async (
            SeniorConnectDbContext db,
            CancellationToken ct) =>
        {
            var now = DateTimeOffset.UtcNow;
            var in30Days = now.AddDays(30);

            var expiring = await db.Verifications
                .Where(v => v.Status == SeniorConnect.Modules.Identity.Domain.VerificationStatus.Verified && v.ValidUntilUtc != null && v.ValidUntilUtc <= in30Days && v.ValidUntilUtc > now)
                .Select(v => new
                {
                    v.Id,
                    v.UserId,
                    Type = v.Type.ToString(),
                    v.VerifiedAtUtc,
                    v.ValidUntilUtc,
                    DaysRemaining = (int)(v.ValidUntilUtc!.Value - now).TotalDays
                })
                .OrderBy(v => v.ValidUntilUtc)
                .ToListAsync(ct);

            return Results.Ok(expiring);
        })
        .WithName("GetExpiringVerificationsAttention")
        .Produces(StatusCodes.Status200OK);

        // P2-28: Attention Queue - Unconfirmed Hours
        coordGroup.MapGet("/attention/unconfirmed-hours", async (
            Guid organizationId,
            SeniorConnectDbContext db,
            CancellationToken ct) =>
        {
            var unconfirmed = await db.Activities
                .Where(a => a.OrganizationId == organizationId && a.Status == ActivityStatus.Logged && !a.IsDeleted)
                .OrderByDescending(a => a.LoggedAtUtc)
                .Select(a => new ActivityDto(
                    a.Id,
                    a.OrganizationId,
                    a.BranchId,
                    a.VolunteerUserId,
                    a.SubjectUserId,
                    a.CategoryId,
                    a.OccurredOn,
                    a.DurationMinutes,
                    a.LocationType,
                    a.Notes,
                    a.InsuranceContext,
                    a.TransportMode,
                    a.Source,
                    a.LoggedByUserId,
                    a.LoggedAtUtc,
                    a.ConfirmedByUserId,
                    a.ConfirmedAtUtc,
                    a.Status))
                .ToListAsync(ct);

            return Results.Ok(unconfirmed);
        })
        .WithName("GetUnconfirmedHoursAttention")
        .Produces<IReadOnlyList<ActivityDto>>(StatusCodes.Status200OK);

        // P2-16: Attention Queue - Silent Volunteers (>60 days inactive)
        coordGroup.MapGet("/attention/silent-volunteers", async (
            Guid organizationId,
            SeniorConnectDbContext db,
            CancellationToken ct) =>
        {
            var sixtyDaysAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60));

            var activeVolunteerIds = await db.VolunteerProfiles
                .Where(p => p.IsAcceptingRequests)
                .Select(p => p.UserId)
                .Distinct()
                .ToListAsync(ct);

            var recentlyActiveVolunteerIds = await db.Activities
                .Where(a => a.OrganizationId == organizationId && a.OccurredOn >= sixtyDaysAgo && !a.IsDeleted)
                .Select(a => a.VolunteerUserId)
                .Distinct()
                .ToListAsync(ct);

            var silentVolunteerIds = activeVolunteerIds.Except(recentlyActiveVolunteerIds).ToList();

            var silentVolunteers = await db.Users
                .Where(u => silentVolunteerIds.Contains(u.Id) && !u.IsDeleted)
                .Select(u => new
                {
                    u.Id,
                    u.DisplayName,
                    u.Phone,
                    u.Email,
                    u.CreatedAtUtc
                })
                .ToListAsync(ct);

            return Results.Ok(silentVolunteers);
        })
        .WithName("GetSilentVolunteersAttention")
        .Produces(StatusCodes.Status200OK);

        // P2-25: Advanced Volunteer Roster Search & Filtering
        coordGroup.MapGet("/volunteers/search", async (
            Guid organizationId,
            string? query,
            string? rosterStatus,
            SeniorConnectDbContext db,
            CancellationToken ct) =>
        {
            var users = await db.Users
                .Where(u => !u.IsDeleted)
                .Select(u => new
                {
                    u.Id,
                    u.DisplayName,
                    u.Phone,
                    u.Email
                })
                .ToListAsync(ct);

            if (!string.IsNullOrWhiteSpace(query))
            {
                var trimmed = query.Trim();
                users = users.Where(u => u.DisplayName.Contains(trimmed, StringComparison.OrdinalIgnoreCase) || (u.Email != null && u.Email.Contains(trimmed, StringComparison.OrdinalIgnoreCase))).ToList();
            }

            var activities = await db.Activities
                .Where(a => a.OrganizationId == organizationId && !a.IsDeleted)
                .GroupBy(a => a.VolunteerUserId)
                .Select(g => new
                {
                    VolunteerUserId = g.Key,
                    TotalMinutes = g.Sum(a => a.DurationMinutes),
                    LastDate = g.Max(a => (DateOnly?)a.OccurredOn)
                })
                .ToDictionaryAsync(g => g.VolunteerUserId, ct);

            var roster = users.Select(u =>
            {
                activities.TryGetValue(u.Id, out var act);
                var totalHours = (act?.TotalMinutes ?? 0) / 60;
                var lastDate = act?.LastDate;

                string status = "NeverActivated";
                if (lastDate.HasValue)
                {
                    var monthsAgo = (DateTime.UtcNow.Year - lastDate.Value.Year) * 12 + DateTime.UtcNow.Month - lastDate.Value.Month;
                    status = monthsAgo <= 3 ? "Active" : monthsAgo <= 6 ? "Dormant" : "Inactive";
                }

                return new VolunteerRosterItemDto(
                    UserId: u.Id,
                    DisplayName: u.DisplayName,
                    Phone: u.Phone,
                    Email: u.Email,
                    RosterStatus: status,
                    TotalHoursLogged: totalHours,
                    LastActivityDate: lastDate);
            })
            .Where(r => string.IsNullOrWhiteSpace(rosterStatus) || string.Equals(r.RosterStatus, rosterStatus, StringComparison.OrdinalIgnoreCase))
            .ToList();

            return Results.Ok(roster);
        })
        .WithName("SearchVolunteerRoster")
        .Produces<IReadOnlyList<VolunteerRosterItemDto>>(StatusCodes.Status200OK);

        // P2-35: Bulk Notification Broadcast to Filtered Volunteers
        coordGroup.MapPost("/volunteers/broadcast", async (
            BroadcastVolunteerMessageRequest request,
            SeniorConnectDbContext db,
            INotificationService notificationService,
            CancellationToken ct) =>
        {
            var profileUserIds = await db.VolunteerProfiles
                .Where(p => p.IsAcceptingRequests)
                .Select(p => p.UserId)
                .Distinct()
                .ToListAsync(ct);

            int sentCount = 0;
            foreach (var volId in profileUserIds)
            {
                var result = await notificationService.DispatchNotificationAsync(new DispatchNotificationRequest(
                    RecipientUserId: volId,
                    Category: NotificationCategory.HelpRequests,
                    Priority: NotificationPriority.Normal,
                    PreferredChannel: NotificationChannel.InApp,
                    Title: request.Title,
                    Body: request.Message), ct);

                if (result.IsSuccess) sentCount++;
            }

            return Results.Ok(new
            {
                Message = "Broadcast dispatched successfully.",
                TargetCount = profileUserIds.Count,
                DeliveredCount = sentCount
            });
        })
        .WithName("BroadcastVolunteerMessage")
        .Produces(StatusCodes.Status200OK);

        return app;
    }
}
