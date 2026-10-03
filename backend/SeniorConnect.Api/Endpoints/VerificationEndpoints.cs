using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SeniorConnect.Api.Common;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Profiles.Application;

namespace SeniorConnect.Api.Endpoints;

/// <summary>
/// Phase 4 (docs/security/verification-and-discovery.md). The caller reads
/// their own facts and switches their own discoverability; only organization
/// staff record address/organization facts, and only for their own active
/// volunteers. No endpoint accepts a level or an "isVerified" flag.
/// </summary>
public static class VerificationEndpoints
{
    public static IEndpointRouteBuilder MapVerificationEndpoints(this IEndpointRouteBuilder app)
    {
        var me = app.MapGroup("/api/v1/me")
            .WithTags("Verification & discovery")
            .RequireAuthorization();

        me.MapGet("/verification", async (ClaimsPrincipal user, IVerificationService service, CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();
            return (await service.GetMineAsync(userId.Value, ct)).ToHttpResult();
        })
        .WithName("GetMyVerification")
        .Produces<MyVerificationDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        me.MapGet("/discoverability", async (ClaimsPrincipal user, IDiscoverabilityService service, CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();
            return (await service.GetAsync(userId.Value, ct)).ToHttpResult();
        })
        .WithName("GetDiscoverability")
        .Produces<DiscoverabilityDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        me.MapPut("/discoverability", async (
            UpdateDiscoverabilityRequest request, ClaimsPrincipal user, IDiscoverabilityService service, CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();
            return (await service.SetAsync(userId.Value, request, ct)).ToHttpResult();
        })
        .WithName("UpdateDiscoverability")
        .Produces<DiscoverabilityDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        var org = app.MapGroup("/api/v1/organizations/{organizationId:guid}")
            .WithTags("Verification & discovery")
            .RequireAuthorization();

        org.MapPost("/volunteers/{volunteerUserId:guid}/verifications", async (
            Guid organizationId, Guid volunteerUserId, RecordVerificationRequest request,
            ClaimsPrincipal user, IVerificationService service, CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();
            return (await service.RecordAsync(userId.Value, organizationId, volunteerUserId, request, ct)).ToHttpResult();
        })
        .WithName("RecordVolunteerVerification")
        .Produces<StaffVerificationDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        org.MapPost("/verifications/{verificationId:guid}:revoke", async (
            Guid organizationId, Guid verificationId, RevokeVerificationRequest request,
            ClaimsPrincipal user, IVerificationService service, CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();
            return (await service.RevokeAsync(userId.Value, organizationId, verificationId, request, ct)).ToHttpResult();
        })
        .WithName("RevokeVolunteerVerification")
        .Produces<StaffVerificationDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
