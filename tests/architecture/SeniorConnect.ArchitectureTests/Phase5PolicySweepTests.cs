using System.Reflection;
using FluentAssertions;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Contracts;
using Xunit;

namespace SeniorConnect.ArchitectureTests;

/// <summary>
/// Phase 5B policy sweep (ADR-027, ADR-026). Guards against two kinds of drift:
///
/// 1. Merging the computed trust ladder with the verification-fact set
///    (BR-TRUST-01: identity, trust, capability stay separate concepts).
/// 2. Accidentally ACTIVATING the approved ADR-026 option C before the
///    Legal/Data Protection Gate has passed: the grant artifacts exist, but
///    the feature gate (`VisitAddress:RequireGrant`) must stay FALSE in the
///    committed configuration until legal validation.
/// </summary>
public sealed class Phase5PolicySweepTests
{
    private static readonly string[] TrustLadderNames =
        ["TrustLevel", "TrustScore", "SafetyLevel", "SafetyScore"];

    [Fact]
    public void VerificationFacts_DoNotCarryATrustOrSafetyLevel()
    {
        typeof(VerificationFacts).GetProperties().Select(p => p.Name)
            .Should().NotIntersectWith(TrustLadderNames,
                "the verified-facts contract must not carry the numeric trust or safety ladder (BR-TRUST-01)");

        typeof(TrustLevelEvaluation).GetProperties().Select(p => p.Name)
            .Should().NotContain("VerificationLevel",
                "the computed trust ladder must never become the discovery verification ladder (ADR-027)");
    }

    [Fact]
    public void VisitAddressGrant_FeatureGate_StartsOffInEveryCommittedConfiguration()
    {
        // ADR-026 option C is approved as a FEATURE, but production activation
        // is gated on the Legal/Data Protection review. The committed
        // configuration must therefore ship with RequireGrant = false; only an
        // explicit operational change may turn it on.
        var backend = Path.Combine(RepoRoot(), "backend");

        var offenders = Directory.EnumerateFiles(backend, "appsettings*.json", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => RequiresGrant(File.ReadAllText(f)))
            .Select(Path.GetFileName)
            .ToList();

        offenders.Should().BeEmpty(
            "ADR-026 option C awaits legal validation: VisitAddress:RequireGrant must be false in every committed appsettings file");
    }

    [Fact]
    public void VisitAddressGrant_StoresNoAddressData()
    {
        // The grant is a pure consent decision. Storing address material on it
        // would widen the data footprint beyond the approved ADR-026 scope.
        var grantType = typeof(SeniorConnect.Modules.HelpRequests.Domain.VisitAddressGrant);
        grantType.GetProperties().Select(p => p.Name)
            .Should().NotContain(name =>
                name.Contains("Address", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Latitude", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Longitude", StringComparison.OrdinalIgnoreCase),
            "the visit-address grant stores only the consent decision, never the address itself");
    }

    private static bool RequiresGrant(string json) =>
        json.Contains("\"VisitAddress\"", StringComparison.Ordinal)
        && json.Contains("\"RequireGrant\": true", StringComparison.Ordinal);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "backend", "SeniorConnect.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root (backend/SeniorConnect.slnx) not found.");
    }
}
