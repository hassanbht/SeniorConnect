using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Organizations.Contracts;
using SeniorConnect.Modules.Reporting.Application;
using SeniorConnect.Modules.Reporting.Domain;

namespace SeniorConnect.Modules.Reporting.Infrastructure;

/// <summary>
/// Organization-side impact reporting. Authorization (org staff only) is
/// enforced by the caller through IOrganizationAccessGuard before any method
/// here runs. Every figure comes from v_volunteer_hours (confirmed,
/// non-deleted activities); nothing is estimated or back-filled (P2-20/P2-29).
/// </summary>
public sealed class ReportingService : IReportingService
{
    private static readonly CultureInfo DeAt = CultureInfo.GetCultureInfo("de-AT");

    private readonly IReportingDbContext _db;
    private readonly IOrganizationDirectory? _organizations;

    public ReportingService(IReportingDbContext db, IOrganizationDirectory? organizations = null)
    {
        _db = db;
        _organizations = organizations;
    }

    public async Task<Result<ImpactReportSummaryDto>> GetImpactSummaryAsync(
        Guid organizationId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (to < from)
        {
            return Error.Validation("'to' must not be before 'from'.");
        }

        var records = await LoadAsync(organizationId, from, to, cancellationToken);
        return Result<ImpactReportSummaryDto>.Success(Summarize(organizationId, from, to, records));
    }

    /// <summary>
    /// P2-29: people supported is the number of DISTINCT people over the whole
    /// period (not a sum of per-month/per-category distinct counts), filtered
    /// by the activity date so a partial first month is included. No
    /// fallback: zero people supported is reported as zero.
    /// </summary>
    public static ImpactReportSummaryDto Summarize(
        Guid organizationId, DateOnly from, DateOnly to, IReadOnlyCollection<VolunteerHoursView> records)
    {
        return new ImpactReportSummaryDto(
            OrganizationId: organizationId,
            From: from,
            To: to,
            TotalActivities: (int)records.Sum(r => r.ActivityCount),
            TotalHours: Math.Round(records.Sum(r => r.Hours), 1),
            ActiveVolunteersCount: records.Select(r => r.VolunteerUserId).Distinct().Count(),
            PeopleSupportedCount: records.Where(r => r.SubjectUserId != null).Select(r => r.SubjectUserId).Distinct().Count(),
            HoursByCategory: HoursByCategory(records));
    }

    private static Dictionary<string, double> HoursByCategory(IEnumerable<VolunteerHoursView> records) =>
        records
            .GroupBy(r => r.CategoryCode, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => Math.Round(g.Sum(r => r.Hours), 1), StringComparer.Ordinal);

    private async Task<List<VolunteerHoursView>> LoadAsync(
        Guid organizationId, DateOnly from, DateOnly to, CancellationToken ct) =>
        await _db.VolunteerHours
            .AsNoTracking()
            .Where(v => v.OrganizationId == organizationId && v.OccurredOn >= from && v.OccurredOn <= to)
            .ToListAsync(ct);

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
        sb.AppendLine(string.Format(DeAt, "{0};{1:dd.MM.yyyy};{2:dd.MM.yyyy};{3:F1};{4};{5};{6}",
            organizationId, from, to, summary.TotalHours, summary.TotalActivities, summary.ActiveVolunteersCount, summary.PeopleSupportedCount));
        sb.AppendLine();
        sb.AppendLine("Kategorie;Stunden");
        foreach (var (cat, hours) in summary.HoursByCategory)
        {
            sb.AppendLine(string.Format(DeAt, "{0};{1:F1}", cat, hours));
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
        var orgName = await OrganizationNameAsync(organizationId, cancellationToken);

        var lines = new List<SimplePdf.Line>
        {
            new("Mitanand / SeniorConnect Wirkungsbericht", 18),
            new(string.Format(DeAt, "Zeitraum: {0:dd.MM.yyyy} bis {1:dd.MM.yyyy}", from, to), 12, 30),
            new($"Organisation: {orgName ?? organizationId.ToString()}", 11, 30),
            new(string.Format(DeAt, "Geleistete Stunden: {0:F1} h", summary.TotalHours)),
            new($"Bestätigte Einsätze: {summary.TotalActivities}"),
            new($"Aktive Freiwillige: {summary.ActiveVolunteersCount}"),
            new($"Unterstützte Personen: {summary.PeopleSupportedCount}"),
            new("Aufschlüsselung nach Kategorien:", 11, 30),
        };
        lines.AddRange(summary.HoursByCategory.Select(kv =>
            new SimplePdf.Line(string.Format(DeAt, "- {0}: {1:F1} h", kv.Key, kv.Value))));
        lines.Add(new SimplePdf.Line("Grundlage: bestätigte Einsätze (BR-ROSTER-03).", 11, 30));

        return Result<byte[]>.Success(SimplePdf.Build(lines));
    }

    public async Task<Result<byte[]>> ExportImpactXlsxAsync(
        Guid organizationId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var summaryResult = await GetImpactSummaryAsync(organizationId, from, to, cancellationToken);
        if (summaryResult.IsFailure) return summaryResult.Error!;

        var summary = summaryResult.Value!;
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\"?>");
        sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
        sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
        sb.AppendLine(" xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
        sb.AppendLine(" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"");
        sb.AppendLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\"");
        sb.AppendLine(" xmlns:html=\"http://www.w3.org/TR/REC-html40\">");
        sb.AppendLine(" <Styles>");
        sb.AppendLine("  <Style ss:ID=\"Header\"><Font ss:Bold=\"1\"/><Interior ss:Color=\"#E0E7FF\" ss:Pattern=\"Solid\"/></Style>");
        sb.AppendLine("  <Style ss:ID=\"Title\"><Font ss:Size=\"14\" ss:Bold=\"1\"/></Style>");
        sb.AppendLine(" </Styles>");
        sb.AppendLine(" <Worksheet ss:Name=\"Wirkungsbericht\">");
        sb.AppendLine("  <Table>");
        sb.AppendLine("   <Row ss:StyleID=\"Title\"><Cell><Data ss:Type=\"String\">SeniorConnect / Mitanand Wirkungsbericht</Data></Cell></Row>");
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "   <Row><Cell><Data ss:Type=\"String\">Organisation: {0}</Data></Cell></Row>", organizationId));
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "   <Row><Cell><Data ss:Type=\"String\">Zeitraum: {0:dd.MM.yyyy} bis {1:dd.MM.yyyy}</Data></Cell></Row>", from, to));
        sb.AppendLine("   <Row/>");
        sb.AppendLine("   <Row ss:StyleID=\"Header\">");
        sb.AppendLine("    <Cell><Data ss:Type=\"String\">Kennzahl</Data></Cell>");
        sb.AppendLine("    <Cell><Data ss:Type=\"String\">Wert</Data></Cell>");
        sb.AppendLine("   </Row>");
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "   <Row><Cell><Data ss:Type=\"String\">Geleistete Stunden</Data></Cell><Cell><Data ss:Type=\"Number\">{0:F1}</Data></Cell></Row>", summary.TotalHours));
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "   <Row><Cell><Data ss:Type=\"String\">Bestaetigte Einsaetze</Data></Cell><Cell><Data ss:Type=\"Number\">{0}</Data></Cell></Row>", summary.TotalActivities));
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "   <Row><Cell><Data ss:Type=\"String\">Aktive Freiwillige</Data></Cell><Cell><Data ss:Type=\"Number\">{0}</Data></Cell></Row>", summary.ActiveVolunteersCount));
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "   <Row><Cell><Data ss:Type=\"String\">Unterstuetzte Personen</Data></Cell><Cell><Data ss:Type=\"Number\">{0}</Data></Cell></Row>", summary.PeopleSupportedCount));
        sb.AppendLine("   <Row/>");
        sb.AppendLine("   <Row ss:StyleID=\"Header\">");
        sb.AppendLine("    <Cell><Data ss:Type=\"String\">Kategorie</Data></Cell>");
        sb.AppendLine("    <Cell><Data ss:Type=\"String\">Stunden</Data></Cell>");
        sb.AppendLine("   </Row>");
        foreach (var (cat, hours) in summary.HoursByCategory)
        {
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "   <Row><Cell><Data ss:Type=\"String\">{0}</Data></Cell><Cell><Data ss:Type=\"Number\">{1:F1}</Data></Cell></Row>",
                System.Security.SecurityElement.Escape(cat), hours));
        }
        sb.AppendLine("  </Table>");
        sb.AppendLine(" </Worksheet>");
        sb.AppendLine("</Workbook>");

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return Result<byte[]>.Success(bytes);
    }

    public async Task<Result<AnnualStatisticsReportDto>> GetAnnualReportAsync(
        Guid organizationId,
        int year,
        CancellationToken cancellationToken = default)
    {
        if (year < 2000 || year > 9998)
        {
            return Error.Validation("Year is out of range.");
        }

        var current = await LoadAsync(organizationId, new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), cancellationToken);
        var previous = await LoadAsync(organizationId, new DateOnly(year - 1, 1, 1), new DateOnly(year - 1, 12, 31), cancellationToken);
        var orgName = await OrganizationNameAsync(organizationId, cancellationToken);

        return Result<AnnualStatisticsReportDto>.Success(BuildAnnualReport(organizationId, orgName, year, current, previous));
    }

    /// <summary>
    /// P2-20: derived only from confirmed activities. Pool, network partners,
    /// insured persons and events are not recorded by this module → null.
    /// </summary>
    public static AnnualStatisticsReportDto BuildAnnualReport(
        Guid organizationId,
        string? organizationName,
        int year,
        IReadOnlyCollection<VolunteerHoursView> current,
        IReadOnlyCollection<VolunteerHoursView> previous)
    {
        static int Volunteers(IEnumerable<VolunteerHoursView> r) => r.Select(h => h.VolunteerUserId).Distinct().Count();
        static int Activities(IEnumerable<VolunteerHoursView> r) => (int)r.Sum(h => h.ActivityCount);

        var placements = Activities(current);

        return new AnnualStatisticsReportDto(
            OrganizationId: organizationId,
            OrganizationName: organizationName,
            Year: year,
            TotalVolunteers: Metric(Volunteers(current), Volunteers(previous), year),
            VolunteerPool: null,
            NetworkPartners: null,
            Placements: Metric(placements, Activities(previous), year),
            InsuredPersons: null,
            EventsAndProjects: null,
            TotalHours: Math.Round(current.Sum(h => h.Hours), 1),
            TotalActivities: placements,
            HoursByCategory: HoursByCategory(current),
            GeneratedAtUtc: DateTimeOffset.UtcNow);
    }

    private static MetricWithGrowthDto Metric(int current, int previous, int year)
    {
        var diff = current - previous;
        var sign = diff >= 0 ? "+" : "";
        return new MetricWithGrowthDto(current, previous, diff, $"{sign}{diff} ({year})");
    }

    public async Task<Result<byte[]>> ExportAnnualReportPdfAsync(
        Guid organizationId,
        int year,
        CancellationToken cancellationToken = default)
    {
        var reportResult = await GetAnnualReportAsync(organizationId, year, cancellationToken);
        if (reportResult.IsFailure) return reportResult.Error!;

        var r = reportResult.Value!;

        static string Value(MetricWithGrowthDto? m) =>
            m is null ? "nicht erfasst" : $"{m.CurrentValue}  [{m.FormattedGrowth}]";

        var lines = new List<SimplePdf.Line>
        {
            new("Die Organisation in Zahlen", 20),
            new($"{r.OrganizationName ?? organizationId.ToString()} - Jahresbericht {year}", 13, 26),
            new("Grundlage: bestätigte Einsätze (BR-ROSTER-03). Nicht erfasste Werte werden nicht geschätzt.", 10, 20),
            new($"1. Aktive Freiwillige: {Value(r.TotalVolunteers)}", 14, 45),
            new($"2. Personen im Freiwilligenpool: {Value(r.VolunteerPool)}", 14, 30),
            new($"3. Vernetzungspartner:innen: {Value(r.NetworkPartners)}", 14, 30),
            new($"4. Vermittlungen (bestätigte Einsätze): {Value(r.Placements)}", 14, 30),
            new($"5. Versicherte: {Value(r.InsuredPersons)}", 14, 30),
            new($"6. Veranstaltungen & Projekte: {Value(r.EventsAndProjects)}", 14, 30),
            new(string.Format(DeAt, "Gesamte geleistete Stunden im Berichtsjahr: {0:F1} h", r.TotalHours), 10, 45),
            new(string.Format(DeAt, "Bericht automatisch generiert am {0:dd.MM.yyyy} über SeniorConnect / Mitanand.", DateTime.UtcNow), 10, 20),
        };

        return Result<byte[]>.Success(SimplePdf.Build(lines));
    }

    private async Task<string?> OrganizationNameAsync(Guid organizationId, CancellationToken ct) =>
        _organizations is null ? null : await _organizations.GetOrganizationNameAsync(organizationId, ct);
}
