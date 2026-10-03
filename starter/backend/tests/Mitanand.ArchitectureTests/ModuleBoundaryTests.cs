using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace SeniorConnect.ArchitectureTests;

/// <summary>
/// The modular monolith survives only if the boundaries are mechanically
/// enforced (ADR-001). Discipline decays; tests do not.
/// </summary>
public sealed class ModuleBoundaryTests
{
    private const string Root = "SeniorConnect";
    private static readonly string[] Modules =
        ["Activities", "Funder", "Identity", "Profiles", "Community",
         "Help", "Matching", "TrustSafety", "Safeguarding", "Organizations",
         "Notifications", "Reporting", "Audit"];

    [Fact]
    public void A_module_may_reference_only_another_modules_Contracts()
    {
        var failures = new List<string>();

        foreach (var module in Modules)
        {
            foreach (var other in Modules.Where(m => m != module))
            {
                foreach (var forbidden in new[] { "Domain", "Application", "Infrastructure" })
                {
                    var result = Types.InCurrentDomain()
                        .That().ResideInNamespaceStartingWith($"{Root}.Modules.{module}")
                        .ShouldNot()
                        .HaveDependencyOn($"{Root}.Modules.{other}.{forbidden}")
                        .GetResult();

                    if (!result.IsSuccessful)
                    {
                        failures.Add(
                            $"{module} -> {other}.{forbidden}: "
                            + string.Join(", ", result.FailingTypeNames ?? []));
                    }
                }
            }
        }

        failures.Should().BeEmpty(
            "cross-module access goes through Contracts, or the monolith stops "
            + "being modular and becomes a big ball of mud with folders");
    }

    [Fact]
    public void Domain_layers_do_not_reference_EF_Core()
    {
        var result = Types.InCurrentDomain()
            .That().ResideInNamespaceMatching($@"{Root}\.Modules\.\w+\.Domain")
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "the domain layer holds invariants, not persistence: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Domain_layers_do_not_reference_ASP_NET()
    {
        var result = Types.InCurrentDomain()
            .That().ResideInNamespaceMatching($@"{Root}\.Modules\.\w+\.Domain")
            .ShouldNot().HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Contracts_do_not_reference_Domain()
    {
        var result = Types.InCurrentDomain()
            .That().ResideInNamespaceMatching($@"{Root}\.Modules\.\w+\.Contracts")
            .ShouldNot().HaveDependencyOnAny(
                Modules.Select(m => $"{Root}.Modules.{m}.Domain").ToArray())
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "a Contracts type that exposes a Domain type leaks the whole "
            + "aggregate across the boundary: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void No_domain_entity_escapes_through_a_Contracts_or_Api_type()
    {
        var entityNamespaces = Modules
            .Select(m => $"{Root}.Modules.{m}.Domain")
            .ToArray();

        var result = Types.InCurrentDomain()
            .That().ResideInNamespaceStartingWith($"{Root}.Api")
            .Or().ResideInNamespaceMatching($@"{Root}\.Modules\.\w+\.Contracts")
            .ShouldNot().HaveDependencyOnAny(entityNamespaces)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "endpoints and contracts use DTOs, never EF entities: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }
}
