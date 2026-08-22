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

        reportGroup.MapGet("/organizations/{orgId:guid}/report.html", async (
            Guid orgId,
            DateOnly? from,
            DateOnly? to,
            IReportingService reportService,
            CancellationToken ct) =>
        {
            var fromDate = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
            var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);

            var result = await reportService.GetImpactSummaryAsync(orgId, fromDate, toDate, ct);
            if (result.IsFailure) return Results.BadRequest(result.Error?.Detail);

            var summary = result.Value!;
            var html = $$"""
                <!DOCTYPE html>
                <html lang="de">
                <head>
                    <meta charset="utf-8">
                    <title>Wirkungsbericht — SeniorConnect</title>
                    <style>
                        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; padding: 40px; color: #1E293B; }
                        h1 { color: #4338CA; border-bottom: 2px solid #E2E8F0; padding-bottom: 12px; }
                        .grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 20px; margin: 30px 0; }
                        .card { background: #F8FAFC; border: 1px solid #E2E8F0; border-radius: 8px; padding: 20px; text-align: center; }
                        .card .number { font-size: 32px; font-weight: 700; color: #4338CA; }
                        .card .label { font-size: 14px; color: #64748B; margin-top: 4px; }
                    </style>
                </head>
                <body>
                    <h1>SeniorConnect Wirkungsbericht</h1>
                    <p>Zeitraum: {{fromDate:dd.MM.yyyy}} bis {{toDate:dd.MM.yyyy}}</p>
                    <div class="grid">
                        <div class="card"><div class="number">{{summary.TotalHours:F1}} h</div><div class="label">Geleistete Stunden</div></div>
                        <div class="card"><div class="number">{{summary.TotalActivities}}</div><div class="label">Erfolgreiche Einsätze</div></div>
                        <div class="card"><div class="number">{{summary.ActiveVolunteersCount}}</div><div class="label">Aktive Freiwillige</div></div>
                    </div>
                </body>
                </html>
                """;

            return Results.Content(html, "text/html");
        })
        .WithName("GetImpactHtmlReport")
        .Produces(StatusCodes.Status200OK, contentType: "text/html");

        return app;
    }
}
