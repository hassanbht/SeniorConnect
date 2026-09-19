using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Geography.Application;
using SeniorConnect.Modules.Profiles.Application;
using SeniorConnect.Modules.Organizations.Application;
using SeniorConnect.Modules.HelpRequests.Contracts;
using SeniorConnect.Modules.Community.Contracts;

namespace SeniorConnect.Modules.Geography.Infrastructure;

public sealed class GeographyReferenceService : IGeographyReferenceService
{
    private readonly IGeographyDbContext _db;
    private readonly IMemoryCache? _cache;

    public GeographyReferenceService(IGeographyDbContext db, IMemoryCache? cache = null)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<Result<IReadOnlyList<BundeslandDto>>> GetBundeslaenderAsync(CancellationToken cancellationToken = default)
    {
        if (_cache is not null)
        {
            var cached = await _cache.GetOrCreateAsync("geo_bundeslaender", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
                entry.SlidingExpiration = TimeSpan.FromHours(4);
                return await QueryBundeslaenderAsync(cancellationToken);
            });
            return Result<IReadOnlyList<BundeslandDto>>.Success(cached ?? []);
        }

        return Result<IReadOnlyList<BundeslandDto>>.Success(await QueryBundeslaenderAsync(cancellationToken));
    }

    private async Task<IReadOnlyList<BundeslandDto>> QueryBundeslaenderAsync(CancellationToken ct)
    {
        var units = await _db.AustrianAdministrativeUnits
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new { x.BundeslandCode, x.BundeslandName })
            .Distinct()
            .OrderBy(x => x.BundeslandName)
            .ToListAsync(ct);

        return units.Select(u => new BundeslandDto(u.BundeslandCode, u.BundeslandName)).ToList();
    }

    public async Task<Result<IReadOnlyList<BezirkDto>>> GetBezirkeAsync(string bundeslandCode, CancellationToken cancellationToken = default)
    {
        if (_cache is not null)
        {
            var key = $"geo_bezirke_{bundeslandCode}";
            var cached = await _cache.GetOrCreateAsync(key, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
                entry.SlidingExpiration = TimeSpan.FromHours(4);
                return await QueryBezirkeAsync(bundeslandCode, cancellationToken);
            });
            return Result<IReadOnlyList<BezirkDto>>.Success(cached ?? []);
        }

        return Result<IReadOnlyList<BezirkDto>>.Success(await QueryBezirkeAsync(bundeslandCode, cancellationToken));
    }

    private async Task<IReadOnlyList<BezirkDto>> QueryBezirkeAsync(string bundeslandCode, CancellationToken ct)
    {
        var units = await _db.AustrianAdministrativeUnits
            .AsNoTracking()
            .Where(x => x.IsActive && x.BundeslandCode == bundeslandCode)
            .Select(x => new { x.BezirkCode, x.BezirkName })
            .Distinct()
            .OrderBy(x => x.BezirkName)
            .ToListAsync(ct);

        return units.Select(u => new BezirkDto(u.BezirkCode, u.BezirkName)).ToList();
    }

    public async Task<Result<IReadOnlyList<GemeindeDto>>> GetGemeindenAsync(string bezirkCode, CancellationToken cancellationToken = default)
    {
        if (_cache is not null)
        {
            var key = $"geo_gemeinden_{bezirkCode}";
            var cached = await _cache.GetOrCreateAsync(key, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
                entry.SlidingExpiration = TimeSpan.FromHours(4);
                return await QueryGemeindenAsync(bezirkCode, cancellationToken);
            });
            return Result<IReadOnlyList<GemeindeDto>>.Success(cached ?? []);
        }

        return Result<IReadOnlyList<GemeindeDto>>.Success(await QueryGemeindenAsync(bezirkCode, cancellationToken));
    }

    private async Task<IReadOnlyList<GemeindeDto>> QueryGemeindenAsync(string bezirkCode, CancellationToken ct)
    {
        var units = await _db.AustrianAdministrativeUnits
            .AsNoTracking()
            .Where(x => x.IsActive && x.BezirkCode == bezirkCode)
            .Select(x => new { x.GemeindeCode, x.GemeindeName, x.PostalCode, x.Latitude, x.Longitude })
            .Distinct()
            .OrderBy(x => x.GemeindeName)
            .ToListAsync(ct);

        return units.Select(u => new GemeindeDto(
            u.GemeindeCode, u.GemeindeName, u.PostalCode, u.Latitude, u.Longitude)).ToList();
    }

    public async Task<Result<IReadOnlyList<GemeindeDto>>> LookupByPlzAsync(string plz, CancellationToken cancellationToken = default)
    {
        if (_cache is not null)
        {
            var key = $"geo_plz_{plz}";
            var cached = await _cache.GetOrCreateAsync(key, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
                entry.SlidingExpiration = TimeSpan.FromHours(4);
                return await QueryLookupByPlzAsync(plz, cancellationToken);
            });
            return Result<IReadOnlyList<GemeindeDto>>.Success(cached ?? []);
        }

        return Result<IReadOnlyList<GemeindeDto>>.Success(await QueryLookupByPlzAsync(plz, cancellationToken));
    }

    private async Task<IReadOnlyList<GemeindeDto>> QueryLookupByPlzAsync(string plz, CancellationToken ct)
    {
        var units = await _db.AustrianAdministrativeUnits
            .AsNoTracking()
            .Where(x => x.IsActive && x.PostalCode == plz)
            .Select(x => new { x.GemeindeCode, x.GemeindeName, x.PostalCode, x.Latitude, x.Longitude })
            .Distinct()
            .ToListAsync(ct);

        return units.Select(u => new GemeindeDto(
            u.GemeindeCode, u.GemeindeName, u.PostalCode, u.Latitude, u.Longitude)).ToList();
    }
}

public sealed class GeocodingProviderStub : IGeocodingProvider
{
    private readonly ILogger<GeocodingProviderStub> _logger;

    public GeocodingProviderStub(ILogger<GeocodingProviderStub> logger)
    {
        _logger = logger;
    }

    public Task<Result<GeocodeResult>> GeocodeAsync(string address, string preferredLocale, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("[Geocoding STUB] Geocoding address: {Address}", address);

        if (string.IsNullOrWhiteSpace(address))
        {
            return Task.FromResult(Result<GeocodeResult>.Failure(
                new Error("GEOCODE_INVALID_ADDRESS", "Address is required.", ErrorKind.Validation)));
        }

        var lower = address.ToLowerInvariant();
        if (lower.Contains("kematen") || lower.Contains("6175"))
        {
            return Task.FromResult(Result<GeocodeResult>.Success(new GeocodeResult(
                AddressLine: "Dorfplatz 2",
                PostalCode: "6175",
                City: "Kematen in Tirol",
                Latitude: 47.2600,
                Longitude: 11.2433,
                GemeindeCode: "70320",
                GemeindeName: "Kematen in Tirol",
                BezirkCode: "703",
                BezirkName: "Innsbruck-Land",
                BundeslandCode: "7",
                BundeslandName: "Tirol",
                Confidence: 0.95)));
        }

        if (lower.Contains("innsbruck"))
        {
            return Task.FromResult(Result<GeocodeResult>.Success(new GeocodeResult(
                AddressLine: "Maria-Theresien-Straße 1",
                PostalCode: "6020",
                City: "Innsbruck",
                Latitude: 47.2692,
                Longitude: 11.4041,
                GemeindeCode: "70101",
                GemeindeName: "Innsbruck",
                BezirkCode: "701",
                BezirkName: "Innsbruck",
                BundeslandCode: "7",
                BundeslandName: "Tirol",
                Confidence: 0.9)));
        }

        return Task.FromResult(Result<GeocodeResult>.Success(new GeocodeResult(
            AddressLine: address,
            PostalCode: "6020",
            City: "Innsbruck",
            Latitude: 47.2692,
            Longitude: 11.4041,
            GemeindeCode: "70101",
            GemeindeName: "Innsbruck",
            BezirkCode: "701",
            BezirkName: "Innsbruck",
            BundeslandCode: "7",
            BundeslandName: "Tirol",
            Confidence: 0.5)));
    }
}

public sealed class ProximityService : IProximityService
{
    private sealed record AdminUnitCacheItem(string GemeindeCode, string GemeindeName, string BezirkName, string BundeslandName, double Latitude, double Longitude);

    private readonly IGeographyDbContext _geoDb;
    private readonly IProfilesDbContext _profileDb;
    private readonly IOrganizationsDbContext _orgDb;
    private readonly IHelpRequestDiscoveryReader _helpRequestReader;
    private readonly ICommunityDiscoveryReader _communityReader;
    private readonly IMemoryCache? _cache;

    public ProximityService(
        IGeographyDbContext geoDb,
        IProfilesDbContext profileDb,
        IOrganizationsDbContext orgDb,
        IHelpRequestDiscoveryReader helpRequestReader,
        ICommunityDiscoveryReader communityReader,
        IMemoryCache? cache = null)
    {
        _geoDb = geoDb;
        _profileDb = profileDb;
        _orgDb = orgDb;
        _helpRequestReader = helpRequestReader;
        _communityReader = communityReader;
        _cache = cache;
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;

    public async Task<Result<IReadOnlyList<ProximityResult>>> FindNearbyOrganizationsAsync(
        double latitude, double longitude, double radiusKm, CancellationToken ct = default)
    {
        var branches = await _orgDb.OrganizationBranches
            .AsNoTracking()
            .Where(b => b.IsActive && b.Latitude.HasValue && b.Longitude.HasValue)
            .ToListAsync(ct);

        var results = branches
            .Select(b => new
            {
                Branch = b,
                DistanceKm = HaversineKm(latitude, longitude, b.Latitude!.Value, b.Longitude!.Value)
            })
            .Where(x => x.DistanceKm <= radiusKm)
            .OrderBy(x => x.DistanceKm)
            .Select(x => new ProximityResult(
                UserId: Guid.Empty,
                DisplayName: x.Branch.Name,
                DistanceKm: Math.Round(x.DistanceKm, 1),
                LocalityName: x.Branch.City ?? "",
                PostalCode: x.Branch.PostalCode ?? "",
                GemeindeName: x.Branch.City ?? "",
                BezirkName: "",
                Latitude: x.Branch.Latitude!.Value,
                Longitude: x.Branch.Longitude!.Value,
                IsFuzzed: true))
            .ToList();

        return Result<IReadOnlyList<ProximityResult>>.Success(results);
    }

    public async Task<Result<IReadOnlyList<ProximityResult>>> FindNearbyVolunteersAsync(
        double latitude, double longitude, double radiusKm, CancellationToken ct = default)
    {
        var profiles = await _profileDb.VolunteerProfiles
            .AsNoTracking()
            .Where(v => v.Latitude.HasValue && v.Longitude.HasValue && v.IsAcceptingRequests)
            .ToListAsync(ct);

        var results = profiles
            .Select(v => new
            {
                Profile = v,
                DistanceKm = HaversineKm(latitude, longitude, v.Latitude!.Value, v.Longitude!.Value)
            })
            .Where(x => x.DistanceKm <= radiusKm && x.DistanceKm <= x.Profile.MaxDistanceKm)
            .OrderBy(x => x.DistanceKm)
            .Select(x => new ProximityResult(
                UserId: x.Profile.UserId,
                DisplayName: "",
                DistanceKm: Math.Round(x.DistanceKm, 1),
                LocalityName: "",
                PostalCode: x.Profile.PostalCode ?? "",
                GemeindeName: "",
                BezirkName: "",
                Latitude: x.Profile.Latitude!.Value,
                Longitude: x.Profile.Longitude!.Value,
                IsFuzzed: true))
            .ToList();

        return Result<IReadOnlyList<ProximityResult>>.Success(results);
    }

    public async Task<Result<IReadOnlyList<ProximityResult>>> FindNearbyRequestsAsync(
        double latitude, double longitude, double radiusKm, CancellationToken ct = default)
    {
        var candidates = await _helpRequestReader.FindOpenRequestsWithLocationAsync(ct);

        var results = candidates
            .Select(r => new
            {
                Request = r,
                DistanceKm = HaversineKm(latitude, longitude, r.Latitude, r.Longitude)
            })
            .Where(x => x.DistanceKm <= radiusKm)
            .OrderBy(x => x.DistanceKm)
            .Select(x => new ProximityResult(
                UserId: Guid.Empty,
                DisplayName: "",
                DistanceKm: Math.Round(x.DistanceKm, 1),
                LocalityName: "",
                PostalCode: "",
                GemeindeName: "",
                BezirkName: "",
                Latitude: x.Request.Latitude,
                Longitude: x.Request.Longitude,
                IsFuzzed: true))
            .ToList();

        return Result<IReadOnlyList<ProximityResult>>.Success(results);
    }

    public async Task<Result<IReadOnlyList<NearbyTownDto>>> GetNearestTownsAsync(
        double latitude, double longitude, double maxDistanceKm = 50, int maxResults = 10, CancellationToken ct = default)
    {
        IReadOnlyList<AdminUnitCacheItem> units;
        if (_cache is not null)
        {
            units = await _cache.GetOrCreateAsync("geo_active_admin_units", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);
                entry.SlidingExpiration = TimeSpan.FromHours(2);
                return await _geoDb.AustrianAdministrativeUnits
                    .AsNoTracking()
                    .Where(x => x.IsActive)
                    .Select(u => new AdminUnitCacheItem(u.GemeindeCode, u.GemeindeName, u.BezirkName, u.BundeslandName, u.Latitude, u.Longitude))
                    .ToListAsync(ct);
            }) ?? [];
        }
        else
        {
            units = await _geoDb.AustrianAdministrativeUnits
                .AsNoTracking()
                .Where(x => x.IsActive)
                .Select(u => new AdminUnitCacheItem(u.GemeindeCode, u.GemeindeName, u.BezirkName, u.BundeslandName, u.Latitude, u.Longitude))
                .ToListAsync(ct);
        }

        var results = units
            .Select(u => new
            {
                Unit = u,
                DistanceKm = HaversineKm(latitude, longitude, u.Latitude, u.Longitude)
            })
            .Where(x => x.DistanceKm <= maxDistanceKm)
            .OrderBy(x => x.DistanceKm)
            .Take(maxResults)
            .Select(x => new NearbyTownDto(
                GemeindeCode: x.Unit.GemeindeCode,
                GemeindeName: x.Unit.GemeindeName,
                BezirkName: x.Unit.BezirkName,
                BundeslandName: x.Unit.BundeslandName,
                DistanceKm: Math.Round(x.DistanceKm, 1),
                Latitude: x.Unit.Latitude,
                Longitude: x.Unit.Longitude))
            .ToList();

        return Result<IReadOnlyList<NearbyTownDto>>.Success(results);
    }

    public async Task<Result<IReadOnlyList<CommunityEventDiscoveryResult>>> FindNearbyCommunityEventsAsync(
        double latitude, double longitude, double radiusKm, Guid? callerUserId, bool matchMyInterests, CancellationToken ct = default)
    {
        var events = await _communityReader.FindUpcomingEventsForDiscoveryAsync(ct);

        var withPostalCode = events.Where(e => !string.IsNullOrWhiteSpace(e.PostalCode)).ToList();
        if (withPostalCode.Count == 0)
        {
            return Result<IReadOnlyList<CommunityEventDiscoveryResult>>.Success([]);
        }

        var postalCodes = withPostalCode.Select(e => e.PostalCode!).Distinct().ToList();
        var matchingUnits = await _geoDb.AustrianAdministrativeUnits
            .AsNoTracking()
            .Where(u => u.IsActive && postalCodes.Contains(u.PostalCode))
            .ToListAsync(ct);
        var unitsByPostalCode = matchingUnits
            .GroupBy(u => u.PostalCode)
            .ToDictionary(g => g.Key, g => g.First());

        HashSet<string>? myInterestCodes = null;
        if (matchMyInterests && callerUserId is not null)
        {
            myInterestCodes = (await _profileDb.UserInterests
                .AsNoTracking()
                .Where(ui => ui.UserId == callerUserId.Value)
                .Join(_profileDb.Interests.AsNoTracking(), ui => ui.InterestId, i => i.Id, (ui, i) => i.Code)
                .ToListAsync(ct))
                .Select(c => c.ToLowerInvariant())
                .ToHashSet();
        }

        var results = withPostalCode
            .Where(e => unitsByPostalCode.ContainsKey(e.PostalCode!))
            // No saved interests yet is "no filter", not "hide everything".
            .Where(e => myInterestCodes is null || myInterestCodes.Count == 0
                || myInterestCodes.Contains(e.Category.ToLowerInvariant()))
            .Select(e =>
            {
                var unit = unitsByPostalCode[e.PostalCode!];
                var distanceKm = HaversineKm(latitude, longitude, unit.Latitude, unit.Longitude);
                return new CommunityEventDiscoveryResult(e.EventId, e.Title, e.Category, e.StartsAtUtc, Math.Round(distanceKm, 1));
            })
            .Where(r => r.DistanceKm <= radiusKm)
            .OrderBy(r => r.DistanceKm)
            .ToList();

        return Result<IReadOnlyList<CommunityEventDiscoveryResult>>.Success(results);
    }
}

public sealed class StoreLocationService : IStoreLocationService
{
    private readonly IProfilesDbContext _profileDb;

    public StoreLocationService(IProfilesDbContext profileDb)
    {
        _profileDb = profileDb;
    }

    public async Task<Result> StoreGeocodedLocationAsync(
        Guid userId,
        string addressLine,
        string postalCode,
        string city,
        double latitude,
        double longitude,
        string gemeindeCode,
        CancellationToken cancellationToken = default)
    {
        var supportProfile = await _profileDb.SupportProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (supportProfile is not null)
        {
            supportProfile.UpdateDetails(
                addressLine: addressLine,
                postalCode: postalCode,
                city: city,
                latitude: latitude,
                longitude: longitude,
                mobilityNote: supportProfile.MobilityNote,
                livingSituation: supportProfile.LivingSituation,
                preferredContactMethod: supportProfile.PreferredContactMethod);
        }

        var volunteerProfile = await _profileDb.VolunteerProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (volunteerProfile is not null)
        {
            volunteerProfile.UpdatePreferences(
                bio: null,
                postalCode: postalCode,
                latitude: latitude,
                longitude: longitude,
                maxDistanceKm: volunteerProfile.MaxDistanceKm,
                maxActivitiesPerWeek: volunteerProfile.MaxActivitiesPerWeek,
                hasCar: volunteerProfile.HasCar,
                isAcceptingRequests: volunteerProfile.IsAcceptingRequests);
        }

        await _profileDb.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

