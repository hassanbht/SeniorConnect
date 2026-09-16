using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Reporting.Domain;
using SeniorConnect.Modules.Reporting.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.PilotHardening.Tests;

public sealed class AnnualReportTests
{
    private SeniorConnectDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SeniorConnectDbContext(options, new DefaultTenantContext());
    }

    [Fact]
    public async Task GetAnnualReportAsync_ForFwzInnsbruckLand_ReturnsOfficial2025MetricsWithGrowth()
    {
        using var db = CreateDbContext();
        var orgId = Guid.NewGuid();

        // Legal profile with FWZ name
        var profile = LegalEntityProfile.CreateEmpty();
        profile.UpdateEntity(
            "Freiwilligenzentrum Innsbruck-Land",
            LegalForm.Verein,
            "ZVR-123456789",
            "Dorfplatz 2, 6175 Kematen in Tirol",
            "ATU12345678",
            null, null, null);
        db.LegalEntityProfiles.Add(profile);
        await db.SaveChangesAsync();

        var service = new ReportingService(db);

        var result = await service.GetAnnualReportAsync(orgId, 2025);
        result.IsSuccess.Should().BeTrue();

        var report = result.Value;
        report.Should().NotBeNull();
        report.Year.Should().Be(2025);
        report.OrganizationName.Should().Be("Freiwilligenzentrum Innsbruck-Land");

        // 1. Freiwillige (304, +64)
        report.TotalVolunteers.CurrentValue.Should().Be(304);
        report.TotalVolunteers.PreviousYearValue.Should().Be(240);
        report.TotalVolunteers.Growth.Should().Be(64);
        report.TotalVolunteers.FormattedGrowth.Should().Be("+64 (2025)");

        // 2. Personen im Freiwilligenpool (77, +21)
        report.VolunteerPool.CurrentValue.Should().Be(77);
        report.VolunteerPool.PreviousYearValue.Should().Be(56);
        report.VolunteerPool.Growth.Should().Be(21);
        report.VolunteerPool.FormattedGrowth.Should().Be("+21 (2025)");

        // 3. Vernetzungspartner:innen (117, +17)
        report.NetworkPartners.CurrentValue.Should().Be(117);
        report.NetworkPartners.PreviousYearValue.Should().Be(100);
        report.NetworkPartners.Growth.Should().Be(17);
        report.NetworkPartners.FormattedGrowth.Should().Be("+17 (2025)");

        // 4. Vermittlungen (261, +57)
        report.Placements.CurrentValue.Should().Be(261);
        report.Placements.PreviousYearValue.Should().Be(204);
        report.Placements.Growth.Should().Be(57);
        report.Placements.FormattedGrowth.Should().Be("+57 (2025)");

        // 5. Versicherte (450, +62)
        report.InsuredPersons.CurrentValue.Should().Be(450);
        report.InsuredPersons.PreviousYearValue.Should().Be(388);
        report.InsuredPersons.Growth.Should().Be(62);
        report.InsuredPersons.FormattedGrowth.Should().Be("+62 (2025)");

        // 6. Veranstaltungen & Projekte (191, +63)
        report.EventsAndProjects.CurrentValue.Should().Be(191);
        report.EventsAndProjects.PreviousYearValue.Should().Be(128);
        report.EventsAndProjects.Growth.Should().Be(63);
        report.EventsAndProjects.FormattedGrowth.Should().Be("+63 (2025)");
    }

    [Fact]
    public async Task ExportAnnualReportPdfAsync_GeneratesValidPdfDocument()
    {
        using var db = CreateDbContext();
        var orgId = Guid.NewGuid();

        var profile = LegalEntityProfile.CreateEmpty();
        profile.UpdateEntity(
            "Freiwilligenzentrum Innsbruck-Land",
            LegalForm.Verein,
            "ZVR-123456789",
            "Dorfplatz 2, 6175 Kematen in Tirol",
            null,
            null, null, null);
        db.LegalEntityProfiles.Add(profile);
        await db.SaveChangesAsync();

        var service = new ReportingService(db);

        var result = await service.ExportAnnualReportPdfAsync(orgId, 2025);
        result.IsSuccess.Should().BeTrue();

        var bytes = result.Value;
        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterThan(200);

        var header = System.Text.Encoding.ASCII.GetString(bytes.Take(8).ToArray());
        header.Should().StartWith("%PDF-1.4");
    }

    [Fact]
    public async Task GetAnnualReportAsync_ForGenericOrganization_ComputesGrowthCorrectly()
    {
        using var db = CreateDbContext();
        var orgId = Guid.NewGuid();

        var service = new ReportingService(db);

        var result = await service.GetAnnualReportAsync(orgId, 2026);
        result.IsSuccess.Should().BeTrue();

        var report = result.Value;
        report.Year.Should().Be(2026);
        report.TotalVolunteers.FormattedGrowth.Should().Contain("(2026)");
        report.Placements.FormattedGrowth.Should().Contain("(2026)");
        report.InsuredPersons.FormattedGrowth.Should().Contain("(2026)");
    }
}
