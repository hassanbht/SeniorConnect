using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SeniorConnect.Api.Common;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Domain;

namespace SeniorConnect.Api.Endpoints;

public static class PrivacyEndpoints
{
    public static IEndpointRouteBuilder MapPrivacyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/privacy")
            .WithTags("Privacy & GDPR Compliance")
            .RequireAuthorization();

        group.MapGet("/consents", async (
            ClaimsPrincipal user,
            IPrivacyService privacyService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await privacyService.GetUserConsentsAsync(userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetUserConsents")
        .Produces<IReadOnlyList<VersionedConsentDto>>(StatusCodes.Status200OK);

        group.MapPost("/consents", async (
            RecordConsentRequest request,
            ClaimsPrincipal user,
            HttpContext httpContext,
            IPrivacyService privacyService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var ipHash = httpContext.Connection.RemoteIpAddress?.ToString();
            var result = await privacyService.RecordConsentAsync(userId.Value, request, ipHash, ct);
            return result.ToHttpResult();
        })
        .WithName("RecordConsent")
        .Produces<VersionedConsentDto>(StatusCodes.Status200OK);

        group.MapPost("/consents/{consentType}/withdraw", async (
            ConsentType consentType,
            ClaimsPrincipal user,
            IPrivacyService privacyService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await privacyService.WithdrawConsentAsync(userId.Value, consentType, ct);
            return result.ToHttpResult();
        })
        .WithName("WithdrawConsent")
        .Produces<VersionedConsentDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/export-data", async (
            ClaimsPrincipal user,
            IPrivacyService privacyService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await privacyService.ExportUserDataAsync(userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("ExportUserData")
        .Produces<UserDataExportDto>(StatusCodes.Status200OK);

        group.MapPost("/account-deletion:request", async (
            RequestDeletionRequest request,
            ClaimsPrincipal user,
            IPrivacyService privacyService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await privacyService.RequestAccountDeletionAsync(userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("RequestAccountDeletion")
        .Produces<DeletionRequestDto>(StatusCodes.Status200OK);

        group.MapPost("/account-deletion:confirm", async (
            ConfirmDeletionRequest request,
            ClaimsPrincipal user,
            IPrivacyService privacyService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await privacyService.ConfirmAccountDeletionAsync(userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("ConfirmAccountDeletion")
        .Produces<DeletionRequestDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
