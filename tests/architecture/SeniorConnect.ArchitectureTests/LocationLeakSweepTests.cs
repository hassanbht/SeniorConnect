using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using SeniorConnect.Modules.Geography.Application;
using SeniorConnect.Modules.Matching.Application;
using SeniorConnect.Modules.Matching.Domain;
using Xunit;

namespace SeniorConnect.ArchitectureTests;

/// <summary>
/// Security hotfix sweep (docs/security/hotfix-matching-location.md):
/// no client-chosen discovery origin, no exact coordinates or km distances
/// in cross-user matching/discovery contracts, and no coordinates in logs.
/// </summary>
public sealed class LocationLeakSweepTests
{
    private static readonly string[] CoordinateNames = ["latitude", "longitude", "lat", "lon", "lng"];

    private static readonly Regex LogCall = new(@"\bLog(Trace|Debug|Information|Warning|Error|Critical)\s*\(", RegexOptions.Compiled);
    private static readonly Regex StringLiteral = new("\"(?:[^\"\\\\]|\\\\.)*\"", RegexOptions.Compiled);
    private static readonly Regex SensitiveIdentifier = new(@"(?i)\b(latitude|longitude|lat|lon|lng|address\w*)\b", RegexOptions.Compiled);

    /// <summary>
    /// Wave-1: the public notification dispatch endpoint was removed because it
    /// let ANY authenticated caller send any notification to ANY user, including
    /// CriticalSafety priority (which bypasses mute and quiet hours). Dispatch is
    /// an internal operation now; nothing may map it again.
    /// </summary>
    [Fact]
    public void No_endpoint_lets_a_caller_dispatch_arbitrary_notifications()
    {
        var endpointSource = File.ReadAllText(Path.Combine(
            RepoRoot(), "backend", "SeniorConnect.Api", "Endpoints", "NotificationEndpoints.cs"));

        endpointSource.Should().NotContain("DispatchNotificationAsync",
            "a client-supplied RecipientUserId/Category/Priority is an impersonation vector");
        endpointSource.Should().NotContain("\"/dispatch\"",
            "notification dispatch must not be reachable from any authenticated caller");
    }

    /// <summary>
    /// Wave-2 (GDPR Art. 32): no endpoint may persist a raw IP address. The
    /// consent endpoint once wrote RemoteIpAddress straight into consents.ip_hash,
    /// which the column contract and every other call site forbid.
    /// </summary>
    [Fact]
    public void No_endpoint_stores_a_raw_ip_address()
    {
        var endpointDirectory = Path.Combine(RepoRoot(), "backend", "SeniorConnect.Api", "Endpoints");
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(endpointDirectory, "*.cs"))
        {
            var source = File.ReadAllText(file);

            // A raw RemoteIpAddress passed to a service (as opposed to read and
            // immediately hashed through IIdentityHashingService.HashIp) is the defect.
            if (source.Contains("RemoteIpAddress", StringComparison.Ordinal)
                && !source.Contains("HashIp", StringComparison.Ordinal))
            {
                offenders.Add(Path.GetFileName(file));
            }
        }

        offenders.Should().BeEmpty(
            "an IP address is a personal datum; it may only be stored as a hash (BR-TRUST-05)");
    }

    [Theory]
    [InlineData(typeof(IProximityService), nameof(IProximityService.FindNearbyVolunteersAsync))]
    [InlineData(typeof(IProximityService), nameof(IProximityService.FindNearbyRequestsAsync))]
    [InlineData(typeof(IMatchingService), nameof(IMatchingService.GetVolunteerFeedAsync))]
    [InlineData(typeof(IMatchingService), nameof(IMatchingService.GetCandidatesForCallerAsync))]
    [InlineData(typeof(IMatchingService), nameof(IMatchingService.GetHybridProposalsForCallerAsync))]
    public void PeopleAndRequestSearches_AcceptNoClientSuppliedOrigin(Type contract, string method)
    {
        var parameters = contract.GetMethod(method)!.GetParameters().Select(p => p.Name!.ToLowerInvariant());

        parameters.Should().NotIntersectWith(CoordinateNames,
            "the origin must come from the caller's stored location, never from the client");
    }

    [Fact]
    public void MatchingViewsOfOtherPeople_CarryBandsNotKilometres()
    {
        foreach (var type in new[] { typeof(MatchingCandidate), typeof(VolunteerFeedItem), typeof(HybridMatchingProposal) })
        {
            var names = type.GetProperties().Select(p => p.Name).ToList();
            names.Should().NotContain(["DistanceKm", "Latitude", "Longitude"], $"{type.Name} is shown to other users");
        }

        typeof(MatchingCandidate).GetProperty(nameof(MatchingCandidate.DistanceBand))!.PropertyType.Should().Be<string>();
    }

    [Fact]
    public void ProximityResultCoordinates_AreOptional_SoPeopleResultsCanOmitThem()
    {
        foreach (var name in new[] { nameof(ProximityResult.Latitude), nameof(ProximityResult.Longitude), nameof(ProximityResult.DistanceKm) })
        {
            typeof(ProximityResult).GetProperty(name)!.PropertyType.Should().Be<double?>();
        }
    }

    [Fact]
    public void NoLogStatement_WritesCoordinatesOrAddresses()
    {
        // Message text may mention the word "address"; what must never appear
        // is a coordinate/address VALUE passed to the logger.
        var offenders = SourceFiles()
            .SelectMany(file => File.ReadLines(file).Select((line, i) => (file, line, number: i + 1)))
            .Where(x => LogCall.IsMatch(x.line)
                     && SensitiveIdentifier.IsMatch(StringLiteral.Replace(x.line[LogCall.Match(x.line).Index..], "\"\"")))
            .Select(x => $"{Path.GetFileName(x.file)}:{x.number}: {x.line.Trim()}")
            .ToList();

        offenders.Should().BeEmpty("private coordinates and addresses must never reach a log");
    }

    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Production.json")]
    public void AspNetCoreRequestLogging_WithQueryStrings_IsOff(string fileName)
    {
        // "Request starting GET /api/v1/discovery/...?latitude=..." is written by
        // Microsoft.AspNetCore.Hosting at Information. Keeping the category at
        // Warning or above means no request URL (and no query-string
        // coordinate) is logged.
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "backend", "SeniorConnect.Api", fileName)));
        var level = doc.RootElement.GetProperty("Logging").GetProperty("LogLevel").GetProperty("Microsoft.AspNetCore").GetString();

        level.Should().BeOneOf("Warning", "Error", "Critical", "None");
    }

    private static IEnumerable<string> SourceFiles()
    {
        var separator = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(Path.Combine(RepoRoot(), "backend"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{separator}obj{separator}")
                     && !f.Contains($"{separator}bin{separator}")
                     && !f.Contains($"{separator}Migrations{separator}"));
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
