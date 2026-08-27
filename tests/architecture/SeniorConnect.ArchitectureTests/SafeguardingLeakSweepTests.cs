using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace SeniorConnect.ArchitectureTests;

/// <summary>
/// P4-13 / BR-SG-05: Automated Safeguarding Leak Sweep.
/// Safeguarding data concerns vulnerable people and must never appear in
/// exports, reports, dashboards, general search, public notifications or funder surface.
/// </summary>
public sealed class SafeguardingLeakSweepTests
{
    private static readonly string[] SafeguardingDomainTypes =
    [
        "SeniorConnect.Modules.TrustSafety.Domain.SafeguardingCase",
        "SeniorConnect.Modules.TrustSafety.Domain.SafeguardingCaseNote",
        "SeniorConnect.Modules.TrustSafety.Domain.SafeguardingAccessLog",
        "SeniorConnect.Modules.TrustSafety.Application.ISafeguardingDbContext",
        "SeniorConnect.Modules.TrustSafety.Application.ISafeguardingService",
        "SeniorConnect.Modules.TrustSafety.Application.SafeguardingCaseDto",
        "SeniorConnect.Modules.TrustSafety.Application.SafeguardingCaseNoteDto"
    ];

    [Theory]
    [InlineData("SeniorConnect.Modules.Reporting")]
    [InlineData("SeniorConnect.Modules.Notifications")]
    [InlineData("SeniorConnect.Modules.Community")]
    [InlineData("SeniorConnect.Modules.Family")]
    [InlineData("SeniorConnect.Modules.Matching")]
    [InlineData("SeniorConnect.Modules.Organizations")]
    [InlineData("SeniorConnect.Modules.Profiles")]
    public void Module_has_zero_safeguarding_leakage(string moduleNamespace)
    {
        var result = Types.InCurrentDomain()
            .That().ResideInNamespaceStartingWith(moduleNamespace)
            .ShouldNot().HaveDependencyOnAny(SafeguardingDomainTypes)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "module {0} must have zero reference to safeguarding records or services: {1}",
            moduleNamespace,
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Safeguarding_is_excluded_from_funder_surface()
    {
        var result = Types.InCurrentDomain()
            .That().ResideInNamespaceStartingWith("SeniorConnect.Api.Endpoints.Funder")
            .Or().ResideInNamespaceStartingWith("SeniorConnect.Modules.Reporting.Application")
            .ShouldNot().HaveDependencyOnAny(SafeguardingDomainTypes)
            .GetResult();

        result.IsSuccessful.Should().BeTrue("funder surface must not expose safeguarding data.");
    }
}
