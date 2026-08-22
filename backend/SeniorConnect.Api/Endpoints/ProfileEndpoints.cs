using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SeniorConnect.Api.Common;
using SeniorConnect.Modules.Profiles.Application;

namespace SeniorConnect.Api.Endpoints;

public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var meGroup = app.MapGroup("/api/v1/me")
            .WithTags("User Profiles")
            .RequireAuthorization();

        meGroup.MapGet("/senior-profile", async (
            ClaimsPrincipal user,
            IProfileService profileService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await profileService.GetSeniorProfileAsync(userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetSeniorProfile")
        .Produces<SeniorProfileDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        meGroup.MapPut("/senior-profile", async (
            UpdateSeniorProfileRequest request,
            ClaimsPrincipal user,
            IProfileService profileService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await profileService.UpsertSeniorProfileAsync(userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("UpdateSeniorProfile")
        .Produces<SeniorProfileDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        meGroup.MapGet("/volunteer-profile", async (
            ClaimsPrincipal user,
            IProfileService profileService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await profileService.GetVolunteerProfileAsync(userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetVolunteerProfile")
        .Produces<VolunteerProfileDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        meGroup.MapPut("/volunteer-profile", async (
            UpdateVolunteerProfileRequest request,
            ClaimsPrincipal user,
            IProfileService profileService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await profileService.UpsertVolunteerProfileAsync(userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("UpdateVolunteerProfile")
        .Produces<VolunteerProfileDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        meGroup.MapGet("/availability", async (
            ClaimsPrincipal user,
            IProfileService profileService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await profileService.GetAvailabilityAsync(userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetAvailability")
        .Produces<IReadOnlyList<AvailabilitySlotDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        meGroup.MapPut("/availability", async (
            UpdateAvailabilityRequest request,
            ClaimsPrincipal user,
            IProfileService profileService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await profileService.UpdateAvailabilityAsync(userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("UpdateAvailability")
        .Produces<IReadOnlyList<AvailabilitySlotDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }
}
