using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Geography.Application;
using SeniorConnect.Modules.Profiles.Application;
using SeniorConnect.Modules.Profiles.Contracts;
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

public sealed class ProximityService : IProximityService
{
    private sealed record AdminUnitCacheItem(string GemeindeCode, string GemeindeName, string BezirkName, string BundeslandName, double Latitude, double Longitude);

    private readonly IGeographyDbContext _geoDb;
    private readonly IProfilesDbContext _profileDb;
    private readonly IOrganizationsDbContext _orgDb;
    private readonly IHelpRequestDiscoveryReader _helpRequestReader;
    private readonly ICommunityDiscoveryReader _communityReader;
    private readonly IMemoryCache? _cache;
    private readonly ILocationVisibilityService? _locationVisibility;
    private readonly IDiscoveryEligibilityService? _eligibility;

    public ProximityService(
        IGeographyDbContext geoDb,
        IProfilesDbContext profileDb,
        IOrganizationsDbContext orgDb,
        IHelpRequestDiscoveryReader helpRequestReader,
        ICommunityDiscoveryReader communityReader,
        IMemoryCache? cache = null,
        ILocationVisibilityService? locationVisibility = null,
        IDiscoveryEligibilityService? eligibility = null)
    {
        _geoDb = geoDb;
        _profileDb = profileDb;
        _orgDb = orgDb;
        _helpRequestReader = helpRequestReader;
        _communityReader = communityReader;
        _cache = cache;
        // Fail closed: without it every owner counts as not consented (band only).
        _locationVisibility = locationVisibility;
        // Phase 4: registered in production (ponytail: legacy unit tests run
        // without it and then see the pre-Phase-4 candidate set).
        _eligibility = eligibility;
    }

    private async Task<IReadOnlySet<Guid>?> EligibleAsync(
        Guid viewerUserId, IReadOnlyCollection<Guid> ownerUserIds, DiscoveryPurpose purpose, CancellationToken ct) =>
        _eligibility is null
            ? null
            : await _eligibility.FilterEligibleAsync(viewerUserId, ownerUserIds, new DiscoveryContext(purpose), ct);

    /// <summary>
    /// Discovery viewers see a person's area (postal code / Gemeinde) only
    /// when that person agreed to at least approximate sharing; otherwise just
    /// the band. Decided centrally, never here.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, LocationDisclosure>> DecideDiscoveryAsync(
        IReadOnlyCollection<Guid> ownerUserIds, Guid viewerUserId, CancellationToken ct) =>
        _locationVisibility is null
            ? ownerUserIds.Distinct().ToDictionary(
                id => id, _ => LocationVisibilityPolicy.Decide(LocationRelationship.DiscoveryViewer, LocationSharingLevel.None))
            : await _locationVisibility.DecideManyAsync(ownerUserIds, viewerUserId, LocationRelationship.DiscoveryViewer, ct);

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

    /// <summary>BR-GEO-06: snaps a coordinate to a 0.01° grid (~1.1 km N-S, ~0.75 km E-W in Tirol).</summary>
    internal static double FuzzCoordinate(double degrees) => LocationPrivacy.SnapToGrid(degrees);

    private static readonly Error LocationUnavailable = new(
        "LOCATION_UNAVAILABLE",
        "Add your address in your profile to see who and what is nearby.",
        ErrorKind.Conflict);

    /// <summary>
    /// Hotfix: the discovery origin is the caller's own stored location
    /// (support profile first, then volunteer profile) — never a point the
    /// client sends, so nobody can probe arbitrary places.
    /// </summary>
    private async Task<(double Latitude, double Longitude)?> GetCallerOriginAsync(Guid callerUserId, CancellationToken ct)
    {
        var support = await _profileDb.SupportProfiles
            .AsNoTracking()
            .Where(p => p.UserId == callerUserId && p.Latitude != null && p.Longitude != null)
            .Select(p => new { Latitude = p.Latitude!.Value, Longitude = p.Longitude!.Value })
            .FirstOrDefaultAsync(ct);
        if (support is not null) return (support.Latitude, support.Longitude);

        var volunteer = await _profileDb.VolunteerProfiles
            .AsNoTracking()
            .Where(p => p.UserId == callerUserId && p.Latitude != null && p.Longitude != null)
            .Select(p => new { Latitude = p.Latitude!.Value, Longitude = p.Longitude!.Value })
            .FirstOrDefaultAsync(ct);
        return volunteer is null ? null : (volunteer.Latitude, volunteer.Longitude);
    }

    /// <summary>Gemeinde name per postal code, for the "approximate area" label.</summary>
    private async Task<Dictionary<string, string>> GemeindeNamesByPostalCodeAsync(IEnumerable<string?> postalCodes, CancellationToken ct)
    {
        var codes = postalCodes.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct().ToList();
        if (codes.Count == 0) return [];

        var units = await _geoDb.AustrianAdministrativeUnits
            .AsNoTracking()
            .Where(u => u.IsActive && codes.Contains(u.PostalCode))
            .Select(u => new { u.PostalCode, u.GemeindeName })
            .ToListAsync(ct);

        return units.GroupBy(u => u.PostalCode).ToDictionary(g => g.Key, g => g.First().GemeindeName);
    }

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
                DistanceBand: DistanceBand.FromKm(x.DistanceKm),
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
        Guid callerUserId, double radiusKm, CancellationToken ct = default)
    {
        if (await GetCallerOriginAsync(callerUserId, ct) is not { } origin)
        {
            return LocationUnavailable;
        }

        var profiles = await _profileDb.VolunteerProfiles
            .AsNoTracking()
            .Where(v => v.Latitude.HasValue && v.Longitude.HasValue && v.IsAcceptingRequests && v.UserId != callerUserId)
            .ToListAsync(ct);

        // BR-GEO-06 + hotfix: a volunteer's home is never revealed. The
        // distance comes from its ~1 km grid cell, is reported only as a band,
        // and the radius filter works on bands too — so it cannot be
        // binary-searched. No coordinates and no UserId are returned.
        var banded = profiles
            .Select(v => new
            {
                Profile = v,
                Band = DistanceBand.FromKm(HaversineKm(
                    origin.Latitude, origin.Longitude,
                    FuzzCoordinate(v.Latitude!.Value), FuzzCoordinate(v.Longitude!.Value))),
            })
            .Where(x => DistanceBand.IsWithin(x.Band, radiusKm) && DistanceBand.IsWithin(x.Band, x.Profile.MaxDistanceKm))
            .OrderBy(x => DistanceBand.UpperKm(x.Band))
            .ToList();

        // Phase 4: only people who chose to be discoverable, are verified,
        // active, not blocked and not safeguarding-restricted.
        var eligible = await EligibleAsync(callerUserId, banded.Select(x => x.Profile.UserId).ToList(), DiscoveryPurpose.OpenDiscovery, ct);
        if (eligible is not null)
        {
            banded = banded.Where(x => eligible.Contains(x.Profile.UserId)).ToList();
        }

        var disclosures = await DecideDiscoveryAsync(banded.Select(x => x.Profile.UserId).ToList(), callerUserId, ct);
        var gemeinden = await GemeindeNamesByPostalCodeAsync(banded.Select(x => x.Profile.PostalCode), ct);

        var results = banded
            .Select(x => (x.Band, Area: disclosures[x.Profile.UserId].ShowsArea() ? x.Profile.PostalCode : null))
            .Select(x => new ProximityResult(
                UserId: Guid.Empty,
                DisplayName: "",
                DistanceKm: null,
                DistanceBand: x.Band,
                LocalityName: "",
                PostalCode: x.Area ?? "",
                GemeindeName: x.Area is { } plz && gemeinden.TryGetValue(plz, out var name) ? name : "",
                BezirkName: "",
                Latitude: null,
                Longitude: null,
                IsFuzzed: true))
            .ToList();

        return Result<IReadOnlyList<ProximityResult>>.Success(results);
    }

    public async Task<Result<IReadOnlyList<ProximityResult>>> FindNearbyRequestsAsync(
        Guid callerUserId, double radiusKm, CancellationToken ct = default)
    {
        if (await GetCallerOriginAsync(callerUserId, ct) is not { } origin)
        {
            return LocationUnavailable;
        }

        var candidates = await _helpRequestReader.FindOpenRequestsWithLocationAsync(ct);

        // BR-GEO-06 + hotfix: an open request's location is a senior's home in
        // most cases — reduced exactly like volunteers above.
        var banded = candidates
            .Select(r => new
            {
                Row = r,
                Band = DistanceBand.FromKm(HaversineKm(
                    origin.Latitude, origin.Longitude,
                    FuzzCoordinate(r.Latitude), FuzzCoordinate(r.Longitude))),
            })
            .Where(x => DistanceBand.IsWithin(x.Band, radiusKm))
            .OrderBy(x => DistanceBand.UpperKm(x.Band))
            .ToList();

        // Phase 4: requests of people who blocked the caller, or are
        // restricted towards them, do not exist for the caller.
        var eligible = await EligibleAsync(callerUserId, banded.Select(x => x.Row.SeniorUserId).ToList(), DiscoveryPurpose.OwnedContent, ct);
        if (eligible is not null)
        {
            banded = banded.Where(x => eligible.Contains(x.Row.SeniorUserId)).ToList();
        }

        var disclosures = await DecideDiscoveryAsync(banded.Select(x => x.Row.SeniorUserId).ToList(), callerUserId, ct);
        var gemeinden = await GemeindeNamesByPostalCodeAsync(banded.Select(x => x.Row.PostalCode), ct);

        var results = banded
            .Select(x => (x.Band, Area: disclosures[x.Row.SeniorUserId].ShowsArea() ? x.Row.PostalCode : null))
            .Select(x => new ProximityResult(
                UserId: Guid.Empty,
                DisplayName: "",
                DistanceKm: null,
                DistanceBand: x.Band,
                LocalityName: "",
                PostalCode: x.Area ?? "",
                GemeindeName: x.Area is { } plz && gemeinden.TryGetValue(plz, out var name) ? name : "",
                BezirkName: "",
                Latitude: null,
                Longitude: null,
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
                bio: volunteerProfile.Bio,
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

