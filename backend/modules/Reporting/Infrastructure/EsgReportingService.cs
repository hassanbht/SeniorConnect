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
        var totalActivities = (int)records.Sum(r => r.ActivityCount);

        var monthlyReports = await _db.FunderMonthlyReports
            .AsNoTracking()
            .Where(r => r.OrganizationId == companyOrganizationId && r.Month >= from && r.Month <= to)
            .ToListAsync(cancellationToken);

        var categoryHours = monthlyReports
            .GroupBy(m => m.CategoryCode)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Hours ?? 0.0));

        var beneficiaries = monthlyReports.Sum(m => m.DistinctPeopleSupported ?? 0);
        if (beneficiaries == 0) beneficiaries = totalActivities;

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

        var streamContent = $"""
            BT
            /F1 16 Tf
            50 780 Td
            ({title}) Tj
            /F1 11 Tf
            0 -25 Td
            ({subtitle}) Tj
            0 -30 Td
            (Unternehmen ID: {companyOrganizationId}) Tj
            0 -20 Td
            (Teilnehmende Mitarbeiter:innen: {summary.ParticipatingEmployeesCount}) Tj
            0 -20 Td
            (Geleistete Freiwilligenstunden: {summary.TotalVolunteerHours:F1} h) Tj
            0 -20 Td
            (Unterstuetzte Personen vor Ort: {summary.BeneficiariesSupportedCount}) Tj
            0 -20 Td
            (Berechneter sozialer Mehrwert: EUR {summary.EstimatedSocialValueEur:N2}) Tj
            0 -30 Td
            (Beitrag zu den UN-Nachhaltigkeitszielen (SDGs):) Tj
            0 -20 Td
            (- SDG 3: Gesundheit und Wohlergehen im Alter) Tj
            0 -20 Td
            (- SDG 10: Inklusion und Reduktion sozialer Isolation) Tj
            0 -20 Td
            (- SDG 11: Staerkung lokaler Nachbarschaften in Oesterreich) Tj
            0 -35 Td
            (Bestaetigt gemaess den SeniorConnect Corporate Volunteering Standards.) Tj
            ET
            """;

        var streamBytes = Encoding.ASCII.GetBytes(streamContent);
        var pdfDoc = $"""
            %PDF-1.4
            1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj
            2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj
            3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >> endobj
            5 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj
            4 0 obj
            << /Length {streamBytes.Length} >>
            stream
            {streamContent}
            endstream
            endobj
            xref
            0 6
            0000000000 65535 f 
            0000000009 00000 n 
            0000000058 00000 n 
            0000000115 00000 n 
            0000000280 00000 n 
            0000000220 00000 n 
            trailer << /Size 6 /Root 1 0 R >>
            startxref
            {streamBytes.Length + 400}
            %%EOF
            """;

        return Result<byte[]>.Success(Encoding.ASCII.GetBytes(pdfDoc));
    }
}

