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

        return app;
    }
}
