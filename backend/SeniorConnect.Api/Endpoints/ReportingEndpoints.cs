using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SeniorConnect.Api.Common;
using SeniorConnect.Modules.Reporting.Application;

namespace SeniorConnect.Api.Endpoints;

public static class ReportingEndpoints
{
    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder app)
    {
        var reportGroup = app.MapGroup("/api/v1/reporting")
            .WithTags("Reporting")
            .RequireAuthorization();

        reportGroup.MapGet("/organizations/{orgId:guid}/impact-summary", async (
            Guid orgId,
            DateOnly? from,
            DateOnly? to,
            IReportingService reportService,
            CancellationToken ct) =>
        {
            var fromDate = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
            var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);

            var result = await reportService.GetImpactSummaryAsync(orgId, fromDate, toDate, ct);
            return result.ToHttpResult();
        })
        .WithName("GetImpactSummary")
        .Produces<ImpactReportSummaryDto>(StatusCodes.Status200OK);

        reportGroup.MapGet("/organizations/{orgId:guid}/export.csv", async (
            Guid orgId,
            DateOnly? from,
            DateOnly? to,
            IReportingService reportService,
            CancellationToken ct) =>
        {
            var fromDate = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
            var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);

            var result = await reportService.ExportImpactCsvAsync(orgId, fromDate, toDate, ct);
            if (result.IsFailure) return Results.BadRequest(result.Error?.Detail);

            return Results.File(result.Value!, "text/csv", $"impact-report-{orgId}.csv");
        })
        .WithName("ExportImpactCsv")
        .Produces(StatusCodes.Status200OK, contentType: "text/csv");

        return app;
    }
}
