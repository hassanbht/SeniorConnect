using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Reporting.Application;
using SeniorConnect.Modules.Reporting.Domain;

namespace SeniorConnect.Modules.Reporting.Infrastructure;

public sealed class FunderService : IFunderService
{
    private readonly IReportingDbContext _db;

    public FunderService(IReportingDbContext db)
    {
        _db = db;
    }

    public async Task<Result<FunderSummaryDto>> GetFunderSummaryAsync(
        Guid funderId,
        CancellationToken cancellationToken = default)
    {
        var funder = await _db.Funders
            .FirstOrDefaultAsync(f => f.Id == funderId && f.Status == FunderStatus.Active, cancellationToken);

        if (funder is null)
        {
            return Error.NotFound("Funder");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fundedCount = await _db.FundingRelationships
            .Where(r => r.FunderId == funderId && (r.ValidUntil == null || r.ValidUntil >= today))
            .CountAsync(cancellationToken);

        var relationships = await _db.FundingRelationships
            .Where(r => r.FunderId == funderId && (r.ValidUntil == null || r.ValidUntil >= today))
            .ToListAsync(cancellationToken);

        var orgIds = relationships.Select(r => r.OrganizationId).ToList();

        var reports = await _db.FunderMonthlyReports
            .Where(r => orgIds.Contains(r.OrganizationId))
            .ToListAsync(cancellationToken);

        var totalHours = reports.Sum(r => r.Hours ?? 0.0);
        var totalPeople = reports.Sum(r => r.DistinctPeopleSupported ?? 0);

        return Result<FunderSummaryDto>.Success(new FunderSummaryDto(
            FunderId: funder.Id,
            FunderName: funder.Name,
            TotalFundedOrganizations: fundedCount,
            OverallHours: $"{totalHours:F1} h",
            OverallPeopleSupported: totalPeople < 10 ? "<10" : totalPeople.ToString(CultureInfo.InvariantCulture)));
    }

    public async Task<Result<IReadOnlyList<FunderMonthlyReportItemDto>>> GetMonthlyReportsAsync(
        Guid funderId,
        DateOnly? fromMonth = null,
        DateOnly? toMonth = null,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var relationships = await _db.FundingRelationships
            .Where(r => r.FunderId == funderId && (r.ValidUntil == null || r.ValidUntil >= today))
            .ToListAsync(cancellationToken);

        var orgIds = relationships.Select(r => r.OrganizationId).ToList();

        var query = _db.FunderMonthlyReports
            .Where(r => orgIds.Contains(r.OrganizationId));

        if (fromMonth.HasValue)
            query = query.Where(r => r.Month >= fromMonth.Value);

        if (toMonth.HasValue)
            query = query.Where(r => r.Month <= toMonth.Value);

        var viewRows = await query.ToListAsync(cancellationToken);

        var items = viewRows.Select(ToSuppressedDto).ToList();

        return Result<IReadOnlyList<FunderMonthlyReportItemDto>>.Success(items);
    }

    /// <summary>
    /// P2-34 / BR-FUNDER-03: a cohort under 10 must suppress EVERY measure in
    /// the row, not just the headcount — TotalHours included, or a funder
    /// could re-derive who a "&lt;10" cohort is by cross-referencing hours
    /// against other data they hold.
    /// </summary>
    public static FunderMonthlyReportItemDto ToSuppressedDto(FunderMonthlyReportView row)
    {
        var isSuppressed = row.IsSuppressed
            || (row.DistinctVolunteers ?? 0) < 10
            || (row.DistinctPeopleSupported ?? 0) < 10;

        return new FunderMonthlyReportItemDto(
            OrganizationId: row.OrganizationId,
            Month: row.Month,
            CategoryCode: row.CategoryCode,
            ActivityCount: MaskCohort(row.ActivityCount ?? 0),
            DistinctVolunteers: MaskCohort(row.DistinctVolunteers ?? 0),
            DistinctPeopleSupported: MaskCohort(row.DistinctPeopleSupported ?? 0),
            TotalHours: isSuppressed ? "<10" : (row.Hours ?? 0.0).ToString("0.0", CultureInfo.InvariantCulture),
            IsSuppressed: isSuppressed
        );
    }

    public async Task<Result<MultiOrgFunderDashboardDto>> GetMultiOrgDashboardAsync(
        Guid funderId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var summaryResult = await GetFunderSummaryAsync(funderId, cancellationToken);
        if (summaryResult.IsFailure) return summaryResult.Error!;

        var summary = summaryResult.Value!;
        var reportsResult = await GetMonthlyReportsAsync(funderId, from, to, cancellationToken);
        if (reportsResult.IsFailure) return reportsResult.Error!;

        var metrics = reportsResult.Value!;

        var dashboard = new MultiOrgFunderDashboardDto(
            FunderId: funderId,
            FunderName: summary.FunderName,
            RegionName: "Gemeinde Pilotregion",
            From: from,
            To: to,
            ActiveOrganizationsCount: summary.TotalFundedOrganizations,
            TotalAggregatedHours: summary.OverallHours,
            TotalAggregatedBeneficiaries: summary.OverallPeopleSupported,
            OrganizationMetrics: metrics);

        return Result<MultiOrgFunderDashboardDto>.Success(dashboard);
    }

    private static string MaskCohort(int count)
    {
        return count < 10 ? "<10" : count.ToString(CultureInfo.InvariantCulture);
    }
}
