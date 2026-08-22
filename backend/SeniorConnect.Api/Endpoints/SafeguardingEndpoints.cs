using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SeniorConnect.Api.Common;
using SeniorConnect.Domain;
using SeniorConnect.Modules.TrustSafety.Application;

namespace SeniorConnect.Api.Endpoints;

public static class SafeguardingEndpoints
{
    public static IEndpointRouteBuilder MapSafeguardingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/safeguarding")
            .WithTags("Safeguarding")
            .RequireAuthorization();

        // Anyone can report a concern (2-tap workflow from mobile, BR-SG-04)
        group.MapPost("/concerns", async (
            RaiseConcernRequest request,
            ClaimsPrincipal user,
            ISafeguardingService safeguardingService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await safeguardingService.RaiseConcernAsync(userId.Value, request, ct);
            return result.ToHttpResult("/api/v1/safeguarding/cases/" + result.Value?.Id);
        })
        .WithName("RaiseSafeguardingConcern")
        .Produces<SafeguardingCaseDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // Safeguarding Officer Only Endpoints (BR-SG-02)
        group.MapGet("/cases", async (
            Guid? organizationId,
            ClaimsPrincipal user,
            ISafeguardingService safeguardingService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            if (!IsSafeguardingOfficer(user))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Forbidden",
                    detail: "The capability 'SafeguardingOfficer' is required to access safeguarding records.",
                    extensions: new Dictionary<string, object?> { ["code"] = "SAFEGUARDING_OFFICER_REQUIRED" });
            }

            var result = await safeguardingService.GetCasesAsync(organizationId, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetSafeguardingCases")
        .Produces<IReadOnlyList<SafeguardingCaseDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/cases/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            ISafeguardingService safeguardingService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            if (!IsSafeguardingOfficer(user))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Forbidden",
                    detail: "The capability 'SafeguardingOfficer' is required to access safeguarding records.",
                    extensions: new Dictionary<string, object?> { ["code"] = "SAFEGUARDING_OFFICER_REQUIRED" });
            }

            var result = await safeguardingService.GetCaseByIdAsync(id, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetSafeguardingCaseById")
        .Produces<SafeguardingCaseDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/cases/{id:guid}/notes", async (
            Guid id,
            AddCaseNoteRequest request,
            ClaimsPrincipal user,
            ISafeguardingService safeguardingService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            if (!IsSafeguardingOfficer(user))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Forbidden",
                    detail: "The capability 'SafeguardingOfficer' is required to update safeguarding records.",
                    extensions: new Dictionary<string, object?> { ["code"] = "SAFEGUARDING_OFFICER_REQUIRED" });
            }

            var result = await safeguardingService.AddCaseNoteAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("AddSafeguardingCaseNote")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/cases/{id:guid}:close", async (
            Guid id,
            CloseCaseRequest request,
            ClaimsPrincipal user,
            ISafeguardingService safeguardingService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            if (!IsSafeguardingOfficer(user))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Forbidden",
                    detail: "The capability 'SafeguardingOfficer' is required to close safeguarding records.",
                    extensions: new Dictionary<string, object?> { ["code"] = "SAFEGUARDING_OFFICER_REQUIRED" });
            }

            var result = await safeguardingService.CloseCaseAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("CloseSafeguardingCase")
        .Produces<SafeguardingCaseDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static bool IsSafeguardingOfficer(ClaimsPrincipal user)
    {
        return user.IsInRole("SafeguardingOfficer")
            || user.IsInRole("PlatformAdmin")
            || user.HasClaim("capability", "SafeguardingOfficer")
            || user.HasClaim("capability", "ManageSafeguarding");
    }
}
