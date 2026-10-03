using System.Reflection;
using FluentAssertions;
using Xunit;

namespace SeniorConnect.ArchitectureTests;

/// <summary>
/// ADR-028 project independence guard.
///
/// SeniorConnect is an independent, open-source project (ADR-028). No production
/// code, configuration, seed or client code may reintroduce a dependency on a
/// specific pilot/partner organization.
///
/// Scope is deliberately runtime/config/client artifacts only — `*.cs`, `*.json`
/// and Flutter `*.dart`/translations. Documentation (`*.md`) is intentionally NOT
/// scanned: historical references are allowed there and are not dependencies.
/// </summary>
public sealed class PilotIndependenceGuardTests
{
    private static readonly string[] ForbiddenMarkers =
    [
        "Freiwilligenzentrum Innsbruck", // the previously considered partner organization
        "FWZ Innsbruck",                 // its short name
        "fwz@regio-il.at",               // its support address
        "regio-il",                      // its domain
        "pilot.seniorconnect.at",        // the pilot-specific deployment host
    ];

    [Fact]
    public void NoProductionArtifact_DependsOnASpecificPilotOrganization()
    {
        var offenders = ProductionFiles()
            .Where(f => ForbiddenMarkers.Any(m =>
                File.ReadAllText(f).Contains(m, StringComparison.OrdinalIgnoreCase)))
            .Select(f => Path.GetRelativePath(RepoRoot(), f))
            .Distinct()
            .OrderBy(f => f)
            .ToList();

        offenders.Should().BeEmpty(
            "ADR-028 makes SeniorConnect independent: no production code, seed or "
            + "configuration may require a specific partner organization");
    }

    [Fact]
    public void Guard_DetectsAPlantedPartnerMarker()
    {
        // Meta-test: prove the marker predicate actually catches a reintroduction.
        const string planted = "// contact: fwz@regio-il.at";
        ForbiddenMarkers
            .Any(m => planted.Contains(m, StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue("the guard must catch a reintroduced partner dependency");
    }

    private static IEnumerable<string> ProductionFiles()
    {
        var separator = Path.DirectorySeparatorChar;
        var backend = Path.Combine(RepoRoot(), "backend");
        var mobile = Path.Combine(RepoRoot(), "mobile", "senior_connect");

        var backendFiles = Enumerate(backend)
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

        var mobileFiles = Enumerate(Path.Combine(mobile, "lib"))
            .Where(f => f.EndsWith(".dart", StringComparison.OrdinalIgnoreCase))
            .Concat(Enumerate(Path.Combine(mobile, "assets", "translations"))
                .Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)));

        return backendFiles.Concat(mobileFiles).Where(f => !IsGenerated(f, separator));
    }

    private static IEnumerable<string> Enumerate(string directory) =>
        Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            : [];

    private static bool IsGenerated(string file, char separator)
    {
        string[] generated = ["obj", "bin", "publish", ".vs", ".git", "build", "dart_tool"];
        var segments = file.Split(separator, StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(s => generated.Contains(s, StringComparer.OrdinalIgnoreCase));
    }

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
