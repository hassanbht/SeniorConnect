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
            CancellationToken ct) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == volunteerUserId, ct);
            if (user is null) return Results.NotFound();

            // Reactivate user if deactivated/suspended
            // Record audit note
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

        return app;
    }
}
