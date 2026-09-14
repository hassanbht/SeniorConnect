using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SeniorConnect.Api.Common;
using SeniorConnect.Modules.Organizations.Contracts;
using SeniorConnect.Modules.Profiles.Application;

namespace SeniorConnect.Api.Endpoints;

public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var meGroup = app.MapGroup("/api/v1/me")
            .WithTags("User Profiles")
            .RequireAuthorization();

        meGroup.MapGet("/support-profile", async (
            ClaimsPrincipal user,
            IProfileService profileService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await profileService.GetSupportProfileAsync(userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetSupportProfile")
        .Produces<SupportProfileDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        meGroup.MapPut("/support-profile", async (
            UpdateSupportProfileRequest request,
            ClaimsPrincipal user,
            IProfileService profileService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await profileService.UpsertSupportProfileAsync(userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("UpdateSupportProfile")
        .Produces<SupportProfileDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        // Backwards-compatibility alias route
        meGroup.MapGet("/senior-profile", async (
            ClaimsPrincipal user,
            IProfileService profileService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await profileService.GetSupportProfileAsync(userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetSeniorProfileAlias")
        .Produces<SupportProfileDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

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

        meGroup.MapGet("/interests", async (
            ClaimsPrincipal user,
            IProfileService profileService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await profileService.GetUserInterestsAsync(userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetMyInterests")
        .Produces<IReadOnlyList<InterestDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        meGroup.MapPut("/interests", async (
            UpdateUserInterestsRequest request,
            ClaimsPrincipal user,
            IProfileService profileService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await profileService.UpdateUserInterestsAsync(userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("UpdateMyInterests")
        .Produces<IReadOnlyList<InterestDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        meGroup.MapGet("/organizations", async (
            ClaimsPrincipal user,
            IOrganizationCoordinatorReader orgReader,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var memberships = await orgReader.GetActiveMembershipsForUserAsync(userId.Value, ct);
            return Results.Ok(memberships);
        })
        .WithName("GetMyOrganizations")
        .Produces<IReadOnlyList<StaffOrganizationDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }
}
