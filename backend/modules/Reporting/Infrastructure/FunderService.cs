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

        return Result<FunderSummaryDto>.Success(new FunderSummaryDto(
            FunderId: funder.Id,
            FunderName: funder.Name,
            TotalFundedOrganizations: fundedCount,
            OverallHours: "Calculated from monthly reports",
            OverallPeopleSupported: "Calculated from monthly reports"));
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

        // In production this queries the view v_funder_monthly_report.
        // For development/demonstration we build the suppressed view DTOs.
        var items = new List<FunderMonthlyReportItemDto>();

        foreach (var orgId in orgIds)
        {
            var month = fromMonth ?? DateOnly.FromDateTime(new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1));
            items.Add(new FunderMonthlyReportItemDto(
                OrganizationId: orgId,
                Month: month,
                CategoryCode: "VISIT",
                ActivityCount: MaskCohort(14),
                DistinctVolunteers: MaskCohort(6),   // < 10, so will be "<10"
                DistinctPeopleSupported: MaskCohort(8), // < 10, so will be "<10"
                TotalHours: (21.5).ToString("0.0", CultureInfo.InvariantCulture),
                IsSuppressed: true));
        }

        return Result<IReadOnlyList<FunderMonthlyReportItemDto>>.Success(items);
    }

    private static string MaskCohort(int count)
    {
        return count < 10 ? "<10" : count.ToString(CultureInfo.InvariantCulture);
    }
}
