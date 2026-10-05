using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SeniorConnect.Api.Common;
using SeniorConnect.Modules.Reporting.Application;

namespace SeniorConnect.Api.Endpoints;

/// <summary>
/// ADR-017: aggregate-only funder namespace. IFunderService checks the
/// caller's active funder membership (non-member → 404) on every call.
/// </summary>
public static class FunderEndpoints
{
    public static IEndpointRouteBuilder MapFunderEndpoints(this IEndpointRouteBuilder app)
    {
        var funderGroup = app.MapGroup("/api/v1/funder")
            .WithTags("Funder")
            .RequireAuthorization();

        funderGroup.MapGet("/{id:guid}/summary", async (
            Guid id,
            ClaimsPrincipal user,
            IFunderService funderService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return ResultExtensions.UnauthorizedProblem();
            var result = await funderService.GetFunderSummaryAsync(userId, id, ct);
            return result.ToHttpResult();
        })
        .WithName("GetFunderSummary")
        .Produces<FunderSummaryDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        funderGroup.MapGet("/{id:guid}/monthly-reports", async (
            Guid id,
            DateOnly? fromMonth,
            DateOnly? toMonth,
            ClaimsPrincipal user,
            IFunderService funderService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return ResultExtensions.UnauthorizedProblem();
            var result = await funderService.GetMonthlyReportsAsync(userId, id, fromMonth, toMonth, ct);
            return result.ToHttpResult();
        })
        .WithName("GetFunderMonthlyReports")
        .Produces<IReadOnlyList<FunderMonthlyReportItemDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        funderGroup.MapGet("/{id:guid}/dashboard", async (
            Guid id,
            DateOnly? from,
            DateOnly? to,
            ClaimsPrincipal user,
            IFunderService funderService,
            CancellationToken ct) =>
        {
            if (user.GetUserId() is not { } userId) return ResultExtensions.UnauthorizedProblem();
            var fromDate = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-365));
            var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);

            var result = await funderService.GetMultiOrgDashboardAsync(userId, id, fromDate, toDate, ct);
            return result.ToHttpResult();
        })
        .WithName("GetFunderMultiOrgDashboard")
        .Produces<MultiOrgFunderDashboardDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
