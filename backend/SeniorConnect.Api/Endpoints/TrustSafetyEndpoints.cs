using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SeniorConnect.Api.Common;
using SeniorConnect.Modules.TrustSafety.Application;

namespace SeniorConnect.Api.Endpoints;

public static class TrustSafetyEndpoints
{
    public static IEndpointRouteBuilder MapTrustSafetyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/trust-safety")
            .WithTags("Trust & Safety")
            .RequireAuthorization();

        // --- ID Austria / eID Verification (P8-06, BR-TRUST-05) ---
        group.MapPost("/verify:id-austria", async (
            IdentityVerificationRequest request,
            ClaimsPrincipal user,
            IIdentityVerificationProvider provider,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var req = request with { UserId = userId.Value, ProviderType = VerificationProviderType.IdAustria };
            var result = await provider.VerifyIdentityAsync(req, ct);
            return result.ToHttpResult();
        })
        .WithName("VerifyIdAustria")
        .Produces<IdentityVerificationResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // --- Buddy System ---
        // BR-SAFETY-05: readable by the volunteer themselves, a coordinator of
        // the volunteer's organization, or a safeguarding officer. Anyone else
        // gets 404 — a 403 would confirm that the volunteer exists.
        group.MapGet("/buddy/{volunteerUserId:guid}", async (
            Guid volunteerUserId,
            ClaimsPrincipal user,
            ITrustSafetyService trustService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await trustService.GetBuddyStatusAsync(
                volunteerUserId, userId.Value, SafeguardingEndpoints.IsSafeguardingOfficer(user), ct);
            return result.ToHttpResult();
        })
        .WithName("GetBuddyStatus")
        .Produces<BuddyStatusDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/buddy/{volunteerUserId:guid}:waive", async (
            Guid volunteerUserId,
            WaiveBuddyRequest request,
            ClaimsPrincipal user,
            ITrustSafetyService trustService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await trustService.WaiveBuddyAsync(
                volunteerUserId, userId.Value, SafeguardingEndpoints.IsSafeguardingOfficer(user), request, ct);
            return result.ToHttpResult();
        })
        .WithName("WaiveBuddy")
        .Produces<BuddyStatusDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // --- First Meeting Protocol ---
        group.MapGet("/protocol", async (
            ITrustSafetyService trustService,
            CancellationToken ct) =>
        {
            var result = await trustService.GetFirstMeetingProtocolAsync(ct);
            return result.ToHttpResult();
        })
        .WithName("GetFirstMeetingProtocol")
        .Produces<FirstMeetingProtocolDto>(StatusCodes.Status200OK);

        // --- Key Custody ---
        group.MapPost("/keys", async (
            HandoverKeyRequest request,
            ClaimsPrincipal user,
            ITrustSafetyService trustService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await trustService.HandoverKeyAsync(userId.Value, request, ct);
            return result.ToHttpResult("/api/v1/trust-safety/keys/" + result.Value?.Id);
        })
        .WithName("HandoverKey")
        .Produces<KeyCustodyDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/keys/{id:guid}:return", async (
            Guid id,
            ReturnKeyRequest request,
            ClaimsPrincipal user,
            ITrustSafetyService trustService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await trustService.ReturnKeyAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("ReturnKey")
        .Produces<KeyCustodyDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // --- Expenses ---
        group.MapPost("/expenses", async (
            CreateExpenseRequest request,
            ClaimsPrincipal user,
            ITrustSafetyService trustService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await trustService.RecordExpenseAsync(userId.Value, request, ct);
            return result.ToHttpResult("/api/v1/trust-safety/expenses/" + result.Value?.Id);
        })
        .WithName("RecordExpense")
        .Produces<ExpenseRecordDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/expenses/{id:guid}:confirm", async (
            Guid id,
            ClaimsPrincipal user,
            ITrustSafetyService trustService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await trustService.ConfirmExpenseAsync(id, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("ConfirmExpense")
        .Produces<ExpenseRecordDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/expenses/{id:guid}:dispute", async (
            Guid id,
            DisputeExpenseRequest request,
            ClaimsPrincipal user,
            ITrustSafetyService trustService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await trustService.DisputeExpenseAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("DisputeExpense")
        .Produces<ExpenseRecordDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // --- User Blocking ---
        group.MapPost("/blocks", async (
            CreateBlockRequest request,
            ClaimsPrincipal user,
            ITrustSafetyService trustService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();

            var result = await trustService.BlockUserAsync(userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("BlockUser")
        .Produces<UserBlockDto>(StatusCodes.Status200OK);

        // Phase 4 (docs/security/block-report-policy.md): only my own blocks.
        group.MapGet("/blocks", async (ClaimsPrincipal user, ITrustSafetyService trustService, CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();
            return (await trustService.GetMyBlocksAsync(userId.Value, ct)).ToHttpResult();
        })
        .WithName("GetMyBlocks")
        .Produces<IReadOnlyList<MyBlockDto>>(StatusCodes.Status200OK);

        group.MapDelete("/blocks/{blockedUserId:guid}", async (
            Guid blockedUserId, ClaimsPrincipal user, ITrustSafetyService trustService, CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return ResultExtensions.UnauthorizedProblem();
            var result = await trustService.UnblockUserAsync(userId.Value, blockedUserId, ct);
            return result.IsSuccess ? Results.NoContent() : result.ToHttpResult();
        })
        .WithName("UnblockUser")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
