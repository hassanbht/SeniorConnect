using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Reporting.Application;
using SeniorConnect.Modules.Reporting.Contracts;
using SeniorConnect.Modules.Reporting.Domain;

namespace SeniorConnect.Modules.Reporting.Infrastructure;

/// <summary>
/// ADR-017 / BR-FUNDER. Every read is authorized against the caller's ACTIVE
/// funder membership (BR-FUNDER-04), limited to the funder's own rows of
/// organizations with an active funding relationship, suppressed row-wise
/// below a cohort of 10 (BR-FUNDER-03) and audited (BR-FUNDER-05).
/// </summary>
public sealed class FunderService : IFunderService
{
    public const string Suppressed = "<10";

    private readonly IReportingDbContext _db;

    public FunderService(IReportingDbContext db)
    {
        _db = db;
    }

    public async Task<Result<FunderSummaryDto>> GetFunderSummaryAsync(
        Guid callerUserId,
        Guid funderId,
        CancellationToken cancellationToken = default)
    {
        var access = await AuthorizeAsync(callerUserId, funderId, "funder.summary.read", cancellationToken);
        if (access.IsFailure) return access.Error!;

        var (funder, orgIds) = access.Value!;
        var rows = await FunderRowsQuery(funderId, orgIds).ToListAsync(cancellationToken);
        var aggregate = Aggregate(rows);

        return Result<FunderSummaryDto>.Success(new FunderSummaryDto(
            FunderId: funder.Id,
            FunderName: funder.Name,
            TotalFundedOrganizations: orgIds.Count,
            OverallHours: aggregate.TotalHours,
            OverallPeopleSupported: aggregate.TotalPeopleSupported));
    }

    public async Task<Result<IReadOnlyList<FunderMonthlyReportItemDto>>> GetMonthlyReportsAsync(
        Guid callerUserId,
        Guid funderId,
        DateOnly? fromMonth = null,
        DateOnly? toMonth = null,
        CancellationToken cancellationToken = default)
    {
        var access = await AuthorizeAsync(callerUserId, funderId, "funder.monthly_reports.read", cancellationToken);
        if (access.IsFailure) return access.Error!;

        var rows = await RangeQuery(funderId, access.Value!.OrganizationIds, fromMonth, toMonth).ToListAsync(cancellationToken);
        return Result<IReadOnlyList<FunderMonthlyReportItemDto>>.Success(Aggregate(rows).Rows);
    }

    public async Task<Result<MultiOrgFunderDashboardDto>> GetMultiOrgDashboardAsync(
        Guid callerUserId,
        Guid funderId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var access = await AuthorizeAsync(callerUserId, funderId, "funder.dashboard.read", cancellationToken);
        if (access.IsFailure) return access.Error!;

        var (funder, orgIds) = access.Value!;
        var rows = await RangeQuery(funderId, orgIds, from, to).ToListAsync(cancellationToken);
        var aggregate = Aggregate(rows);

        return Result<MultiOrgFunderDashboardDto>.Success(new MultiOrgFunderDashboardDto(
            FunderId: funderId,
            FunderName: funder.Name,
            RegionName: funder.Name,
            From: from,
            To: to,
            ActiveOrganizationsCount: orgIds.Count,
            TotalAggregatedHours: aggregate.TotalHours,
            TotalAggregatedBeneficiaries: aggregate.TotalPeopleSupported,
            OrganizationMetrics: aggregate.Rows));
    }

    private sealed record FunderAccess(Funder Funder, IReadOnlyList<Guid> OrganizationIds);

    private async Task<Result<FunderAccess>> AuthorizeAsync(
        Guid callerUserId, Guid funderId, string action, CancellationToken ct)
    {
        var funder = await _db.Funders
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == funderId && f.Status == FunderStatus.Active, ct);

        var isMember = funder is not null && await _db.FunderMemberships
            .AsNoTracking()
            .AnyAsync(m => m.FunderId == funderId
                        && m.UserId == callerUserId
                        && m.Status == FunderMembershipStatus.Active, ct);

        // Unknown funder and "not your funder" are indistinguishable: 404.
        if (funder is null || !isMember)
        {
            return Error.NotFound("Funder");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var orgIds = await _db.FundingRelationships
            .AsNoTracking()
            .Where(r => r.FunderId == funderId
                     && r.ValidFrom <= today
                     && (r.ValidUntil == null || r.ValidUntil >= today))
            .Select(r => r.OrganizationId)
            .Distinct()
            .ToListAsync(ct);

        // BR-FUNDER-05: audited like any other access. Aggregate-only metadata.
        _db.AuditEntries.Add(AuditEntry.Create(
            action: action,
            subjectType: "Funder",
            subjectId: funderId,
            actorUserId: callerUserId,
            metadataJson: JsonSerializer.Serialize(new { organizations = orgIds.Count })));
        await _db.SaveChangesAsync(ct);

        return new FunderAccess(funder, orgIds);
    }

    private IQueryable<FunderMonthlyReportView> FunderRowsQuery(Guid funderId, IReadOnlyList<Guid> orgIds) =>
        _db.FunderMonthlyReports
            .AsNoTracking()
            .Where(r => r.FunderId == funderId && orgIds.Contains(r.OrganizationId));

    private IQueryable<FunderMonthlyReportView> RangeQuery(
        Guid funderId, IReadOnlyList<Guid> orgIds, DateOnly? from, DateOnly? to)
    {
        var query = FunderRowsQuery(funderId, orgIds);

        // View rows are keyed by the first of the month; include the month
        // that contains `from` rather than dropping a partial first month.
        if (from.HasValue)
        {
            var firstMonth = new DateOnly(from.Value.Year, from.Value.Month, 1);
            query = query.Where(r => r.Month >= firstMonth);
        }

        if (to.HasValue)
            query = query.Where(r => r.Month <= to.Value);

        return query;
    }

    public sealed record FunderAggregate(
        IReadOnlyList<FunderMonthlyReportItemDto> Rows,
        string TotalHours,
        string TotalPeopleSupported);

    /// <summary>
    /// BR-FUNDER-03. Totals are computed over UNSUPPRESSED rows only, so
    /// "total − visible rows" is always zero and a suppressed cohort can never
    /// be recovered by subtraction. When the visible total itself covers
    /// fewer than 10 people, the totals are suppressed too.
    /// </summary>
    public static FunderAggregate Aggregate(IEnumerable<FunderMonthlyReportView> rows)
    {
        var items = rows
            .OrderBy(r => r.Month).ThenBy(r => r.OrganizationId).ThenBy(r => r.CategoryCode, StringComparer.Ordinal)
            .Select(ToSuppressedDto)
            .ToList();

        var visible = rows.Where(r => !IsSuppressed(r)).ToList();

        // ponytail: sum of per-row distinct counts — a person active in two
        // months/categories counts twice. The funder surface carries no
        // identifiers by design, so a true distinct count needs a dedicated view.
        var people = visible.Sum(r => r.DistinctPeopleSupported ?? 0);
        var hours = visible.Sum(r => r.Hours ?? 0.0);
        var totalsSuppressed = people < FunderSuppression.MinimumCohortSize;

        return new FunderAggregate(
            items,
            totalsSuppressed ? Suppressed : string.Create(CultureInfo.InvariantCulture, $"{hours:F1} h"),
            totalsSuppressed ? Suppressed : people.ToString(CultureInfo.InvariantCulture));
    }

    private static bool IsSuppressed(FunderMonthlyReportView row) =>
        row.IsSuppressed
        || (row.DistinctVolunteers ?? 0) < FunderSuppression.MinimumCohortSize
        || (row.DistinctPeopleSupported ?? 0) < FunderSuppression.MinimumCohortSize;

    /// <summary>
    /// P2-34 / BR-FUNDER-03: a suppressed row masks EVERY measure — activity
    /// count and hours included — never cell by cell.
    /// </summary>
    public static FunderMonthlyReportItemDto ToSuppressedDto(FunderMonthlyReportView row)
    {
        if (IsSuppressed(row))
        {
            return new FunderMonthlyReportItemDto(
                row.OrganizationId, row.Month, row.CategoryCode,
                Suppressed, Suppressed, Suppressed, Suppressed, IsSuppressed: true);
        }

        return new FunderMonthlyReportItemDto(
            OrganizationId: row.OrganizationId,
            Month: row.Month,
            CategoryCode: row.CategoryCode,
            ActivityCount: (row.ActivityCount ?? 0).ToString(CultureInfo.InvariantCulture),
            DistinctVolunteers: (row.DistinctVolunteers ?? 0).ToString(CultureInfo.InvariantCulture),
            DistinctPeopleSupported: (row.DistinctPeopleSupported ?? 0).ToString(CultureInfo.InvariantCulture),
            TotalHours: (row.Hours ?? 0.0).ToString("0.0", CultureInfo.InvariantCulture),
            IsSuppressed: false);
    }
}
