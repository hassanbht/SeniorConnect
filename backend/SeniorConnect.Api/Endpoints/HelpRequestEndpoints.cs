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
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var seniorUserId = request.SeniorUserId ?? userId.Value;
            var result = await helpService.CreateHelpRequestAsync(userId.Value, seniorUserId, request, ct);
            return result.ToHttpResult("/api/v1/help-requests/" + result.Value?.Id);
        })
        .WithName("CreateHelpRequest")
        .Produces<HelpRequestDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden);

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
            ClaimsPrincipal user,
            IHelpRequestService helpService,
            CancellationToken ct) =>
        {
            var result = await helpService.GetOpenHelpRequestsAsync(organizationId, user.GetUserId(), ct);
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
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

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
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await helpService.GetHelpRequestByIdAsync(id, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetHelpRequestById")
        .Produces<HelpRequestDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // The only route to a request's exact coordinates; the central
        // location policy decides (docs/security/location-consent-and-visibility.md).
        helpGroup.MapGet("/{id:guid}/location", async (
            Guid id,
            ClaimsPrincipal user,
            IHelpRequestService helpService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            return (await helpService.GetHelpRequestLocationAsync(id, userId.Value, ct)).ToHttpResult();
        })
        .WithName("GetHelpRequestLocation")
        .Produces<HelpRequestLocationDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        helpGroup.MapPost("/{id:guid}:accept", async (
            Guid id,
            AcceptHelpRequestRequest request,
            ClaimsPrincipal user,
            IHelpRequestService helpService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

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
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

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
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

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
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

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
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await helpService.MarkNoShowAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("MarkHelpRequestNoShow")
        .Produces<HelpRequestDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // P3-19 / BR-HELP-06: a dispute by the accused volunteer suspends the
        // reliability penalty until a coordinator resolves it.
        helpGroup.MapPost("/{id:guid}:dispute-no-show", async (
            Guid id,
            DisputeNoShowRequest request,
            ClaimsPrincipal user,
            IHelpRequestService helpService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await helpService.DisputeNoShowAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("DisputeHelpRequestNoShow")
        .Produces<HelpRequestDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // BR-HELP-06: coordinator-only resolution of a disputed no-show.
        helpGroup.MapPost("/{id:guid}/no-show:resolve", async (
            Guid id,
            ResolveNoShowDisputeRequest request,
            ClaimsPrincipal user,
            IHelpRequestService helpService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await helpService.ResolveNoShowDisputeAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("ResolveHelpRequestNoShowDispute")
        .Produces<HelpRequestDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // --- ADR-026 Option C (approved 2026-09-30; legal validation pending) ---
        // The grant is honoured only when the operator enables VisitAddress:RequireGrant.
        helpGroup.MapPost("/{id:guid}/visit-address-grant", async (
            Guid id,
            GrantVisitAddressRequest request,
            ClaimsPrincipal user,
            IHelpRequestService helpService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await helpService.GrantVisitAddressAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("GrantVisitAddress")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        helpGroup.MapDelete("/{id:guid}/visit-address-grant", async (
            Guid id,
            // Explicit [FromBody]: a complex-type parameter on a DELETE is not
            // body-inferred, and an implicit inference attempt crashes route
            // building at startup (.NET 10 RequestDelegateFactory rule).
            // The optional reason stays in the body — the Flutter client
            // already sends it (my_request_status_notifier.dart).
            [Microsoft.AspNetCore.Mvc.FromBody] RevokeVisitAddressRequest request,
            ClaimsPrincipal user,
            IHelpRequestService helpService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await helpService.RevokeVisitAddressAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("RevokeVisitAddress")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        helpGroup.MapGet("/{id:guid}/history", async (
            Guid id,
            ClaimsPrincipal user,
            IHelpRequestService helpService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await helpService.GetStatusHistoryAsync(id, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetHelpRequestHistory")
        .Produces<IReadOnlyList<HelpRequestStatusHistoryDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
