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
        ["Identity", "Profiles", "Organizations", "TrustSafety", "HelpRequests",
         "Reporting", "Community", "Matching", "Family", "Notifications"];

    [Fact]
    public void A_module_may_reference_only_another_modules_Contracts()
    {
        // Types.InCurrentDomain() only sees assemblies that are already
        // loaded; load every module so the rule never passes vacuously.
        foreach (var module in Modules)
        {
            System.Reflection.Assembly.Load($"{Root}.Modules.{module}");
        }

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

        failures.RemoveAll(f => KnownViolations.Any(known => f.StartsWith(known, StringComparison.Ordinal)));

        failures.Should().BeEmpty(
            "cross-module access goes through Contracts, or the monolith stops "
            + "being modular and becomes a big ball of mud with folders. All violations: {0}",
            string.Join(" | ", failures));
    }

    /// <summary>
    /// Pre-existing violations surfaced (not introduced) when the rule stopped
    /// passing vacuously — before, the Matching assembly was never loaded, so
    /// it was never checked. Each entry is tracked debt, not a permission:
    /// shrink this list, never grow it.
    ///
    /// Matching (P3 era) reads help requests, volunteer profiles and the
    /// notification service through the other modules' DbContexts and
    /// Application interfaces instead of their Contracts. Fix: Contracts
    /// readers/ports for matching (docs/security/hotfix-matching-location.md,
    /// "Residual risk").
    /// </summary>
    private const string MatchingService = "SeniorConnect.Modules.Matching.Infrastructure.MatchingService";

    private static readonly string[] KnownViolations =
    [
        $"Matching -> HelpRequests.Domain: {MatchingService}",
        $"Matching -> HelpRequests.Application: {MatchingService}",
        $"Matching -> Profiles.Domain: {MatchingService}",
        $"Matching -> Profiles.Application: {MatchingService}",
        $"Matching -> Notifications.Application: {MatchingService}",
    ];

    [Fact]
    public void Known_violations_are_still_real_so_the_list_only_shrinks()
    {
        foreach (var module in Modules)
        {
            System.Reflection.Assembly.Load($"{Root}.Modules.{module}");
        }

        var stillViolating = Types.InCurrentDomain()
            .That().ResideInNamespaceStartingWith($"{Root}.Modules.Matching")
            .ShouldNot().HaveDependencyOn($"{Root}.Modules.Profiles.Domain")
            .GetResult();

        stillViolating.IsSuccessful.Should().BeFalse(
            "the Matching -> Profiles.Domain debt is fixed — delete it from KnownViolations");
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
        // Same vacuity trap as above: load the Api assembly and every module,
        // or the rule only checks whatever another test happened to load.
        System.Reflection.Assembly.Load($"{Root}.Api");
        foreach (var module in Modules)
        {
            System.Reflection.Assembly.Load($"{Root}.Modules.{module}");
        }

        var entityNamespaces = Modules
            .Select(m => $"{Root}.Modules.{m}.Domain")
            .ToArray();

        var result = Types.InCurrentDomain()
            .That().ResideInNamespaceStartingWith($"{Root}.Api")
            .Or().ResideInNamespaceMatching($@"{Root}\.Modules\.\w+\.Contracts")
            .ShouldNot().HaveDependencyOnAny(entityNamespaces)
            .GetResult();

        var failing = (result.FailingTypeNames ?? []).Except(KnownApiDomainLeaks).ToList();
        failing.Should().BeEmpty(
            "endpoints and contracts use DTOs, never EF entities: {0}",
            string.Join(", ", failing));
    }

    /// <summary>
    /// Pre-existing (present at HEAD before Phase 3), surfaced when the rule
    /// above stopped depending on test order. These endpoints reference module
    /// Domain types (mostly enums such as roles and statuses) directly. Tracked
    /// debt: shrink, never grow.
    /// </summary>
    private static readonly string[] KnownApiDomainLeaks =
    [
        "SeniorConnect.Api.Endpoints.CommunityEndpoints",
        "SeniorConnect.Api.Endpoints.BulkEntryItem",
        "SeniorConnect.Api.Endpoints.CoordinatorEndpoints",
        "SeniorConnect.Api.Endpoints.MatchingEndpoints",
        "SeniorConnect.Api.Endpoints.PrivacyEndpoints",
    ];
}
