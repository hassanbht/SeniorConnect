namespace SeniorConnect.Domain;

/// <summary>
/// What a person has explicitly agreed to share about their own location
/// (docs/security/location-consent-and-visibility.md §2). Stored as the latest
/// row of the owner's consent history; no row means <see cref="None"/>.
/// </summary>
public enum LocationSharingLevel
{
    /// <summary>Default. Others learn at most a distance band.</summary>
    None = 0,

    /// <summary>Others may also see the area (municipality / postal code, ~1 km grid).</summary>
    Approximate = 1,

    /// <summary>Exact coordinates, but only inside an authorized relationship.</summary>
    Precise = 2,
}

/// <summary>How much of a location one viewer actually receives.</summary>
public enum LocationDisclosure
{
    None = 0,
    DistanceOnly = 1,
    Approximate = 2,
    Precise = 3,
}

/// <summary>
/// The viewer's relationship to the location, established by the caller from
/// server-side facts (ownership, assignment, membership) — never from a
/// client flag. Verification/trust is deliberately not an input.
/// </summary>
public enum LocationRelationship
{
    /// <summary>No established relationship: the location is not disclosed.</summary>
    None = 0,

    /// <summary>The owner looking at their own location.</summary>
    Self,

    /// <summary>Whoever entered this help request's location (the senior, or a family member / coordinator acting for them).</summary>
    RequestCreator,

    /// <summary>The volunteer currently assigned to this help request.</summary>
    AssignedVolunteer,

    /// <summary>Coordinator/admin of the owning organization. Never more than approximate.</summary>
    OrganizationStaff,

    /// <summary>Any other authenticated user reaching the location through discovery or an open list.</summary>
    DiscoveryViewer,
}

/// <summary>
/// THE location disclosure rule. Every response carrying another person's
/// location decides through here; no endpoint or mapper decides on its own.
/// </summary>
public static class LocationVisibilityPolicy
{
    public static LocationDisclosure Decide(LocationRelationship relationship, LocationSharingLevel ownerConsent) =>
        relationship switch
        {
            LocationRelationship.Self => LocationDisclosure.Precise,
            LocationRelationship.RequestCreator => LocationDisclosure.Precise,

            // The standard path to exact coordinates: an authorized assignment
            // AND the owner's explicit precise consent. Both, always.
            LocationRelationship.AssignedVolunteer => ownerConsent == LocationSharingLevel.Precise
                ? LocationDisclosure.Precise
                : LocationDisclosure.Approximate,

            // Membership, coordinator and admin roles never unlock exact
            // coordinates; no staff workflow needs them yet (documented gap).
            LocationRelationship.OrganizationStaff => LocationDisclosure.Approximate,

            LocationRelationship.DiscoveryViewer => ownerConsent == LocationSharingLevel.None
                ? LocationDisclosure.DistanceOnly
                : LocationDisclosure.Approximate,

            _ => LocationDisclosure.None,
        };

    /// <summary>A disclosure capped at <paramref name="cap"/> (e.g. generic DTOs never go above Approximate).</summary>
    public static LocationDisclosure AtMost(this LocationDisclosure disclosure, LocationDisclosure cap) =>
        disclosure <= cap ? disclosure : cap;

    /// <summary>Coordinates as the disclosure allows: exact, ~1 km grid cell, or nothing.</summary>
    public static (double? Latitude, double? Longitude) Shape(
        LocationDisclosure disclosure, double? latitude, double? longitude)
    {
        if (latitude is not { } lat || longitude is not { } lon)
        {
            return (null, null);
        }

        return disclosure switch
        {
            LocationDisclosure.Precise => (lat, lon),
            LocationDisclosure.Approximate => (LocationPrivacy.SnapToGrid(lat), LocationPrivacy.SnapToGrid(lon)),
            _ => (null, null),
        };
    }

    /// <summary>Area text (postal code, municipality) is shown from Approximate up.</summary>
    public static bool ShowsArea(this LocationDisclosure disclosure) => disclosure >= LocationDisclosure.Approximate;

    /// <summary>Stable wire code for the disclosure level.</summary>
    public static string ToCode(this LocationDisclosure disclosure) => disclosure switch
    {
        LocationDisclosure.Precise => "PRECISE",
        LocationDisclosure.Approximate => "APPROXIMATE",
        LocationDisclosure.DistanceOnly => "DISTANCE_ONLY",
        _ => "NONE",
    };
}

/// <summary>Coordinate validation shared by every write path and the geocoder.</summary>
public static class GeoCoordinate
{
    /// <summary>Both or neither; finite; latitude in [-90, 90]; longitude in [-180, 180].</summary>
    public static bool IsValidPair(double? latitude, double? longitude) =>
        (latitude, longitude) switch
        {
            (null, null) => true,
            ({ } lat, { } lon) => double.IsFinite(lat) && double.IsFinite(lon)
                && lat is >= -90 and <= 90 && lon is >= -180 and <= 180,
            _ => false,
        };

    /// <summary>Rough Austria bounding box (lat ~46–49, lon ~9–17); also catches an inverted lat/lon pair.</summary>
    public static bool IsInAustria(double latitude, double longitude) =>
        latitude is >= 46.3 and <= 49.1 && longitude is >= 9.4 and <= 17.2;

    public static Error Invalid() => new(
        "LOCATION_INVALID_COORDINATES",
        "Latitude must be between -90 and 90 and longitude between -180 and 180, and both must be given together.",
        ErrorKind.Validation);
}
