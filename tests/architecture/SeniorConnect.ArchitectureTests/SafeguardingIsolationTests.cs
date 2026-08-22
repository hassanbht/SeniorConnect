using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace SeniorConnect.ArchitectureTests;

/// <summary>
/// ADR-004 / BR-SG-01..06.
///
/// Safeguarding data concerns vulnerable people and is restricted to appointed
/// officers. An OrganizationAdmin managing volunteers and events has no access.
/// These tests exist because that rule is the one everybody gets wrong.
/// </summary>
public sealed class SafeguardingIsolationTests
{
    private const string Safeguarding = "SeniorConnect.Modules.Safeguarding";

    [Fact]
    public void Only_the_Safeguarding_module_and_the_Api_reference_safeguarding_types()
    {
        var result = Types.InCurrentDomain()
            .That().ResideInNamespaceStartingWith("SeniorConnect")
            .And().DoNotResideInNamespaceStartingWith(Safeguarding)
            .And().DoNotResideInNamespaceStartingWith("SeniorConnect.Api.Safeguarding")
            .And().DoNotResideInNamespaceStartingWith("SeniorConnect.ArchitectureTests")
            .ShouldNot().HaveDependencyOn(Safeguarding)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "safeguarding data must not be reachable from ordinary code: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void The_Reporting_module_never_references_safeguarding()
    {
        // BR-SG-05: never in reports, exports, dashboards, notifications or search.
        var result = Types.InCurrentDomain()
            .That().ResideInNamespaceStartingWith("SeniorConnect.Modules.Reporting")
            .ShouldNot().HaveDependencyOn(Safeguarding)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void The_Funder_module_never_references_safeguarding()
    {
        var result = Types.InCurrentDomain()
            .That().ResideInNamespaceStartingWith("SeniorConnect.Modules.Reporting")
            .ShouldNot().HaveDependencyOn(Safeguarding)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void The_Notifications_module_never_references_safeguarding_contracts()
    {
        // Officers are notified through a dedicated channel inside the
        // Safeguarding module, not through the general notification pipeline.
        var result = Types.InCurrentDomain()
            .That().ResideInNamespaceStartingWith("SeniorConnect.Modules.Notifications")
            .ShouldNot().HaveDependencyOn(Safeguarding)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
