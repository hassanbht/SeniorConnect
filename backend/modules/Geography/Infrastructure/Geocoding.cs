using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Geography.Application;

namespace SeniorConnect.Modules.Geography.Infrastructure;

/// <summary>
/// "Geocoding" configuration section (docs/security/location-consent-and-visibility.md §7).
/// Secrets come from the environment (<c>Geocoding__ApiKey</c>), never from a
/// committed file, and never reach the Flutter app.
/// </summary>
public sealed class GeocodingOptions
{
    public const string SectionName = "Geocoding";

    /// <summary>"Nominatim" (any Nominatim-compatible search API: self-hosted, OSM, LocationIQ …) or "None".</summary>
    public string Provider { get; set; } = "None";

    public string? BaseUrl { get; set; }

    public string? ApiKey { get; set; }

    /// <summary>Query parameter carrying <see cref="ApiKey"/> (LocationIQ: "key").</summary>
    public string ApiKeyParameter { get; set; } = "key";

    /// <summary>Hosted commercial endpoints need a key; a self-hosted Nominatim may not.</summary>
    public bool RequireApiKey { get; set; }

    /// <summary>Required by the OSM Nominatim usage policy; identifies the deployment.</summary>
    public string UserAgent { get; set; } = "SeniorConnect";

    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>Extra attempts after a transient failure (timeout, 5xx, network).</summary>
    public int MaxRetries { get; set; } = 1;

    /// <summary>Minimum spacing between provider calls from this instance (OSM policy: 1 request/second).</summary>
    public int MinIntervalMilliseconds { get; set; } = 1000;

    /// <summary>Why this configuration cannot be used, or null when it can. Never contains the key.</summary>
    public string? ConfigurationProblem()
    {
        if (!string.Equals(Provider, "Nominatim", StringComparison.OrdinalIgnoreCase))
        {
            return "no geocoding provider is configured";
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return "Geocoding:BaseUrl must be an absolute https URL";
        }

        if (RequireApiKey && string.IsNullOrWhiteSpace(ApiKey))
        {
            return "Geocoding:ApiKey is required by this provider but missing";
        }

        return null;
    }
}

internal static class GeocodingErrors
{
    public static readonly Error InvalidAddress = new(
        "GEOCODE_INVALID_ADDRESS", "Address is required.", ErrorKind.Validation);

    public static readonly Error NotFound = new(
        "GEOCODE_NOT_FOUND",
        "This address could not be located. Please check it or place the pin on the map.",
        ErrorKind.Validation);

    public static readonly Error OutsideCoverage = new(
        "GEOCODE_OUTSIDE_COVERAGE",
        "This address is outside the area SeniorConnect currently covers.",
        ErrorKind.Validation);

    public static readonly Error NotConfigured = Error.ProviderUnavailable(
        "GEOCODING_NOT_CONFIGURED", "Address lookup is not available right now. Please enter your area manually.");

    public static readonly Error Timeout = Error.ProviderUnavailable(
        "GEOCODING_TIMEOUT", "Address lookup took too long. Please try again.");

    public static readonly Error Unavailable = Error.ProviderUnavailable(
        "GEOCODING_UNAVAILABLE", "Address lookup is not available right now. Please try again later.");

    public static readonly Error RateLimited = Error.ProviderUnavailable(
        "GEOCODING_RATE_LIMITED", "Address lookup is busy. Please try again in a minute.");
}

/// <summary>
/// Registered when no usable provider is configured: every lookup fails
/// truthfully. There is no fake/sample fallback anywhere.
/// </summary>
public sealed class UnconfiguredGeocodingProvider : IGeocodingProvider
{
    public Task<Result<GeocodeResult>> GeocodeAsync(string address, string preferredLocale, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<GeocodeResult>.Failure(GeocodingErrors.NotConfigured));
}

/// <summary>
/// Nominatim-compatible forward geocoding, called only by the backend.
/// Privacy: the address, the request URL, the coordinates and the key are
/// never logged (the typed HttpClient has its loggers removed, too) and
/// results are not cached.
/// </summary>
public sealed class NominatimGeocodingProvider : IGeocodingProvider
{
    // ponytail: per-instance spacing only; use a shared limiter if the API scales out.
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTimeOffset _lastCallUtc = DateTimeOffset.MinValue;

    private readonly HttpClient _http;
    private readonly GeocodingOptions _options;
    private readonly IGeographyDbContext _geoDb;
    private readonly ILogger<NominatimGeocodingProvider> _logger;
    private readonly TimeProvider _clock;

    public NominatimGeocodingProvider(
        HttpClient http,
        GeocodingOptions options,
        IGeographyDbContext geoDb,
        ILogger<NominatimGeocodingProvider> logger,
        TimeProvider? clock = null)
    {
        _http = http;
        _options = options;
        _geoDb = geoDb;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Result<GeocodeResult>> GeocodeAsync(
        string address, string preferredLocale, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address) || address.Length > 300)
        {
            return GeocodingErrors.InvalidAddress;
        }

        if (_options.ConfigurationProblem() is not null)
        {
            return GeocodingErrors.NotConfigured;
        }

        var correlationId = Guid.NewGuid().ToString("N")[..12];
        var fetched = await FetchWithRetryAsync(address.Trim(), preferredLocale, correlationId, cancellationToken);
        if (fetched.IsFailure)
        {
            return fetched.Error!;
        }

        var hit = fetched.Value!.Count > 0 ? fetched.Value[0] : null;
        if (hit is null
            || !double.TryParse(hit.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
            || !double.TryParse(hit.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude)
            || !GeoCoordinate.IsValidPair(latitude, longitude)
            || !GeoCoordinate.IsInAustria(latitude, longitude))
        {
            _logger.LogInformation("Geocoding {CorrelationId}: no usable match", correlationId);
            return GeocodingErrors.NotFound;
        }

        var postalCode = hit.Address?.Postcode?.Trim();
        if (string.IsNullOrEmpty(postalCode))
        {
            return GeocodingErrors.NotFound;
        }

        // Administrative codes come from our own seed, never from free text.
        // Several Gemeinden can share a PLZ: take the one whose centroid is closest.
        var units = await _geoDb.AustrianAdministrativeUnits
            .AsNoTracking()
            .Where(u => u.IsActive && u.PostalCode == postalCode)
            .ToListAsync(cancellationToken);
        var unit = units.MinBy(u => LocationPrivacy.HaversineKm(latitude, longitude, u.Latitude, u.Longitude));
        if (unit is null)
        {
            _logger.LogInformation("Geocoding {CorrelationId}: match outside the seeded coverage", correlationId);
            return GeocodingErrors.OutsideCoverage;
        }

        var street = string.Join(' ', new[] { hit.Address?.Road, hit.Address?.HouseNumber }
            .Where(p => !string.IsNullOrWhiteSpace(p)));

        return Result<GeocodeResult>.Success(new GeocodeResult(
            AddressLine: street.Length > 0 ? street : unit.LocalityName,
            PostalCode: postalCode,
            City: hit.Address?.Locality ?? unit.GemeindeName,
            Latitude: latitude,
            Longitude: longitude,
            GemeindeCode: unit.GemeindeCode,
            GemeindeName: unit.GemeindeName,
            BezirkCode: unit.BezirkCode,
            BezirkName: unit.BezirkName,
            BundeslandCode: unit.BundeslandCode,
            BundeslandName: unit.BundeslandName,
            Confidence: Math.Clamp(hit.Importance ?? 0, 0, 1)));
    }

    private async Task<Result<IReadOnlyList<NominatimHit>>> FetchWithRetryAsync(
        string address, string locale, string correlationId, CancellationToken ct)
    {
        var attempts = 1 + Math.Max(0, _options.MaxRetries);
        Error lastError = GeocodingErrors.Unavailable;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            await WaitForSlotAsync(ct);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(address, locale));
                request.Headers.UserAgent.ParseAdd(_options.UserAgent);
                using var response = await _http.SendAsync(request, timeout.Token);

                if (response.IsSuccessStatusCode)
                {
                    var hits = await JsonSerializer.DeserializeAsync<List<NominatimHit>>(
                        await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
                    return Result<IReadOnlyList<NominatimHit>>.Success(hits ?? []);
                }

                var status = (int)response.StatusCode;
                _logger.LogWarning("Geocoding {CorrelationId}: provider answered {Status} (attempt {Attempt})", correlationId, status, attempt);
                if (response.StatusCode == HttpStatusCode.TooManyRequests) return GeocodingErrors.RateLimited;
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return GeocodingErrors.NotConfigured;
                if (status < 500) return GeocodingErrors.NotFound;
                lastError = GeocodingErrors.Unavailable;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("Geocoding {CorrelationId}: provider timed out (attempt {Attempt})", correlationId, attempt);
                lastError = GeocodingErrors.Timeout;
            }
            catch (HttpRequestException)
            {
                // The exception text is not logged: it can echo the request URI.
                _logger.LogWarning("Geocoding {CorrelationId}: provider unreachable (attempt {Attempt})", correlationId, attempt);
                lastError = GeocodingErrors.Unavailable;
            }
            catch (JsonException)
            {
                _logger.LogWarning("Geocoding {CorrelationId}: provider sent an unreadable answer", correlationId);
                return GeocodingErrors.Unavailable;
            }
        }

        return lastError;
    }

    private Uri BuildUri(string address, string locale)
    {
        var query = new List<string>
        {
            "q=" + Uri.EscapeDataString(address),
            "format=jsonv2",
            "addressdetails=1",
            "limit=1",
            "countrycodes=at",
            "accept-language=" + Uri.EscapeDataString(string.IsNullOrWhiteSpace(locale) ? "de" : locale),
        };
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            query.Add(Uri.EscapeDataString(_options.ApiKeyParameter) + "=" + Uri.EscapeDataString(_options.ApiKey));
        }

        return new Uri($"{_options.BaseUrl!.TrimEnd('/')}/search?{string.Join('&', query)}");
    }

    private async Task WaitForSlotAsync(CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var wait = _lastCallUtc.AddMilliseconds(_options.MinIntervalMilliseconds) - _clock.GetUtcNow();
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, _clock, ct);
            }

            _lastCallUtc = _clock.GetUtcNow();
        }
        finally
        {
            Gate.Release();
        }
    }

    internal sealed class NominatimHit
    {
        [JsonPropertyName("lat")] public string? Lat { get; set; }
        [JsonPropertyName("lon")] public string? Lon { get; set; }
        [JsonPropertyName("importance")] public double? Importance { get; set; }
        [JsonPropertyName("address")] public NominatimAddress? Address { get; set; }
    }

    internal sealed class NominatimAddress
    {
        [JsonPropertyName("road")] public string? Road { get; set; }
        [JsonPropertyName("house_number")] public string? HouseNumber { get; set; }
        [JsonPropertyName("postcode")] public string? Postcode { get; set; }
        [JsonPropertyName("city")] public string? City { get; set; }
        [JsonPropertyName("town")] public string? Town { get; set; }
        [JsonPropertyName("village")] public string? Village { get; set; }
        [JsonPropertyName("municipality")] public string? Municipality { get; set; }

        public string? Locality => City ?? Town ?? Village ?? Municipality;
    }
}
