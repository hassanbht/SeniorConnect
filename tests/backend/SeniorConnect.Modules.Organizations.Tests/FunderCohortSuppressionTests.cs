using FluentAssertions;
using SeniorConnect.Modules.Reporting.Domain;
using SeniorConnect.Modules.Reporting.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Organizations.Tests;

/// <summary>
/// P2-34 / BR-FUNDER-03: a cohort under 10 must suppress EVERY measure, not
/// just the headcount — otherwise TotalHours lets a funder re-derive who a
/// suppressed "&lt;10" cohort is by cross-referencing hours against other
/// data they hold.
/// </summary>
public sealed class FunderCohortSuppressionTests
{
    [Fact]
    public void ToSuppressedDto_MasksTotalHours_WhenCohortIsBelowTen()
    {
        var row = new FunderMonthlyReportView
        {
            FunderId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Month = new DateOnly(2026, 6, 1),
            CategoryCode = "accompaniment",
            ActivityCount = 3,
            DistinctVolunteers = 2,
            DistinctPeopleSupported = 1,
            Hours = 47.5,
            IsSuppressed = false
        };

        var dto = FunderService.ToSuppressedDto(row);

        dto.IsSuppressed.Should().BeTrue();
        dto.TotalHours.Should().Be("<10", "a suppressed cohort must hide hours too, not just headcounts");
        dto.DistinctVolunteers.Should().Be("<10");
        dto.DistinctPeopleSupported.Should().Be("<10");
    }

    [Fact]
    public void ToSuppressedDto_ShowsRealHours_WhenCohortIsTenOrMore()
    {
        var row = new FunderMonthlyReportView
        {
            FunderId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Month = new DateOnly(2026, 6, 1),
            CategoryCode = "accompaniment",
            ActivityCount = 40,
            DistinctVolunteers = 15,
            DistinctPeopleSupported = 12,
            Hours = 120.2,
            IsSuppressed = false
        };

        var dto = FunderService.ToSuppressedDto(row);

        dto.IsSuppressed.Should().BeFalse();
        dto.TotalHours.Should().Be("120.2");
    }
}
