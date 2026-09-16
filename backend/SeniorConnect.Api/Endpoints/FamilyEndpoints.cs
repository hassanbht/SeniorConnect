using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SeniorConnect.Api.Common;
using SeniorConnect.Modules.Family.Application;

namespace SeniorConnect.Api.Endpoints;

public static class FamilyEndpoints
{
    public static IEndpointRouteBuilder MapFamilyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/family")
            .WithTags("Family & Delegation")
            .RequireAuthorization();

        // --- Provisioning & Zugangskarte ---

        group.MapPost("/seniors", async (
            CreateSeniorWithZugangskarteRequest request,
            ClaimsPrincipal user,
            IFamilyService familyService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await familyService.CreateSeniorWithZugangskarteAsync(userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("CreateSeniorWithZugangskarte")
        .Produces<ZugangskarteDto>(StatusCodes.Status201Created);

        group.MapPost("/zugangskarte:claim", async (
            ClaimZugangskarteRequest request,
            ClaimsPrincipal user,
            IFamilyService familyService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            var result = await familyService.ClaimZugangskarteAsync(userId, request, ct);
            return result.ToHttpResult();
        })
        .WithName("ClaimZugangskarte")
        .AllowAnonymous()
        .Produces<ClaimZugangskarteResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // --- Invitations & Relationships ---

        group.MapPost("/invitations", async (
            InviteCaregiverRequest request,
            ClaimsPrincipal user,
            IFamilyService familyService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await familyService.InviteCaregiverAsync(userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("InviteCaregiver")
        .Produces<FamilyRelationshipDto>(StatusCodes.Status201Created);

        group.MapPost("/invitations:accept", async (
            AcceptInvitationRequest request,
            ClaimsPrincipal user,
            IFamilyService familyService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await familyService.AcceptInvitationAsync(userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("AcceptCaregiverInvitation")
        .Produces<FamilyRelationshipDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/relationships/{id:guid}/permissions", async (
            Guid id,
            UpdatePermissionsRequest request,
            ClaimsPrincipal user,
            IFamilyService familyService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await familyService.UpdatePermissionsAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("UpdateFamilyPermissions")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/relationships/{id:guid}:revoke", async (
            Guid id,
            ClaimsPrincipal user,
            IFamilyService familyService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await familyService.RevokeRelationshipAsync(id, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("RevokeFamilyRelationship")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/seniors/{seniorId:guid}/caregivers", async (
            Guid seniorId,
            ClaimsPrincipal user,
            IFamilyService familyService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await familyService.GetCaregiversForSeniorAsync(seniorId, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetCaregiversForSenior")
        .Produces<IReadOnlyList<FamilyRelationshipDto>>(StatusCodes.Status200OK);

        group.MapGet("/my-seniors", async (
            ClaimsPrincipal user,
            IFamilyService familyService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await familyService.GetSeniorsForCaregiverAsync(userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetSeniorsForCaregiver")
        .Produces<IReadOnlyList<FamilyRelationshipDto>>(StatusCodes.Status200OK);

        // --- Transparency Log: "Wer hat was gesehen?" ---

        group.MapGet("/me/access-logs", async (
            int? days,
            ClaimsPrincipal user,
            IFamilyService familyService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await familyService.GetAccessLogsAsync(userId.Value, userId.Value, days ?? 30, ct);
            return result.ToHttpResult();
        })
        .WithName("GetMyAccessLogs")
        .Produces<IReadOnlyList<SeniorAccessLogDto>>(StatusCodes.Status200OK);

        group.MapGet("/seniors/{seniorId:guid}/access-logs", async (
            Guid seniorId,
            int? days,
            ClaimsPrincipal user,
            IFamilyService familyService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await familyService.GetAccessLogsAsync(seniorId, userId.Value, days ?? 30, ct);
            return result.ToHttpResult();
        })
        .WithName("GetSeniorAccessLogs")
        .Produces<IReadOnlyList<SeniorAccessLogDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // --- Trusted Contacts ---

        group.MapGet("/seniors/{seniorId:guid}/trusted-contacts", async (
            Guid seniorId,
            ClaimsPrincipal user,
            IFamilyService familyService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await familyService.GetTrustedContactsAsync(seniorId, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetTrustedContacts")
        .Produces<IReadOnlyList<TrustedContactDto>>(StatusCodes.Status200OK);

        group.MapPost("/trusted-contacts", async (
            CreateTrustedContactRequest request,
            ClaimsPrincipal user,
            IFamilyService familyService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await familyService.CreateTrustedContactAsync(userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("CreateTrustedContact")
        .Produces<TrustedContactDto>(StatusCodes.Status201Created);

        group.MapPut("/trusted-contacts/{id:guid}", async (
            Guid id,
            UpdateTrustedContactRequest request,
            ClaimsPrincipal user,
            IFamilyService familyService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await familyService.UpdateTrustedContactAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("UpdateTrustedContact")
        .Produces<TrustedContactDto>(StatusCodes.Status200OK);

        group.MapDelete("/trusted-contacts/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            IFamilyService familyService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await familyService.DeleteTrustedContactAsync(id, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("DeleteTrustedContact")
        .Produces(StatusCodes.Status200OK);

        // --- Safety Alerts ---

        group.MapPost("/safety-alerts", async (
            TriggerSafetyAlertRequest request,
            ClaimsPrincipal user,
            IFamilyService familyService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await familyService.TriggerSafetyAlertAsync(userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("TriggerSafetyAlert")
        .Produces<SafetyAlertDto>(StatusCodes.Status201Created);

        group.MapPost("/safety-alerts/{id:guid}:acknowledge", async (
            Guid id,
            ClaimsPrincipal user,
            IFamilyService familyService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await familyService.AcknowledgeSafetyAlertAsync(id, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("AcknowledgeSafetyAlert")
        .Produces<SafetyAlertDto>(StatusCodes.Status200OK);

        group.MapPost("/safety-alerts/{id:guid}:resolve", async (
            Guid id,
            ResolveSafetyAlertRequest request,
            ClaimsPrincipal user,
            IFamilyService familyService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await familyService.ResolveSafetyAlertAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("ResolveSafetyAlert")
        .Produces<SafetyAlertDto>(StatusCodes.Status200OK);

        group.MapGet("/seniors/{seniorId:guid}/safety-alerts", async (
            Guid seniorId,
            ClaimsPrincipal user,
            IFamilyService familyService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await familyService.GetSafetyAlertsForSeniorAsync(seniorId, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("GetSafetyAlertsForSenior")
        .Produces<IReadOnlyList<SafetyAlertDto>>(StatusCodes.Status200OK);

        return app;
    }
}
