using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Reporting.Application;

namespace SeniorConnect.Modules.Reporting.Infrastructure;

public sealed class EsgReportingService : IEsgReportingService
{
    private readonly IReportingDbContext _db;

    public EsgReportingService(IReportingDbContext db)
    {
        _db = db;
    }

    public async Task<Result<CorporateEsgSummaryDto>> GetCorporateEsgSummaryAsync(
        Guid companyOrganizationId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var records = await _db.VolunteerHours
            .AsNoTracking()
            .Where(v => v.OrganizationId == companyOrganizationId && v.OccurredOn >= from && v.OccurredOn <= to)
            .ToListAsync(cancellationToken);

        var totalHours = records.Sum(r => r.Hours);
        var participatingEmployees = records.Select(r => r.VolunteerUserId).Distinct().Count();

        // P2-29: category split and distinct beneficiaries come from the same
        // confirmed-activity source; no substitution when there are none.
        var categoryHours = records
            .GroupBy(r => r.CategoryCode, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => Math.Round(g.Sum(x => x.Hours), 1), StringComparer.Ordinal);

        var beneficiaries = records.Where(r => r.SubjectUserId != null).Select(r => r.SubjectUserId).Distinct().Count();

        // Estimated social return value: Austrian benchmark ~ €35/hour of specialized community care and accompaniment
        var estimatedSocialValue = Math.Round(totalHours * 35.0, 2);

        var sdgs = new List<string>
        {
            "SDG 3: Good Health and Well-being (Gesundheit und Wohlergehen)",
            "SDG 10: Reduced Inequalities (Weniger Ungleichheiten)",
            "SDG 11: Sustainable Cities and Communities (Nachhaltige Städte und Gemeinden)"
        };

        var summary = new CorporateEsgSummaryDto(
            CompanyOrganizationId: companyOrganizationId,
            CompanyName: "Corporate Volunteering Partner",
            From: from,
            To: to,
            ParticipatingEmployeesCount: participatingEmployees,
            TotalVolunteerHours: Math.Round(totalHours, 1),
            BeneficiariesSupportedCount: beneficiaries,
            HoursByCategory: categoryHours,
            SdgsImpacted: sdgs,
            EstimatedSocialValueEur: estimatedSocialValue);

        return Result<CorporateEsgSummaryDto>.Success(summary);
    }

    public async Task<Result<byte[]>> ExportEsgCertificatePdfAsync(
        Guid companyOrganizationId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var summaryResult = await GetCorporateEsgSummaryAsync(companyOrganizationId, from, to, cancellationToken);
        if (summaryResult.IsFailure) return summaryResult.Error!;

        var summary = summaryResult.Value!;
        var title = "SeniorConnect / Mitanand Corporate ESG Certificate";
        var subtitle = $"Offizieller Nachweis gesellschaftlichen Engagements ({from:dd.MM.yyyy} - {to:dd.MM.yyyy})";

        var lines = new List<SimplePdf.Line>
        {
            new(title, 16),
            new(subtitle, 11, 25),
            new($"Unternehmen ID: {companyOrganizationId}", 11, 30),
            new($"Teilnehmende Mitarbeiter:innen: {summary.ParticipatingEmployeesCount}"),
            new(string.Format(CultureInfo.GetCultureInfo("de-AT"), "Geleistete Freiwilligenstunden: {0:F1} h", summary.TotalVolunteerHours)),
            new($"Unterstützte Personen vor Ort: {summary.BeneficiariesSupportedCount}"),
            new(string.Format(CultureInfo.GetCultureInfo("de-AT"), "Berechneter sozialer Mehrwert: EUR {0:N2}", summary.EstimatedSocialValueEur)),
            new("Beitrag zu den UN-Nachhaltigkeitszielen (SDGs):", 11, 30),
            new("- SDG 3: Gesundheit und Wohlergehen im Alter"),
            new("- SDG 10: Inklusion und Reduktion sozialer Isolation"),
            new("- SDG 11: Stärkung lokaler Nachbarschaften in Österreich"),
            new("Bestätigt gemäß den SeniorConnect Corporate Volunteering Standards.", 11, 35),
        };

        return Result<byte[]>.Success(SimplePdf.Build(lines));
    }
}

