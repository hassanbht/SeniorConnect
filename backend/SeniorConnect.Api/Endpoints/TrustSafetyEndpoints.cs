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

        // --- Buddy System ---
        group.MapGet("/buddy/{volunteerUserId:guid}", async (
            Guid volunteerUserId,
            ITrustSafetyService trustService,
            CancellationToken ct) =>
        {
            var result = await trustService.GetBuddyStatusAsync(volunteerUserId, ct);
            return result.ToHttpResult();
        })
        .WithName("GetBuddyStatus")
        .Produces<BuddyStatusDto>(StatusCodes.Status200OK);

        group.MapPost("/buddy/{volunteerUserId:guid}:waive", async (
            Guid volunteerUserId,
            WaiveBuddyRequest request,
            ClaimsPrincipal user,
            ITrustSafetyService trustService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await trustService.WaiveBuddyAsync(volunteerUserId, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("WaiveBuddy")
        .Produces<BuddyStatusDto>(StatusCodes.Status200OK);

        // --- Key Custody ---
        group.MapPost("/keys", async (
            HandoverKeyRequest request,
            ITrustSafetyService trustService,
            CancellationToken ct) =>
        {
            var result = await trustService.HandoverKeyAsync(request, ct);
            return result.ToHttpResult("/api/v1/trust-safety/keys/" + result.Value?.Id);
        })
        .WithName("HandoverKey")
        .Produces<KeyCustodyDto>(StatusCodes.Status201Created);

        group.MapPost("/keys/{id:guid}:return", async (
            Guid id,
            ReturnKeyRequest request,
            ITrustSafetyService trustService,
            CancellationToken ct) =>
        {
            var result = await trustService.ReturnKeyAsync(id, request, ct);
            return result.ToHttpResult();
        })
        .WithName("ReturnKey")
        .Produces<KeyCustodyDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // --- Expenses ---
        group.MapPost("/expenses", async (
            CreateExpenseRequest request,
            ITrustSafetyService trustService,
            CancellationToken ct) =>
        {
            var result = await trustService.RecordExpenseAsync(request, ct);
            return result.ToHttpResult("/api/v1/trust-safety/expenses/" + result.Value?.Id);
        })
        .WithName("RecordExpense")
        .Produces<ExpenseRecordDto>(StatusCodes.Status201Created);

        group.MapPost("/expenses/{id:guid}:confirm", async (
            Guid id,
            ITrustSafetyService trustService,
            CancellationToken ct) =>
        {
            var result = await trustService.ConfirmExpenseAsync(id, ct);
            return result.ToHttpResult();
        })
        .WithName("ConfirmExpense")
        .Produces<ExpenseRecordDto>(StatusCodes.Status200OK);

        group.MapPost("/expenses/{id:guid}:dispute", async (
            Guid id,
            DisputeExpenseRequest request,
            ITrustSafetyService trustService,
            CancellationToken ct) =>
        {
            var result = await trustService.DisputeExpenseAsync(id, request, ct);
            return result.ToHttpResult();
        })
        .WithName("DisputeExpense")
        .Produces<ExpenseRecordDto>(StatusCodes.Status200OK);

        // --- User Blocking ---
        group.MapPost("/blocks", async (
            CreateBlockRequest request,
            ClaimsPrincipal user,
            ITrustSafetyService trustService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await trustService.BlockUserAsync(userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("BlockUser")
        .Produces<UserBlockDto>(StatusCodes.Status200OK);

        return app;
    }
}
