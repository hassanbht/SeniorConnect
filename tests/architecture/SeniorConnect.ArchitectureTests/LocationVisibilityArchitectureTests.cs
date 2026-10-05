using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using SeniorConnect.Modules.Geography.Application;
using Xunit;

namespace SeniorConnect.ArchitectureTests;

/// <summary>
/// Phase 3 (docs/security/location-consent-and-visibility.md §6): every
/// response type that can carry coordinates is a reviewed, purpose-specific
/// DTO; the geocoder has no fake implementation; HTTP client logging (which
/// would print geocoder URLs with the address and key) stays off.
/// </summary>
public sealed class LocationVisibilityArchitectureTests
{
    /// <summary>
    /// Adding a type here is a privacy review: say whose location it is and
    /// who decides what it shows.
    /// </summary>
    private static readonly Dictionary<string, string> ReviewedCoordinateCarriers = new()
    {
        ["HelpRequestDto"] = "generic request: capped at APPROXIMATE by LocationVisibilityPolicy (MapRequest)",
        ["HelpRequestLocationDto"] = "the only exact route for a request: LocationVisibilityService decides",
        ["ProximityResult"] = "people/requests: always null; organizations: public venue",
        ["CommunityGroupDto"] = "personal location: APPROXIMATE grid cell via the policy",
        ["SupportProfileDto"] = "own profile only (/me)",
        ["VolunteerProfileDto"] = "own profile only (/me)",
        ["GeocodeResult"] = "own address lookup, internal to the geocode endpoint",
        ["GeocodeAddressResponse"] = "own address lookup (/reference/geocode-address)",
        ["GemeindeDto"] = "public municipality centroid",
        ["NearbyTownDto"] = "public municipality centroid",
        ["BranchDto"] = "public venue of an organization branch",
        ["NearbyHelpRequestRow"] = "internal module contract, never serialized to a client",
    };

    [Fact]
    public void EveryTypeCarryingCoordinates_IsAReviewedPurposeSpecificDto()
    {
        var carriers = ProductionTypes()
            .Where(t => t.IsPublic && !t.Name.EndsWith("Request", StringComparison.Ordinal) && !t.Name.EndsWith("Command", StringComparison.Ordinal))
            .Where(t => !typeof(Domain.Entity).IsAssignableFrom(t)) // entities are never serialized directly
            .Where(t => t.GetProperties().Any(p => p.Name is "Latitude" or "Longitude"))
            .Select(t => t.Name)
            .Distinct()
            .ToList();

        carriers.Except(ReviewedCoordinateCarriers.Keys).Should().BeEmpty(
            "a new response type with coordinates needs a privacy review and an entry in ReviewedCoordinateCarriers");
    }

    /// <summary>
    /// Phase 4 (docs/security/location-consent-and-visibility.md §13): every
    /// response or contract type carrying a location, address, postal code,
    /// municipality or distance field is classified. A new one fails here
    /// until someone decides which class it is.
    /// </summary>
    private static readonly Dictionary<string, string> LocationFieldClassification = new()
    {
        ["BranchDto"] = "PUBLIC: organization branch venue",
        ["GemeindeDto"] = "PUBLIC: municipality centroid and postal code",
        ["NearbyTownDto"] = "PUBLIC: municipality centroid, distance from the caller's own point",
        ["ReferralProviderDto"] = "PUBLIC: professional service address",
        ["LegalEntityProfileDto"] = "PUBLIC: the organization's registered address",
        ["CommunityEventDiscoveryResult"] = "APPROXIMATE: postal-code centroid distance, no address",
        ["CommunityEventDiscoveryRow"] = "PRIVATE_INTERNAL: module contract, postal code only",
        ["CommunityEventDto"] = "APPROXIMATE: address only for public venues, managers and confirmed attendees",
        ["MyScheduleItemDto"] = "APPROXIMATE: address only for public venues and confirmed attendees",
        ["CommunityGroupDto"] = "APPROXIMATE: ~1 km grid cell",
        ["HelpRequestDto"] = "APPROXIMATE: grid cell at most; address only to the live assignee (ADR-026)",
        ["HelpRequestLocationDto"] = "PRECISE_AUTHORIZED: LocationVisibilityService decides",
        ["ProximityResult"] = "DISTANCE_ONLY: people and requests; PUBLIC for organizations",
        ["VolunteerFeedItem"] = "DISTANCE_ONLY: band; area only with the senior's consent",
        ["MatchingCandidate"] = "DISTANCE_ONLY: band from grid cells",
        ["ScoreBreakdown"] = "DISTANCE_ONLY: a 0..1 score computed from grid cells",
        ["MatchingConfig"] = "PRIVATE_INTERNAL: scoring weights, no location",
        ["NearbyHelpRequestRow"] = "PRIVATE_INTERNAL: module contract, never serialized",
        ["VerificationFacts"] = "PRIVATE_INTERNAL: a boolean 'address verified', no address",
        ["GeocodeResult"] = "OWN_DATA: the caller's own address lookup",
        ["GeocodeAddressResponse"] = "OWN_DATA: the caller's own address lookup",
        ["SupportProfileDto"] = "OWN_DATA: /me only",
        ["VolunteerProfileDto"] = "OWN_DATA: /me only",
    };

    private static bool IsLocationField(string name) =>
        name is "Latitude" or "Longitude"
        || (name.Contains("Address", StringComparison.Ordinal) && !name.Contains("Email", StringComparison.Ordinal) && !name.StartsWith("Ip", StringComparison.Ordinal))
        || name.Contains("PostalCode", StringComparison.Ordinal)
        || name.Contains("Gemeinde", StringComparison.Ordinal)
        || name.Contains("Municipality", StringComparison.Ordinal)
        || name.Contains("Distance", StringComparison.Ordinal);

    [Fact]
    public void EveryTypeWithALocationField_IsClassified()
    {
        var allowed = new[] { "PUBLIC", "APPROXIMATE", "DISTANCE_ONLY", "PRECISE_AUTHORIZED", "PRIVATE_INTERNAL", "OWN_DATA" };
        var carriers = ProductionTypes()
            .Where(t => t.IsPublic && !t.IsEnum && !t.IsInterface && !t.IsAbstract)
            .Where(t => !t.Name.EndsWith("Request", StringComparison.Ordinal) && !t.Name.EndsWith("Command", StringComparison.Ordinal))
            .Where(t => !typeof(Domain.Entity).IsAssignableFrom(t))
            .Where(t => t.Namespace is { } ns && (ns.EndsWith(".Application", StringComparison.Ordinal) || ns.EndsWith(".Contracts", StringComparison.Ordinal) || ns.EndsWith(".Domain", StringComparison.Ordinal) || ns.Contains(".Api", StringComparison.Ordinal)))
            .Where(t => t.GetProperties().Any(p => IsLocationField(p.Name)))
            .Select(t => t.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        var unclassified = carriers.Except(LocationFieldClassification.Keys).ToList();
        unclassified.Should().BeEmpty(
            "classify each new location-bearing type ({0}) as one of {1}",
            string.Join(", ", unclassified), string.Join(", ", allowed));
        LocationFieldClassification.Values.Select(v => v.Split(':')[0]).Should().OnlyContain(v => allowed.Contains(v));
    }

    /// <summary>
    /// Phase 4: the eligibility, restriction and consent checks are optional
    /// constructor parameters (legacy unit tests run without them). Production
    /// must register every one, or DI would silently skip the check.
    /// </summary>
    [Fact]
    public void Phase4PolicyServices_AreRegistered()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        // A non-Development registration requires a real pepper; supply a test-only one.
        var pepperConfiguration = new TestConfigurationSource(
            new Dictionary<string, string?>
            {
                ["IDENTITY_HASH_PEPPER"] = "architecture_test_pepper_value_not_a_real_secret",
            });
        SeniorConnect.Modules.Identity.Infrastructure.IdentityModuleExtensions.AddIdentityModule(
            services, isDevelopment: false, configuration: pepperConfiguration);
        SeniorConnect.Modules.Profiles.Infrastructure.ProfilesModuleExtensions.AddProfilesModule(services);
        SeniorConnect.Modules.TrustSafety.Infrastructure.TrustSafetyModuleExtensions.AddTrustSafetyModule(services);
        var registered = services.Select(d => d.ServiceType).ToHashSet();

        registered.Should().Contain([
            typeof(SeniorConnect.Modules.Profiles.Contracts.IDiscoveryEligibilityService),
            typeof(SeniorConnect.Modules.Profiles.Contracts.ILocationVisibilityService),
            typeof(SeniorConnect.Modules.TrustSafety.Contracts.ISafeguardingRestrictionReader),
            typeof(SeniorConnect.Modules.TrustSafety.Contracts.ISafetyBoundaryReader),
            typeof(SeniorConnect.Modules.Identity.Contracts.IVerificationFactsReader),
            typeof(SeniorConnect.Modules.Identity.Contracts.IAddressVerificationInvalidator),
        ]);
    }

    [Fact]
    public void TheGeocoder_HasOnlyTheRealAndTheTruthfulUnconfiguredImplementation()
    {
        var implementations = ProductionTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IGeocodingProvider).IsAssignableFrom(t))
            .Select(t => t.Name);

        implementations.Should().BeEquivalentTo("NominatimGeocodingProvider", "UnconfiguredGeocodingProvider");
    }

    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Production.json")]
    public void HttpClientRequestLogging_IsOff_AndNoGeocoderSecretIsCommitted(string fileName)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "backend", "SeniorConnect.Api", fileName)));
        var root = doc.RootElement;

        root.GetProperty("Logging").GetProperty("LogLevel").GetProperty("System.Net.Http.HttpClient").GetString()
            .Should().BeOneOf("Warning", "Error", "Critical", "None");

        var geocoding = root.GetProperty("Geocoding");
        geocoding.GetProperty("Provider").GetString().Should().Be("None", "a provider is enabled per environment, not in the committed file");
        geocoding.TryGetProperty("ApiKey", out _).Should().BeFalse("the key comes from Geocoding__ApiKey in the environment");
    }

    private static IEnumerable<Type> ProductionTypes()
    {
        var binDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        foreach (var dll in Directory.EnumerateFiles(binDir, "SeniorConnect.*.dll"))
        {
            var name = Path.GetFileNameWithoutExtension(dll);
            if (name.Contains("Tests", StringComparison.Ordinal)) continue;

            Type[] types;
            try
            {
                types = Assembly.Load(name).GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t is not null).ToArray()!;
            }

            foreach (var type in types) yield return type;
        }
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

    /// <summary>
    /// Minimal in-memory IConfiguration for the registration-only test above,
    /// avoiding a Microsoft.Extensions.Configuration.Binder dependency.
    /// </summary>
    private sealed class TestConfigurationSource(Dictionary<string, string?> values)
        : Microsoft.Extensions.Configuration.IConfiguration
    {
        public string? this[string key]
        {
            get => values.TryGetValue(key, out var value) ? value : null;
            set => values[key] = value;
        }

        public Microsoft.Extensions.Configuration.IConfigurationSection GetSection(string key) => null!;

        public System.Collections.Generic.IEnumerable<Microsoft.Extensions.Configuration.IConfigurationSection> GetChildren() =>
            Array.Empty<Microsoft.Extensions.Configuration.IConfigurationSection>();

        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() =>
            new Microsoft.Extensions.Primitives.CancellationChangeToken(CancellationToken.None);

        public string[] GetChildKeys(
            Microsoft.Extensions.Configuration.IConfigurationBuilder builder,
            Action<string>? keyFilter) => values.Keys.ToArray();

        public Microsoft.Extensions.Primitives.IChangeToken OnChange(Action<object, string?> listener) =>
            GetReloadToken();
    }
}
