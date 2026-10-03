using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SeniorConnect.Api.Common;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Organizations.Contracts;
using SeniorConnect.Modules.Reporting.Application;

namespace SeniorConnect.Api.Endpoints;

public static class ReportingEndpoints
{
    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder app)
    {
        // Org-staff only (Coordinator/Admin): non-member → 404, member without
        // role → 403 CAPABILITY_MISSING. A funder principal has no org
        // membership and therefore never reaches these (BR-FUNDER-02).
        var reportGroup = app.MapGroup("/api/v1/reporting")
            .WithTags("Reporting")
            .RequireAuthorization();

        reportGroup.MapGet("/organizations/{orgId:guid}/impact-summary", async (
            Guid orgId,
            DateOnly? from,
            DateOnly? to,
            ClaimsPrincipal user,
            IOrganizationAccessGuard guard,
            IReportingService reportService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return Results.Unauthorized();
            var auth = await guard.EnsureStaffAsync(userId, orgId, ct);
            if (auth.IsFailure) return auth.ToHttpResult();

            var fromDate = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
            var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);

            var result = await reportService.GetImpactSummaryAsync(orgId, fromDate, toDate, ct);
            return result.ToHttpResult();
        })
        .WithName("GetImpactSummary")
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .Produces<ImpactReportSummaryDto>(StatusCodes.Status200OK);

        reportGroup.MapGet("/organizations/{orgId:guid}/export.csv", async (
            Guid orgId,
            DateOnly? from,
            DateOnly? to,
            ClaimsPrincipal user,
            IOrganizationAccessGuard guard,
            IReportingService reportService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return Results.Unauthorized();
            var auth = await guard.EnsureStaffAsync(userId, orgId, ct);
            if (auth.IsFailure) return auth.ToHttpResult();

            var fromDate = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
            var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);

            var result = await reportService.ExportImpactCsvAsync(orgId, fromDate, toDate, ct);
            if (result.IsFailure) return Result.Failure(result.Error!).ToHttpResult();

            return Results.File(result.Value!, "text/csv", $"impact-report-{orgId}.csv");
        })
        .WithName("ExportImpactCsv")
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status200OK, contentType: "text/csv");

        reportGroup.MapGet("/organizations/{orgId:guid}/export.pdf", async (
            Guid orgId,
            DateOnly? from,
            DateOnly? to,
            ClaimsPrincipal user,
            IOrganizationAccessGuard guard,
            IReportingService reportService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return Results.Unauthorized();
            var auth = await guard.EnsureStaffAsync(userId, orgId, ct);
            if (auth.IsFailure) return auth.ToHttpResult();

            var fromDate = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
            var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);

            var result = await reportService.ExportImpactPdfAsync(orgId, fromDate, toDate, ct);
            if (result.IsFailure) return Result.Failure(result.Error!).ToHttpResult();

            return Results.File(result.Value!, "application/pdf", $"impact-report-{orgId}.pdf");
        })
        .WithName("ExportImpactPdf")
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status200OK, contentType: "application/pdf");

        reportGroup.MapGet("/organizations/{orgId:guid}/export.xlsx", async (
            Guid orgId,
            DateOnly? from,
            DateOnly? to,
            ClaimsPrincipal user,
            IOrganizationAccessGuard guard,
            IReportingService reportService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return Results.Unauthorized();
            var auth = await guard.EnsureStaffAsync(userId, orgId, ct);
            if (auth.IsFailure) return auth.ToHttpResult();

            var fromDate = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
            var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);

            var result = await reportService.ExportImpactXlsxAsync(orgId, fromDate, toDate, ct);
            if (result.IsFailure) return Result.Failure(result.Error!).ToHttpResult();

            return Results.File(result.Value!, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"impact-report-{orgId}.xlsx");
        })
        .WithName("ExportImpactXlsx")
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status200OK, contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        reportGroup.MapGet("/companies/{companyOrgId:guid}/esg-summary", async (
            Guid companyOrgId,
            DateOnly? from,
            DateOnly? to,
            ClaimsPrincipal user,
            IOrganizationAccessGuard guard,
            IEsgReportingService esgService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return Results.Unauthorized();
            var auth = await guard.EnsureStaffAsync(userId, companyOrgId, ct);
            if (auth.IsFailure) return auth.ToHttpResult();

            var fromDate = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-365));
            var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);

            var result = await esgService.GetCorporateEsgSummaryAsync(companyOrgId, fromDate, toDate, ct);
            return result.ToHttpResult();
        })
        .WithName("GetCorporateEsgSummary")
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .Produces<CorporateEsgSummaryDto>(StatusCodes.Status200OK);

        reportGroup.MapGet("/companies/{companyOrgId:guid}/esg-certificate.pdf", async (
            Guid companyOrgId,
            DateOnly? from,
            DateOnly? to,
            ClaimsPrincipal user,
            IOrganizationAccessGuard guard,
            IEsgReportingService esgService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return Results.Unauthorized();
            var auth = await guard.EnsureStaffAsync(userId, companyOrgId, ct);
            if (auth.IsFailure) return auth.ToHttpResult();

            var fromDate = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-365));
            var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);

            var result = await esgService.ExportEsgCertificatePdfAsync(companyOrgId, fromDate, toDate, ct);
            if (result.IsFailure) return Result.Failure(result.Error!).ToHttpResult();

            return Results.File(result.Value!, "application/pdf", $"esg-certificate-{companyOrgId}.pdf");
        })
        .WithName("ExportCorporateEsgCertificatePdf")
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status200OK, contentType: "application/pdf");

        reportGroup.MapGet("/organizations/{orgId:guid}/annual-report", async (
            Guid orgId,
            int? year,
            ClaimsPrincipal user,
            IOrganizationAccessGuard guard,
            IReportingService reportService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return Results.Unauthorized();
            var auth = await guard.EnsureStaffAsync(userId, orgId, ct);
            if (auth.IsFailure) return auth.ToHttpResult();

            var targetYear = year ?? DateTime.UtcNow.Year;
            var result = await reportService.GetAnnualReportAsync(orgId, targetYear, ct);
            return result.ToHttpResult();
        })
        .WithName("GetAnnualReport")
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .Produces<AnnualStatisticsReportDto>(StatusCodes.Status200OK);

        reportGroup.MapGet("/organizations/{orgId:guid}/annual-report/export.pdf", async (
            Guid orgId,
            int? year,
            ClaimsPrincipal user,
            IOrganizationAccessGuard guard,
            IReportingService reportService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return Results.Unauthorized();
            var auth = await guard.EnsureStaffAsync(userId, orgId, ct);
            if (auth.IsFailure) return auth.ToHttpResult();

            var targetYear = year ?? DateTime.UtcNow.Year;
            var result = await reportService.ExportAnnualReportPdfAsync(orgId, targetYear, ct);
            if (result.IsFailure) return Result.Failure(result.Error!).ToHttpResult();

            return Results.File(result.Value!, "application/pdf", $"annual-report-{orgId}-{targetYear}.pdf");
        })
        .WithName("ExportAnnualReportPdf")
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status200OK, contentType: "application/pdf");

        return app;
    }
}
