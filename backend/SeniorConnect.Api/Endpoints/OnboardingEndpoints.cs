using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SeniorConnect.Api.Common;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Organizations.Contracts;
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

        // BR-ONBOARD-01: the applicant sees their own application; org staff
        // see applications to their org. Anyone else gets 404.
        onbGroup.MapGet("/applications/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            IOrganizationAccessGuard guard,
            IOnboardingService onboardingService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return Results.Unauthorized();

            var result = await onboardingService.GetApplicationByIdAsync(id, ct);
            if (result.IsFailure || result.Value!.UserId == userId) return result.ToHttpResult();

            var auth = await AuthorizeStaffForApplicationAsync(guard, userId, result.Value, ct);
            return auth.IsFailure ? Result.Failure(Error.NotFound("VolunteerApplication")).ToHttpResult() : result.ToHttpResult();
        })
        .WithName("GetApplicationById")
        .Produces<VolunteerApplicationDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        onbGroup.MapGet("/organizations/{orgId:guid}/applications", async (
            Guid orgId,
            ClaimsPrincipal user,
            IOrganizationAccessGuard guard,
            IOnboardingService onboardingService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return Results.Unauthorized();
            var auth = await guard.EnsureStaffAsync(userId, orgId, ct);
            if (auth.IsFailure) return auth.ToHttpResult();

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
            IOrganizationAccessGuard guard,
            IOnboardingService onboardingService,
            CancellationToken ct) =>
        {
            var staffUserId = user.GetUserId();
            if (staffUserId is null) return Results.Unauthorized();

            var denied = await AuthorizeReviewerAsync(guard, onboardingService, staffUserId.Value, appId, ct);
            if (denied is not null) return denied;

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
            IOrganizationAccessGuard guard,
            IOnboardingService onboardingService,
            CancellationToken ct) =>
        {
            var staffUserId = user.GetUserId();
            if (staffUserId is null) return Results.Unauthorized();

            var denied = await AuthorizeReviewerAsync(guard, onboardingService, staffUserId.Value, id, ct);
            if (denied is not null) return denied;

            var result = await onboardingService.DecideApplicationAsync(id, request, staffUserId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("DecideApplication")
        .Produces<VolunteerApplicationDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<Result> AuthorizeStaffForApplicationAsync(
        IOrganizationAccessGuard guard, Guid userId, VolunteerApplicationDto application, CancellationToken ct) =>
        application.OrganizationId is { } orgId
            ? await guard.EnsureStaffAsync(userId, orgId, ct)
            : Error.NotFound("VolunteerApplication");

    /// <summary>
    /// Pipeline steps and decisions: staff of the application's organization
    /// only, and never the applicant themselves (no self-approval).
    /// </summary>
    private static async Task<IResult?> AuthorizeReviewerAsync(
        IOrganizationAccessGuard guard, IOnboardingService onboardingService, Guid userId, Guid applicationId, CancellationToken ct)
    {
        var application = await onboardingService.GetApplicationByIdAsync(applicationId, ct);
        if (application.IsFailure) return application.ToHttpResult();

        var auth = await AuthorizeStaffForApplicationAsync(guard, userId, application.Value!, ct);
        if (auth.IsFailure) return auth.ToHttpResult();

        return application.Value!.UserId == userId
            ? Result.Failure(new Error("SELF_DECISION_FORBIDDEN", "You cannot review your own application.", ErrorKind.Forbidden)).ToHttpResult()
            : null;
    }
}
