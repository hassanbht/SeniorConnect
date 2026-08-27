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
    private static readonly string[] NonSafeguardingModules =
    [
        "SeniorConnect.Modules.Reporting",
        "SeniorConnect.Modules.Notifications",
        "SeniorConnect.Modules.Community",
        "SeniorConnect.Modules.Family",
        "SeniorConnect.Modules.HelpRequests",
        "SeniorConnect.Modules.Identity",
        "SeniorConnect.Modules.Matching",
        "SeniorConnect.Modules.Organizations",
        "SeniorConnect.Modules.Profiles"
    ];

    private static readonly string[] SafeguardingTypes =
    [
        "SeniorConnect.Modules.TrustSafety.Domain.SafeguardingCase",
        "SeniorConnect.Modules.TrustSafety.Domain.SafeguardingCaseNote",
        "SeniorConnect.Modules.TrustSafety.Domain.SafeguardingAccessLog",
        "SeniorConnect.Modules.TrustSafety.Application.ISafeguardingDbContext",
        "SeniorConnect.Modules.TrustSafety.Application.ISafeguardingService"
    ];

    [Fact]
    public void Other_modules_never_reference_safeguarding_types()
    {
        foreach (var moduleNamespace in NonSafeguardingModules)
        {
            var result = Types.InCurrentDomain()
                .That().ResideInNamespaceStartingWith(moduleNamespace)
                .ShouldNot().HaveDependencyOnAny(SafeguardingTypes)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                "module {0} must never access safeguarding entities or services: {1}",
                moduleNamespace,
                string.Join(", ", result.FailingTypeNames ?? []));
        }
    }

    [Fact]
    public void The_Reporting_module_never_references_safeguarding()
    {
        // BR-SG-05: never in reports, exports, dashboards, notifications or search.
        var result = Types.InCurrentDomain()
            .That().ResideInNamespaceStartingWith("SeniorConnect.Modules.Reporting")
            .ShouldNot().HaveDependencyOnAny(SafeguardingTypes)
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
            .ShouldNot().HaveDependencyOnAny(SafeguardingTypes)
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
