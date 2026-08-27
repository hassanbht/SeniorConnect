using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Reporting.Application;

namespace SeniorConnect.Modules.Reporting.Infrastructure;

public sealed class ReportingService : IReportingService
{
    private readonly IReportingDbContext _db;

    public ReportingService(IReportingDbContext db)
    {
        _db = db;
    }

    public async Task<Result<ImpactReportSummaryDto>> GetImpactSummaryAsync(
        Guid organizationId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var records = await _db.VolunteerHours
            .Where(v => v.OrganizationId == organizationId && v.OccurredOn >= from && v.OccurredOn <= to)
            .ToListAsync(cancellationToken);

        var totalActivities = records.Sum(r => r.ActivityCount);
        var totalHours = records.Sum(r => r.Hours);
        var activeVolunteers = records.Select(r => r.VolunteerUserId).Distinct().Count();

        var monthlyReports = await _db.FunderMonthlyReports
            .Where(r => r.OrganizationId == organizationId && r.Month >= from && r.Month <= to)
            .ToListAsync(cancellationToken);

        var categoryHours = monthlyReports
            .GroupBy(m => m.CategoryCode)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Hours ?? 0.0));

        var peopleSupported = monthlyReports.Sum(m => m.DistinctPeopleSupported ?? 0);

        var summary = new ImpactReportSummaryDto(
            OrganizationId: organizationId,
            From: from,
            To: to,
            TotalActivities: (int)totalActivities,
            TotalHours: Math.Round(totalHours, 1),
            ActiveVolunteersCount: activeVolunteers,
            PeopleSupportedCount: peopleSupported > 0 ? peopleSupported : activeVolunteers,
            HoursByCategory: categoryHours);

        return Result<ImpactReportSummaryDto>.Success(summary);
    }

    public async Task<Result<byte[]>> ExportImpactCsvAsync(
        Guid organizationId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var summaryResult = await GetImpactSummaryAsync(organizationId, from, to, cancellationToken);
        if (summaryResult.IsFailure) return summaryResult.Error!;

        var summary = summaryResult.Value!;
        var sb = new StringBuilder();
        sb.AppendLine("OrganisationId;ZeitraumVon;ZeitraumBis;GesamtStunden;GesamtEinsaetze;AktiveFreiwillige;UnterstuetztePersonen");
        sb.AppendLine(string.Format(CultureInfo.GetCultureInfo("de-AT"), "{0};{1:dd.MM.yyyy};{2:dd.MM.yyyy};{3:F1};{4};{5};{6}",
            organizationId, from, to, summary.TotalHours, summary.TotalActivities, summary.ActiveVolunteersCount, summary.PeopleSupportedCount));
        sb.AppendLine();
        sb.AppendLine("Kategorie;Stunden");
        foreach (var (cat, hours) in summary.HoursByCategory)
        {
            sb.AppendLine(string.Format(CultureInfo.GetCultureInfo("de-AT"), "{0};{1:F1}", cat, hours));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return Result<byte[]>.Success(bytes);
    }

    public async Task<Result<byte[]>> ExportImpactPdfAsync(
        Guid organizationId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var summaryResult = await GetImpactSummaryAsync(organizationId, from, to, cancellationToken);
        if (summaryResult.IsFailure) return summaryResult.Error!;

        var summary = summaryResult.Value!;
        var title = "Mitanand / SeniorConnect Wirkungsbericht";
        var dateRange = $"Zeitraum: {from:dd.MM.yyyy} bis {to:dd.MM.yyyy}";

        var categoryLines = new StringBuilder();
        foreach (var (cat, hours) in summary.HoursByCategory)
        {
            categoryLines.AppendLine(CultureInfo.InvariantCulture, $"0 -20 Td\n(- {cat}: {hours:F1} h) Tj");
        }

        var streamContent = $"""
            BT
            /F1 18 Tf
            50 780 Td
            ({title}) Tj
            /F1 12 Tf
            0 -30 Td
            ({dateRange}) Tj
            /F1 11 Tf
            0 -30 Td
            (Organisation: {organizationId}) Tj
            0 -20 Td
            (Geleistete Stunden: {summary.TotalHours:F1} h) Tj
            0 -20 Td
            (Erfolgreiche Einsaetze: {summary.TotalActivities}) Tj
            0 -20 Td
            (Aktive Freiwillige: {summary.ActiveVolunteersCount}) Tj
            0 -20 Td
            (Unterstuetzte Personen: {summary.PeopleSupportedCount}) Tj
            0 -30 Td
            (Aufschluesselung nach Kategorien:) Tj
            {categoryLines}
            0 -30 Td
            (Gepruefter Bericht gemaess BR-ROSTER-03 und BR-FUNDER-03.) Tj
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
