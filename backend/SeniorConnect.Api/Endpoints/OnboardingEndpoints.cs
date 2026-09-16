using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SeniorConnect.Api.Common;
using SeniorConnect.Modules.TrustSafety.Application;

namespace SeniorConnect.Api.Endpoints;

public static class OnboardingEndpoints
{
    public static IEndpointRouteBuilder MapOnboardingEndpoints(this IEndpointRouteBuilder app)
    {
        var onbGroup = app.MapGroup("/api/v1/onboarding")
            .WithTags("Onboarding")
            .RequireAuthorization();

        onbGroup.MapPost("/apply", async (
            ApplyVolunteerRequest request,
            ClaimsPrincipal user,
            IOnboardingService onboardingService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await onboardingService.ApplyAsync(userId.Value, request, ct);
            return result.ToHttpResult("/api/v1/onboarding/applications/" + result.Value?.Id);
        })
        .WithName("ApplyVolunteer")
        .Produces<VolunteerApplicationDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status409Conflict);

        onbGroup.MapGet("/my-applications", async (
            ClaimsPrincipal user,
            IOnboardingService onboardingService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await onboardingService.GetUserApplicationsAsync(userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetMyVolunteerApplications")
        .Produces<IReadOnlyList<VolunteerApplicationDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        onbGroup.MapGet("/applications/{id:guid}", async (
            Guid id,
            IOnboardingService onboardingService,
            CancellationToken ct) =>
        {
            var result = await onboardingService.GetApplicationByIdAsync(id, ct);
            return result.ToHttpResult();
        })
        .WithName("GetApplicationById")
        .Produces<VolunteerApplicationDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        onbGroup.MapGet("/organizations/{orgId:guid}/applications", async (
            Guid orgId,
            IOnboardingService onboardingService,
            CancellationToken ct) =>
        {
            var result = await onboardingService.GetOrganizationApplicationsAsync(orgId, ct);
            return result.ToHttpResult();
        })
        .WithName("GetOrganizationApplications")
        .Produces<IReadOnlyList<VolunteerApplicationDto>>(StatusCodes.Status200OK);

        onbGroup.MapPut("/applications/{appId:guid}/steps/{stepId:guid}", async (
            Guid appId,
            Guid stepId,
            UpdateStepRequest request,
            ClaimsPrincipal user,
            IOnboardingService onboardingService,
            CancellationToken ct) =>
        {
            var staffUserId = user.GetUserId();
            if (staffUserId is null) return Results.Unauthorized();

            var result = await onboardingService.UpdateStepAsync(appId, stepId, request, staffUserId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("UpdateApplicationStep")
        .Produces<VolunteerApplicationDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        onbGroup.MapPost("/applications/{id:guid}:decide", async (
            Guid id,
            DecideApplicationRequest request,
            ClaimsPrincipal user,
            IOnboardingService onboardingService,
            CancellationToken ct) =>
        {
            var staffUserId = user.GetUserId();
            if (staffUserId is null) return Results.Unauthorized();

            var result = await onboardingService.DecideApplicationAsync(id, request, staffUserId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("DecideApplication")
        .Produces<VolunteerApplicationDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
