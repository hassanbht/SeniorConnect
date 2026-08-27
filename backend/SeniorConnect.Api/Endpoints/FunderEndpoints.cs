using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SeniorConnect.Api.Common;
using SeniorConnect.Modules.Reporting.Application;

namespace SeniorConnect.Api.Endpoints;

public static class FunderEndpoints
{
    public static IEndpointRouteBuilder MapFunderEndpoints(this IEndpointRouteBuilder app)
    {
        var funderGroup = app.MapGroup("/api/v1/funder")
            .WithTags("Funder")
            .RequireAuthorization();

        funderGroup.MapGet("/{id:guid}/summary", async (
            Guid id,
            IFunderService funderService,
            CancellationToken ct) =>
        {
            var result = await funderService.GetFunderSummaryAsync(id, ct);
            return result.ToHttpResult();
        })
        .WithName("GetFunderSummary")
        .Produces<FunderSummaryDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        funderGroup.MapGet("/{id:guid}/monthly-reports", async (
            Guid id,
            DateOnly? fromMonth,
            DateOnly? toMonth,
            IFunderService funderService,
            CancellationToken ct) =>
        {
            var result = await funderService.GetMonthlyReportsAsync(id, fromMonth, toMonth, ct);
            return result.ToHttpResult();
        })
        .WithName("GetFunderMonthlyReports")
        .Produces<IReadOnlyList<FunderMonthlyReportItemDto>>(StatusCodes.Status200OK);

        funderGroup.MapGet("/{id:guid}/dashboard", async (
            Guid id,
            DateOnly? from,
            DateOnly? to,
            IFunderService funderService,
            CancellationToken ct) =>
        {
            var fromDate = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-365));
            var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);

            var result = await funderService.GetMultiOrgDashboardAsync(id, fromDate, toDate, ct);
            return result.ToHttpResult();
        })
        .WithName("GetFunderMultiOrgDashboard")
        .Produces<MultiOrgFunderDashboardDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
