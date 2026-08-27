using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SeniorConnect.Api.Common;
using SeniorConnect.Modules.Matching.Application;
using SeniorConnect.Modules.Matching.Domain;

namespace SeniorConnect.Api.Endpoints;

public static class MatchingEndpoints
{
    public static IEndpointRouteBuilder MapMatchingEndpoints(this IEndpointRouteBuilder app)
    {
        var matchingGroup = app.MapGroup("/api/v1/matching")
            .WithTags("Matching")
            .RequireAuthorization();

        matchingGroup.MapGet("/requests/{id:guid}/candidates", async (
            Guid id,
            IMatchingService matchingService,
            CancellationToken ct) =>
        {
            var result = await matchingService.FindCandidatesAsync(id, cancellationToken: ct);
            return result.ToHttpResult();
        })
        .WithName("GetMatchingCandidates")
        .Produces<IReadOnlyList<MatchingCandidate>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        matchingGroup.MapGet("/feed", async (
            double? latitude,
            double? longitude,
            double? radiusKm,
            ClaimsPrincipal user,
            IMatchingService matchingService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await matchingService.GetVolunteerFeedAsync(
                userId.Value,
                latitude,
                longitude,
                radiusKm ?? 20.0,
                ct);

            return result.ToHttpResult();
        })
        .WithName("GetVolunteerFeed")
        .Produces<IReadOnlyList<VolunteerFeedItem>>(StatusCodes.Status200OK);

        matchingGroup.MapPost("/proposals:hybrid", async (
            HybridMatchingRequest request,
            IMatchingService matchingService,
            CancellationToken ct) =>
        {
            var result = await matchingService.GetHybridProposalsAsync(request, ct);
            return result.ToHttpResult();
        })
        .WithName("GetHybridMatchingProposals")
        .Produces<IReadOnlyList<HybridMatchingProposal>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
