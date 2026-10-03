namespace SeniorConnect.Domain;

/// <summary>
/// Hotfix standard for every cross-user location answer
/// (docs/security/hotfix-matching-location.md), until the full location
/// model (docs/security/location-and-reference-data-design.md) lands.
///
///   · Another person's coordinate is snapped to a 0.01° grid before ANY
///     distance is computed from it (~1.1 km N-S, ~0.75 km E-W in Austria),
///     so no amount of re-querying reveals more than that grid cell.
///   · Distances to other people are reported only as a band, never as km.
/// </summary>
public static class LocationPrivacy
{
    public const int GridDecimals = 2;

    /// <summary>BR-GEO-06: the ~1 km grid cell a private location is reduced to.</summary>
    public static double SnapToGrid(double degrees) => Math.Round(degrees, GridDecimals);

    public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2))
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}

/// <summary>
/// Stable distance-band codes (the client translates them; never branch on
/// the label). Upper bounds are inclusive: 3.0 km is "KM_1_3".
/// </summary>
public static class DistanceBand
{
    public const string Under1 = "KM_UNDER_1";
    public const string From1To3 = "KM_1_3";
    public const string From3To5 = "KM_3_5";
    public const string From5To10 = "KM_5_10";
    public const string From10To20 = "KM_10_20";
    public const string Over20 = "KM_OVER_20";

    private static readonly (double UpperKm, string Code)[] Bands =
    [
        (1, Under1),
        (3, From1To3),
        (5, From3To5),
        (10, From5To10),
        (20, From10To20),
    ];

    public static string FromKm(double km)
    {
        foreach (var (upperKm, code) in Bands)
        {
            if (km <= upperKm) return code;
        }

        return Over20;
    }

    /// <summary>Inclusive upper edge of a band; Over20 has none.</summary>
    public static double UpperKm(string band)
    {
        foreach (var (upperKm, code) in Bands)
        {
            if (code == band) return upperKm;
        }

        return double.PositiveInfinity;
    }

    /// <summary>
    /// A radius filter only as sharp as the bands: an item is "within" when
    /// its whole band is inside the radius (snapped up to a band edge).
    /// Otherwise a caller could binary-search the radius and recover the
    /// exact distance.
    /// </summary>
    public static bool IsWithin(string band, double radiusKm) => UpperKm(band) <= SnapRadius(radiusKm);

    private static double SnapRadius(double radiusKm)
    {
        foreach (var (upperKm, _) in Bands)
        {
            if (radiusKm <= upperKm) return upperKm;
        }

        return double.PositiveInfinity;
    }
}
