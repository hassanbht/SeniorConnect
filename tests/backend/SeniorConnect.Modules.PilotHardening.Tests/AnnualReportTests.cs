using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Reporting.Domain;
using SeniorConnect.Modules.Reporting.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.PilotHardening.Tests;

/// <summary>
/// P2-20 / P2-29 / P2-30: every reported figure is derived from confirmed
/// activities; nothing is hard-coded, estimated or back-filled, and exported
/// PDFs have a valid cross-reference table.
/// </summary>
public sealed class AnnualReportTests
{
    private static SeniorConnectDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<SeniorConnectDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            new DefaultTenantContext());

    private static VolunteerHoursView Hours(Guid volunteer, Guid? subject, string category, DateOnly on, long activities, double hours) => new()
    {
        VolunteerUserId = volunteer,
        OrganizationId = Guid.Empty,
        SubjectUserId = subject,
        CategoryCode = category,
        OccurredOn = on,
        OccurredMonth = new DateOnly(on.Year, on.Month, 1),
        ActivityCount = activities,
        Minutes = (long)(hours * 60),
        Hours = hours,
    };

    [Fact]
    public async Task GetAnnualReport_ForNamedOrganization_NoLongerInventsOfficialFigures()
    {
        // Arrange — a named legal profile used to trigger hard-coded 304/261/450…
        using var db = CreateDbContext();
        var profile = LegalEntityProfile.CreateEmpty();
        profile.UpdateEntity("Testverein Musterstadt", LegalForm.Verein, "ZVR-123456789",
            "Musterstraße 1, 1010 Wien", null, null, null, null);
        db.LegalEntityProfiles.Add(profile);
        await db.SaveChangesAsync();

        // Act
        var result = await new ReportingService(db).GetAnnualReportAsync(Guid.NewGuid(), 2025);

        // Assert — no recorded activity means zeros, and unrecorded figures are null.
        var report = result.Value!;
        report.TotalVolunteers.CurrentValue.Should().Be(0);
        report.Placements.CurrentValue.Should().Be(0);
        report.VolunteerPool.Should().BeNull();
        report.NetworkPartners.Should().BeNull();
        report.InsuredPersons.Should().BeNull();
        report.EventsAndProjects.Should().BeNull();
        report.OrganizationName.Should().BeNull("the platform's own legal entity is not the reporting organization");
    }

    [Fact]
    public void BuildAnnualReport_DerivesVolunteersAndPlacementsWithGrowthFromData()
    {
        // Arrange
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var current = new[]
        {
            Hours(a, Guid.NewGuid(), "shopping", new DateOnly(2026, 3, 1), 2, 3.0),
            Hours(b, Guid.NewGuid(), "shopping", new DateOnly(2026, 4, 1), 1, 1.5),
            Hours(c, Guid.NewGuid(), "visit", new DateOnly(2026, 5, 1), 3, 4.5),
        };
        var previous = new[] { Hours(a, Guid.NewGuid(), "visit", new DateOnly(2025, 5, 1), 1, 1.0) };

        // Act
        var report = ReportingService.BuildAnnualReport(Guid.NewGuid(), "Org", 2026, current, previous);

        // Assert
        report.TotalVolunteers.CurrentValue.Should().Be(3);
        report.TotalVolunteers.FormattedGrowth.Should().Be("+2 (2026)");
        report.Placements.CurrentValue.Should().Be(6);
        report.Placements.PreviousYearValue.Should().Be(1);
        report.TotalHours.Should().Be(9.0);
        report.HoursByCategory.Should().BeEquivalentTo(new Dictionary<string, double> { ["shopping"] = 4.5, ["visit"] = 4.5 });
    }

    [Fact]
    public void Summarize_CountsDistinctPeopleOverTheWholePeriod_NotPerMonth()
    {
        // The same senior helped in two months and two categories is ONE person.
        var senior = Guid.NewGuid();
        var records = new[]
        {
            Hours(Guid.NewGuid(), senior, "shopping", new DateOnly(2026, 8, 30), 1, 1.0),
            Hours(Guid.NewGuid(), senior, "visit", new DateOnly(2026, 9, 2), 1, 1.0),
            Hours(Guid.NewGuid(), Guid.NewGuid(), "visit", new DateOnly(2026, 9, 3), 1, 1.0),
        };

        var summary = ReportingService.Summarize(Guid.NewGuid(), new DateOnly(2026, 8, 15), new DateOnly(2026, 9, 30), records);

        summary.PeopleSupportedCount.Should().Be(2);
        summary.ActiveVolunteersCount.Should().Be(3);
        summary.TotalActivities.Should().Be(3);
    }

    [Fact]
    public void Summarize_ReportsZeroPeopleSupported_InsteadOfFallingBackToVolunteerCount()
    {
        var records = new[] { Hours(Guid.NewGuid(), null, "admin", new DateOnly(2026, 9, 1), 1, 2.0) };

        var summary = ReportingService.Summarize(Guid.NewGuid(), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), records);

        summary.PeopleSupportedCount.Should().Be(0);
        summary.ActiveVolunteersCount.Should().Be(1);
    }

    [Fact]
    public async Task ExportAnnualReportPdf_ProducesAPdfWithCorrectXrefOffsets()
    {
        using var db = CreateDbContext();

        var result = await new ReportingService(db).ExportAnnualReportPdfAsync(Guid.NewGuid(), 2025);

        AssertValidPdf(result.Value!);
    }

    [Fact]
    public async Task ExportImpactPdf_ProducesAPdfWithCorrectXrefOffsets()
    {
        using var db = CreateDbContext();

        var result = await new ReportingService(db).ExportImpactPdfAsync(
            Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));

        AssertValidPdf(result.Value!);
    }

    [Fact]
    public void SimplePdf_EscapesParenthesesAndKeepsUmlauts()
    {
        var bytes = SimplePdf.Build([new SimplePdf.Line("Unterstützte (a) Personen \\ Österreich")]);

        var text = Encoding.Latin1.GetString(bytes);
        text.Should().Contain(@"(Unterstützte \(a\) Personen \\ Österreich) Tj");
        AssertValidPdf(bytes);
    }

    private static void AssertValidPdf(byte[] bytes)
    {
        var text = Encoding.Latin1.GetString(bytes);
        text.Should().StartWith("%PDF-1.4").And.EndWith("%%EOF\n");

        var startXref = int.Parse(Regex.Match(text, @"startxref\n(\d+)\n%%EOF").Groups[1].Value);
        text[startXref..].Should().StartWith("xref\n", "startxref must point at the xref table");

        var entries = Regex.Matches(text[startXref..], @"(\d{10}) 00000 n ");
        entries.Should().HaveCount(5);
        for (var i = 0; i < entries.Count; i++)
        {
            var offset = int.Parse(entries[i].Groups[1].Value);
            text[offset..].Should().StartWith($"{i + 1} 0 obj", $"xref entry {i + 1} must point at its object");
        }

        var length = int.Parse(Regex.Match(text, @"<< /Length (\d+) >>\nstream\n").Groups[1].Value);
        var streamStart = text.IndexOf(">>\nstream\n", StringComparison.Ordinal) + ">>\nstream\n".Length;
        text.Substring(streamStart + length, "\nendstream".Length).Should().Be("\nendstream");
    }
}
