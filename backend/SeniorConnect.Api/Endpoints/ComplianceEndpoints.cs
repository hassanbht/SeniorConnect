using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SeniorConnect.Api.Common;
using SeniorConnect.Modules.Reporting.Application;

namespace SeniorConnect.Api.Endpoints;

/// <summary>
/// P7-15 / Legal Gate — platform-level compliance tracking endpoints.
///
/// ALL endpoints require authentication. Platform admin check is enforced
/// by each handler returning 403 for non-platform-scope callers (the
/// IsPlatformAdmin check reads from server-side claims, never from the request body).
///
/// These endpoints exist to make the Legal Gate auditable and machine-readable
/// rather than a paragraph in a markdown file. They do NOT replace the actual
/// legal work — they just track when it has been done.
/// </summary>
public static class ComplianceEndpoints
{
    private const string Tag = "Legal Gate Compliance";

    public static IEndpointRouteBuilder MapComplianceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/compliance")
            .WithTags(Tag)
            .RequireAuthorization();

        // ── Legal Gate status (read-only summary) ─────────────────────────

        group.MapGet("/status", async (
            ClaimsPrincipal user,
            IComplianceService svc,
            CancellationToken ct) =>
        {
            if (!user.IsPlatformAdmin()) return Results.Forbid();
            var result = await svc.GetLegalGateStatusAsync(ct);
            return result.ToHttpResult();
        })
        .WithName("GetLegalGateStatus")
        .Produces<LegalGateStatusDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // ── Legal entity profile ──────────────────────────────────────────

        group.MapGet("/legal-entity", async (
            ClaimsPrincipal user,
            IComplianceService svc,
            CancellationToken ct) =>
        {
            if (!user.IsPlatformAdmin()) return Results.Forbid();
            var result = await svc.GetLegalEntityProfileAsync(ct);
            return result.ToHttpResult();
        })
        .WithName("GetLegalEntityProfile")
        .Produces<LegalEntityProfileDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/legal-entity", async (
            UpsertLegalEntityProfileRequest request,
            ClaimsPrincipal user,
            IComplianceService svc,
            CancellationToken ct) =>
        {
            if (!user.IsPlatformAdmin()) return Results.Forbid();
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await svc.UpsertLegalEntityProfileAsync(request, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("UpsertLegalEntityProfile")
        .Produces<LegalEntityProfileDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/legal-entity:mark-established", async (
            MarkEstablishedRequest request,
            ClaimsPrincipal user,
            IComplianceService svc,
            CancellationToken ct) =>
        {
            if (!user.IsPlatformAdmin()) return Results.Forbid();
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await svc.MarkEstablishedAsync(request.EstablishedAtUtc, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("MarkLegalEntityEstablished")
        .Produces<LegalEntityProfileDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/legal-entity:record-privacy-policy-review", async (
            RecordReviewRequest request,
            ClaimsPrincipal user,
            IComplianceService svc,
            CancellationToken ct) =>
        {
            if (!user.IsPlatformAdmin()) return Results.Forbid();
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await svc.RecordPrivacyPolicyReviewAsync(request.Version, request.ReviewedAtUtc, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("RecordPrivacyPolicyReview")
        .Produces<LegalEntityProfileDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/legal-entity:record-terms-review", async (
            RecordReviewRequest request,
            ClaimsPrincipal user,
            IComplianceService svc,
            CancellationToken ct) =>
        {
            if (!user.IsPlatformAdmin()) return Results.Forbid();
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await svc.RecordTermsReviewAsync(request.Version, request.ReviewedAtUtc, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("RecordTermsReview")
        .Produces<LegalEntityProfileDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // ── DPA ───────────────────────────────────────────────────────────

        group.MapGet("/dpas", async (
            ClaimsPrincipal user,
            IComplianceService svc,
            CancellationToken ct) =>
        {
            if (!user.IsPlatformAdmin()) return Results.Forbid();
            var result = await svc.ListDpasAsync(ct);
            return result.ToHttpResult();
        })
        .WithName("ListDataProcessingAgreements")
        .Produces<IReadOnlyList<DataProcessingAgreementDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/dpas", async (
            CreateDpaRequest request,
            ClaimsPrincipal user,
            IComplianceService svc,
            CancellationToken ct) =>
        {
            if (!user.IsPlatformAdmin()) return Results.Forbid();
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await svc.CreateDpaAsync(request, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("CreateDataProcessingAgreement")
        .Produces<DataProcessingAgreementDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/dpas/{id:guid}:mark-signed", async (
            Guid id,
            MarkDpaSignedRequest request,
            ClaimsPrincipal user,
            IComplianceService svc,
            CancellationToken ct) =>
        {
            if (!user.IsPlatformAdmin()) return Results.Forbid();
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await svc.MarkDpaSignedAsync(id, request, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("MarkDataProcessingAgreementSigned")
        .Produces<DataProcessingAgreementDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // ── Verarbeitungsverzeichnis (Art. 30) ────────────────────────────

        group.MapGet("/processing-activities", async (
            ClaimsPrincipal user,
            IComplianceService svc,
            CancellationToken ct) =>
        {
            if (!user.IsPlatformAdmin()) return Results.Forbid();
            var result = await svc.ListProcessingActivitiesAsync(ct);
            return result.ToHttpResult();
        })
        .WithName("ListProcessingActivityRecords")
        .Produces<IReadOnlyList<ProcessingActivityRecordDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/processing-activities", async (
            CreateProcessingActivityRequest request,
            ClaimsPrincipal user,
            IComplianceService svc,
            CancellationToken ct) =>
        {
            if (!user.IsPlatformAdmin()) return Results.Forbid();
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await svc.CreateProcessingActivityAsync(request, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("CreateProcessingActivityRecord")
        .Produces<ProcessingActivityRecordDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // ── DPIA ──────────────────────────────────────────────────────────

        group.MapGet("/dpias", async (
            ClaimsPrincipal user,
            IComplianceService svc,
            CancellationToken ct) =>
        {
            if (!user.IsPlatformAdmin()) return Results.Forbid();
            var result = await svc.ListDpiasAsync(ct);
            return result.ToHttpResult();
        })
        .WithName("ListDpiaRecords")
        .Produces<IReadOnlyList<DpiaRecordDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/dpias", async (
            CreateDpiaRequest request,
            ClaimsPrincipal user,
            IComplianceService svc,
            CancellationToken ct) =>
        {
            if (!user.IsPlatformAdmin()) return Results.Forbid();
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await svc.CreateDpiaAsync(request, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("CreateDpiaRecord")
        .Produces<DpiaRecordDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/dpias/{id:guid}:approve", async (
            Guid id,
            ApproveDpiaRequest request,
            ClaimsPrincipal user,
            IComplianceService svc,
            CancellationToken ct) =>
        {
            if (!user.IsPlatformAdmin()) return Results.Forbid();
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await svc.ApproveDpiaAsync(id, request, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("ApproveDpiaRecord")
        .Produces<DpiaRecordDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // ── Insurance ─────────────────────────────────────────────────────

        group.MapGet("/insurance-policies", async (
            ClaimsPrincipal user,
            IComplianceService svc,
            CancellationToken ct) =>
        {
            if (!user.IsPlatformAdmin()) return Results.Forbid();
            var result = await svc.ListInsurancePoliciesAsync(ct);
            return result.ToHttpResult();
        })
        .WithName("ListInsurancePolicies")
        .Produces<IReadOnlyList<InsurancePolicyDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/insurance-policies", async (
            CreateInsurancePolicyRequest request,
            ClaimsPrincipal user,
            IComplianceService svc,
            CancellationToken ct) =>
        {
            if (!user.IsPlatformAdmin()) return Results.Forbid();
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await svc.CreateInsurancePolicyAsync(request, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("CreateInsurancePolicy")
        .Produces<InsurancePolicyDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/insurance-policies/{id:guid}:record-written-confirmation", async (
            Guid id,
            RecordInsuranceConfirmationRequest request,
            ClaimsPrincipal user,
            IComplianceService svc,
            CancellationToken ct) =>
        {
            if (!user.IsPlatformAdmin()) return Results.Forbid();
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await svc.RecordInsuranceWrittenConfirmationAsync(id, request, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("RecordInsurancePolicyWrittenConfirmation")
        .Produces<InsurancePolicyDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // ── EU Hosting attestation ─────────────────────────────────────────

        group.MapGet("/hosting-attestation", async (
            ClaimsPrincipal user,
            IComplianceService svc,
            CancellationToken ct) =>
        {
            if (!user.IsPlatformAdmin()) return Results.Forbid();
            var result = await svc.GetHostingAttestationAsync(ct);
            return result.ToHttpResult();
        })
        .WithName("GetHostingAttestation")
        .Produces<HostingAttestationDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/hosting-attestation", async (
            UpsertHostingAttestationRequest request,
            ClaimsPrincipal user,
            IComplianceService svc,
            CancellationToken ct) =>
        {
            if (!user.IsPlatformAdmin()) return Results.Forbid();
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await svc.UpsertHostingAttestationAsync(request, userId.Value, ct);
            return result.ToHttpResult();
        })
        .WithName("UpsertHostingAttestation")
        .Produces<HostingAttestationDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }
}

// ── Request types local to this endpoint file ─────────────────────────────────

public sealed record MarkEstablishedRequest(DateTimeOffset EstablishedAtUtc);
public sealed record RecordReviewRequest(string Version, DateTimeOffset ReviewedAtUtc);
