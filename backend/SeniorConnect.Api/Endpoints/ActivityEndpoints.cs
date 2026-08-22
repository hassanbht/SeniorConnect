using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SeniorConnect.Api.Common;
using SeniorConnect.Modules.HelpRequests.Application;

namespace SeniorConnect.Api.Endpoints;

public static class ActivityEndpoints
{
    public static IEndpointRouteBuilder MapActivityEndpoints(this IEndpointRouteBuilder app)
    {
        var actGroup = app.MapGroup("/api/v1/activities")
            .WithTags("Activities")
            .RequireAuthorization();

        actGroup.MapPost("/", async (
            LogActivityRequest request,
            ClaimsPrincipal user,
            IActivityService activityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await activityService.LogActivityAsync(userId.Value, request, cancellationToken: ct);
            return result.ToHttpResult("/api/v1/activities/" + result.Value?.Id);
        })
        .WithName("LogActivity")
        .Produces<ActivityDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        actGroup.MapGet("/", async (
            Guid? organizationId,
            DateOnly? from,
            DateOnly? to,
            ClaimsPrincipal user,
            IActivityService activityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            if (organizationId.HasValue)
            {
                var orgResult = await activityService.GetOrganizationActivitiesAsync(organizationId.Value, from, to, ct);
                return orgResult.ToHttpResult();
            }

            var result = await activityService.GetVolunteerActivitiesAsync(userId.Value, from, to, ct);
            return result.ToHttpResult();
        })
        .WithName("GetActivities")
        .Produces<IReadOnlyList<ActivityDto>>(StatusCodes.Status200OK);

        actGroup.MapGet("/{id:guid}", async (
            Guid id,
            IActivityService activityService,
            CancellationToken ct) =>
        {
            var result = await activityService.GetActivityByIdAsync(id, ct);
            return result.ToHttpResult();
        })
        .WithName("GetActivityById")
        .Produces<ActivityDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        actGroup.MapPost("/{id:guid}:confirm", async (
            Guid id,
            ClaimsPrincipal user,
            IActivityService activityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await activityService.ConfirmActivityAsync(id, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("ConfirmActivity")
        .Produces<ActivityDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        actGroup.MapPost("/{id:guid}:dispute", async (
            Guid id,
            DisputeActivityRequest request,
            ClaimsPrincipal user,
            IActivityService activityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await activityService.DisputeActivityAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("DisputeActivity")
        .Produces<ActivityDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        actGroup.MapGet("/categories", async (
            IActivityService activityService,
            CancellationToken ct) =>
        {
            var result = await activityService.GetCategoriesAsync(ct);
            return result.ToHttpResult();
        })
        .WithName("GetActivityCategories")
        .Produces<IReadOnlyList<ActivityCategoryDto>>(StatusCodes.Status200OK);

        actGroup.MapGet("/referral-providers", async (
            string? referralGroup,
            string? regionCode,
            IActivityService activityService,
            CancellationToken ct) =>
        {
            var result = await activityService.GetReferralProvidersAsync(referralGroup, regionCode, ct);
            return result.ToHttpResult();
        })
        .WithName("GetReferralProviders")
        .Produces<IReadOnlyList<ReferralProviderDto>>(StatusCodes.Status200OK);

        return app;
    }
}
