using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Api.Common;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.HelpRequests.Application;
using SeniorConnect.Modules.HelpRequests.Domain;
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
