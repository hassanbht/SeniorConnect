using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SeniorConnect.Domain;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Geography.Application;
using SeniorConnect.Modules.Geography.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.HelpRequests.Tests;

/// <summary>
/// Phase 3 geocoder (docs/security/location-consent-and-visibility.md §7):
/// a real, configurable provider called only by the backend; truthful
/// failures; no fake coordinates; no address, coordinate or key in logs.
/// Tnn refers to the Phase 3 test list.
/// </summary>
public sealed class GeocodingProviderTests : IDisposable
{
    private const string PrivateAddress = "Maria-Theresien-Straße 7, 6020 Innsbruck";
    private const string ApiKey = "sk-test-DO-NOT-LEAK-4711";

    private const string InnsbruckHit = """
        [{"lat":"47.2680412","lon":"11.3931251","importance":0.61,
          "address":{"road":"Maria-Theresien-Straße","house_number":"7","postcode":"6020","city":"Innsbruck"}}]
        """;

    private readonly SeniorConnectDbContext _db;
    private readonly CapturingLogger _logs = new();

    public GeocodingProviderTests()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new SeniorConnectDbContext(options, new DefaultTenantContext { IsPlatformScope = true });
        _db.AustrianAdministrativeUnits.Add(Geography.Domain.AustrianAdministrativeUnit.Create(
            bundeslandCode: "7", bundeslandName: "Tirol",
            bezirkCode: "701", bezirkName: "Innsbruck",
            gemeindeCode: "70101", gemeindeName: "Innsbruck",
            postalCode: "6020", localityName: "Innsbruck", latitude: 47.2692, longitude: 11.4041));
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private static GeocodingOptions Options(bool requireKey = true, string? key = ApiKey, int retries = 1, int timeoutSeconds = 5) => new()
    {
        Provider = "Nominatim",
        BaseUrl = "https://geocoder.example.test",
        ApiKey = key,
        RequireApiKey = requireKey,
        TimeoutSeconds = timeoutSeconds,
        MaxRetries = retries,
        MinIntervalMilliseconds = 0,
    };

    private NominatimGeocodingProvider Provider(FakeHandler handler, GeocodingOptions? options = null) =>
        new(new HttpClient(handler), options ?? Options(), _db, _logs);

    [Fact]
    public async Task T16_ValidAddress_CallsTheConfiguredProvider_AndResolvesCodesFromOurSeed()
    {
        var handler = FakeHandler.Json(InnsbruckHit);

        var result = await Provider(handler).GeocodeAsync(PrivateAddress, "de");

        result.IsSuccess.Should().BeTrue();
        handler.Calls.Should().Be(1);
        var uri = handler.LastUri!;
        uri.Host.Should().Be("geocoder.example.test");
        uri.AbsolutePath.Should().Be("/search");
        uri.Query.Should().Contain("countrycodes=at").And.Contain("key=").And.Contain("q=");
        result.Value!.Latitude.Should().Be(47.2680412);
        result.Value.Longitude.Should().Be(11.3931251);
        result.Value.GemeindeCode.Should().Be("70101");
        result.Value.AddressLine.Should().Be("Maria-Theresien-Straße 7");
    }

    [Fact]
    public async Task T17_InvalidAddress_FailsTruthfully_WithoutCallingTheProvider()
    {
        var handler = FakeHandler.Json(InnsbruckHit);

        var result = await Provider(handler).GeocodeAsync("   ", "de");

        result.Error!.Code.Should().Be("GEOCODE_INVALID_ADDRESS");
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task T17_T21_UnknownAddress_IsNotFound_NeverAFallbackLocation()
    {
        var result = await Provider(FakeHandler.Json("[]")).GeocodeAsync("Nirgendwo 1", "de");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("GEOCODE_NOT_FOUND");
    }

    [Fact]
    public async Task T18_ProviderTimeout_FailsTruthfully()
    {
        var handler = new FakeHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var result = await Provider(handler, Options(retries: 0, timeoutSeconds: 1)).GeocodeAsync(PrivateAddress, "de");

        result.Error!.Code.Should().Be("GEOCODING_TIMEOUT");
        result.Error.Kind.Should().Be(ErrorKind.Unavailable);
    }

    [Fact]
    public async Task T19_ProviderUnavailable_IsRetriedThenFailsTruthfully()
    {
        var handler = FakeHandler.Status(HttpStatusCode.ServiceUnavailable);

        var result = await Provider(handler, Options(retries: 1)).GeocodeAsync(PrivateAddress, "de");

        result.Error!.Code.Should().Be("GEOCODING_UNAVAILABLE");
        handler.Calls.Should().Be(2, "one retry after a transient 5xx");
    }

    [Fact]
    public async Task T19_ProviderUnreachable_FailsTruthfully()
    {
        var handler = new FakeHandler((_, _) => throw new HttpRequestException($"Connection refused (geocoder.example.test/search?q={PrivateAddress}&key={ApiKey})"));

        var result = await Provider(handler, Options(retries: 0)).GeocodeAsync(PrivateAddress, "de");

        result.Error!.Code.Should().Be("GEOCODING_UNAVAILABLE");
        _logs.Text.Should().NotContain(ApiKey).And.NotContain("Maria-Theresien", "exception text echoing the URI is never logged");
    }

    [Fact]
    public async Task T20_MissingCredentials_IsAConfigurationError_AndNoCallIsMade()
    {
        var handler = FakeHandler.Json(InnsbruckHit);

        var result = await Provider(handler, Options(requireKey: true, key: null)).GeocodeAsync(PrivateAddress, "de");

        result.Error!.Code.Should().Be("GEOCODING_NOT_CONFIGURED");
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task T20_RejectedCredentials_AreAConfigurationError()
    {
        var result = await Provider(FakeHandler.Status(HttpStatusCode.Unauthorized)).GeocodeAsync(PrivateAddress, "de");

        result.Error!.Code.Should().Be("GEOCODING_NOT_CONFIGURED");
    }

    [Fact]
    public async Task T20_T21_NoProviderConfigured_EveryLookupFails_NoFakeProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<IGeographyDbContext>(_ => _db);
        services.AddGeographyModule(new ConfigurationBuilder().Build()); // no "Geocoding" section at all
        using var provider = services.BuildServiceProvider();

        var geocoder = provider.CreateScope().ServiceProvider.GetRequiredService<IGeocodingProvider>();
        var result = await geocoder.GeocodeAsync("Innsbruck", "de"); // the old stub answered this with fake coordinates

        geocoder.Should().BeOfType<UnconfiguredGeocodingProvider>();
        result.Error!.Code.Should().Be("GEOCODING_NOT_CONFIGURED");
        result.Error.Kind.Should().Be(ErrorKind.Unavailable);
    }

    [Fact]
    public void T20_AConfiguredProvider_IsTheRealOne()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IGeographyDbContext>(_ => _db);
        services.AddGeographyModule(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Geocoding:Provider"] = "Nominatim",
            ["Geocoding:BaseUrl"] = "https://geocoder.example.test",
            ["Geocoding:RequireApiKey"] = "true",
            ["Geocoding:ApiKey"] = ApiKey,
        }).Build());
        using var provider = services.BuildServiceProvider();

        provider.CreateScope().ServiceProvider.GetRequiredService<IGeocodingProvider>().Should().BeOfType<NominatimGeocodingProvider>();
    }

    [Theory]
    [InlineData("http://geocoder.example.test")] // not https: the address would travel in clear text
    [InlineData("not a url")]
    [InlineData(null)]
    public void T20_AnUnsafeOrMissingEndpoint_IsNotAUsableConfiguration(string? baseUrl)
    {
        var options = Options();
        options.BaseUrl = baseUrl;

        options.ConfigurationProblem().Should().NotBeNull();
    }

    [Fact]
    public async Task T21_T22_CoordinatesOutsideAustria_OrInverted_AreRejected()
    {
        const string inverted = """[{"lat":"11.3931251","lon":"47.2680412","address":{"postcode":"6020"}}]""";

        var result = await Provider(FakeHandler.Json(inverted)).GeocodeAsync(PrivateAddress, "de");

        result.Error!.Code.Should().Be("GEOCODE_NOT_FOUND");
    }

    [Fact]
    public async Task T21_AMatchOutsideTheSeededCoverage_IsReported_NotInvented()
    {
        const string vienna = """[{"lat":"48.2082","lon":"16.3738","address":{"postcode":"1010","city":"Wien"}}]""";

        var result = await Provider(FakeHandler.Json(vienna)).GeocodeAsync("Stephansplatz 1, 1010 Wien", "de");

        result.Error!.Code.Should().Be("GEOCODE_OUTSIDE_COVERAGE");
    }

    [Fact]
    public async Task T28_T29_T30_Logs_NeverContainTheAddress_Coordinates_Url_OrKey()
    {
        await Provider(FakeHandler.Json(InnsbruckHit)).GeocodeAsync(PrivateAddress, "de");
        await Provider(FakeHandler.Json("[]")).GeocodeAsync(PrivateAddress, "de");
        await Provider(FakeHandler.Status(HttpStatusCode.InternalServerError), Options(retries: 0)).GeocodeAsync(PrivateAddress, "de");

        _logs.Entries.Should().NotBeEmpty("failures are logged — by correlation id only");
        _logs.Text.Should()
            .NotContain("Maria-Theresien").And
            .NotContain("Innsbruck").And
            .NotContain("47.26").And
            .NotContain("11.39").And
            .NotContain("geocoder.example.test").And
            .NotContain(ApiKey);
    }

    [Fact]
    public void T30_TheConfigurationProblemMessage_NeverEchoesTheKey()
    {
        var options = Options();
        options.BaseUrl = "http://insecure.example.test";

        options.ConfigurationProblem()!.Should().NotContain(ApiKey);
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public Uri? LastUri { get; private set; }

        public static FakeHandler Json(string body) => new((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));

        public static FakeHandler Status(HttpStatusCode status) => new((_, _) => Task.FromResult(new HttpResponseMessage(status)));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastUri = request.RequestUri;
            return respond(request, cancellationToken);
        }
    }

    private sealed class CapturingLogger : ILogger<NominatimGeocodingProvider>
    {
        public List<string> Entries { get; } = [];

        public string Text => string.Join('\n', Entries);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add(formatter(state, exception) + (exception is null ? "" : " | " + exception));
    }
}
