using System.Globalization;
using System.Text;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Reporting.Application;

namespace SeniorConnect.Modules.Reporting.Infrastructure;

public sealed class ReportingService : IReportingService
{
    public Task<Result<ImpactReportSummaryDto>> GetImpactSummaryAsync(
        Guid organizationId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var categoryHours = new Dictionary<string, double>
        {
            ["VISIT"] = 45.0,
            ["SHOPPING"] = 30.5,
            ["TECH_HELP"] = 12.0
        };

        var summary = new ImpactReportSummaryDto(
            OrganizationId: organizationId,
            From: from,
            To: to,
            TotalActivities: 58,
            TotalHours: 87.5,
            ActiveVolunteersCount: 15,
            PeopleSupportedCount: 22,
            HoursByCategory: categoryHours);

        return Task.FromResult(Result<ImpactReportSummaryDto>.Success(summary));
    }

    public Task<Result<byte[]>> ExportImpactCsvAsync(
        Guid organizationId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Category,Hours,ActivitiesCount");
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}", "Visit", 45.0, 30));
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}", "Shopping", 30.5, 20));
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}", "TechHelp", 12.0, 8));

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return Task.FromResult(Result<byte[]>.Success(bytes));
    }
}
