using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SeniorConnect.Api.Common;
using SeniorConnect.Modules.HelpRequests.Application;

namespace SeniorConnect.Api.Endpoints;

public static class HelpRequestEndpoints
{
    public static IEndpointRouteBuilder MapHelpRequestEndpoints(this IEndpointRouteBuilder app)
    {
        var helpGroup = app.MapGroup("/api/v1/help-requests")
            .WithTags("Help Requests")
            .RequireAuthorization();

        helpGroup.MapPost("/", async (
            CreateHelpRequestRequest request,
            ClaimsPrincipal user,
            IHelpRequestService helpService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await helpService.CreateHelpRequestAsync(userId.Value, userId.Value, request, ct);
            return result.ToHttpResult("/api/v1/help-requests/" + result.Value?.Id);
        })
        .WithName("CreateHelpRequest")
        .Produces<HelpRequestDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        helpGroup.MapPost("/parse-voice", async (
            VoiceParseRequest request,
            IVoiceRequestParser voiceParser,
            CancellationToken ct) =>
        {
            var result = await voiceParser.ParseTranscriptAsync(request, ct);
            return result.ToHttpResult();
        })
        .WithName("ParseVoiceHelpRequest")
        .Produces<VoiceParseResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        helpGroup.MapGet("/", async (
            Guid? organizationId,
            IHelpRequestService helpService,
            CancellationToken ct) =>
        {
            var result = await helpService.GetOpenHelpRequestsAsync(organizationId, ct);
            return result.ToHttpResult();
        })
        .WithName("GetOpenHelpRequests")
        .Produces<IReadOnlyList<HelpRequestDto>>(StatusCodes.Status200OK);

        helpGroup.MapGet("/me", async (
            ClaimsPrincipal user,
            IHelpRequestService helpService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await helpService.GetSeniorHelpRequestsAsync(userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetMyHelpRequests")
        .Produces<IReadOnlyList<HelpRequestDto>>(StatusCodes.Status200OK);

        helpGroup.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            IHelpRequestService helpService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await helpService.GetHelpRequestByIdAsync(id, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetHelpRequestById")
        .Produces<HelpRequestDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        helpGroup.MapPost("/{id:guid}:accept", async (
            Guid id,
            AcceptHelpRequestRequest request,
            ClaimsPrincipal user,
            IHelpRequestService helpService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await helpService.AcceptHelpRequestAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("AcceptHelpRequest")
        .Produces<HelpRequestDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status404NotFound);

        helpGroup.MapPost("/{id:guid}:check-in", async (
            Guid id,
            ClaimsPrincipal user,
            IHelpRequestService helpService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await helpService.CheckInHelpRequestAsync(id, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("CheckInHelpRequest")
        .Produces<HelpRequestDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        helpGroup.MapPost("/{id:guid}:complete", async (
            Guid id,
            CompleteHelpRequestRequest request,
            ClaimsPrincipal user,
            IHelpRequestService helpService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await helpService.CompleteHelpRequestAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("CompleteHelpRequest")
        .Produces<HelpRequestDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        helpGroup.MapPost("/{id:guid}:cancel", async (
            Guid id,
            CancelHelpRequestRequest request,
            ClaimsPrincipal user,
            IHelpRequestService helpService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await helpService.CancelHelpRequestAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("CancelHelpRequest")
        .Produces<HelpRequestDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        helpGroup.MapPost("/{id:guid}:no-show", async (
            Guid id,
            NoShowHelpRequestRequest request,
            ClaimsPrincipal user,
            IHelpRequestService helpService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await helpService.MarkNoShowAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("MarkHelpRequestNoShow")
        .Produces<HelpRequestDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // P3-19: a successful dispute reverts the volunteer's reliability
        // score to what it was before the no-show, not just a re-nudge.
        helpGroup.MapPost("/{id:guid}:dispute-no-show", async (
            Guid id,
            DisputeNoShowRequest request,
            ClaimsPrincipal user,
            IHelpRequestService helpService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await helpService.DisputeNoShowAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("DisputeHelpRequestNoShow")
        .Produces<HelpRequestDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        helpGroup.MapGet("/{id:guid}/history", async (
            Guid id,
            IHelpRequestService helpService,
            CancellationToken ct) =>
        {
            var result = await helpService.GetStatusHistoryAsync(id, ct);
            return result.ToHttpResult();
        })
        .WithName("GetHelpRequestHistory")
        .Produces<IReadOnlyList<HelpRequestStatusHistoryDto>>(StatusCodes.Status200OK);

        return app;
    }
}
