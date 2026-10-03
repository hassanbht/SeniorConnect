using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Organizations.Contracts;

/// <summary>
/// Explicit, server-side organization-staff authorization for every
/// org-scoped operation (docs/architecture/authorization.md §2).
///
///   no active membership in the org (or org unknown) → 404 NOT_FOUND
///   active member without the required role         → 403 CAPABILITY_MISSING
///
/// Tenant query filters narrow reads; they are not authorization. Every
/// coordinator / reporting / intake-form operation calls this guard.
/// </summary>
public interface IOrganizationAccessGuard
{
    /// <summary>Coordinator or Admin of the organization.</summary>
    Task<Result> EnsureStaffAsync(Guid userId, Guid organizationId, CancellationToken ct = default);

    /// <summary>Admin of the organization.</summary>
    Task<Result> EnsureAdminAsync(Guid userId, Guid organizationId, CancellationToken ct = default);
}

/// <summary>
/// Read-only organization facts other modules / endpoints need without
/// touching the Organizations tables directly.
/// </summary>
public interface IOrganizationDirectory
{
    /// <summary>Users holding an Active Volunteer membership — the org's roster.</summary>
    Task<IReadOnlyList<Guid>> GetActiveVolunteerUserIdsAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>BR-ROSTER-02: per-org thresholds (policy "roster.thresholds"), BR defaults otherwise.</summary>
    Task<RosterThresholds> GetRosterThresholdsAsync(Guid organizationId, CancellationToken ct = default);

    Task<string?> GetOrganizationNameAsync(Guid organizationId, CancellationToken ct = default);
}

public sealed record RosterThresholds(int ActiveDays, int DormantDays)
{
    /// <summary>BR-ROSTER-01 defaults: Active ≤ 60 days, Dormant 60–120, Inactive &gt; 120.</summary>
    public static readonly RosterThresholds Default = new(60, 120);

    /// <summary>Organization policy key holding {"activeDays":n,"dormantDays":m}.</summary>
    public const string PolicyKey = "roster.thresholds";
}

/// <summary>
/// BR-ROSTER-01 — single source of the roster status computation. Input is the
/// date of the volunteer's last COMPLETED (Confirmed) activity; Logged and
/// Disputed activities never count.
/// </summary>
public static class RosterStatusPolicy
{
    public const string NeverActivated = "NeverActivated";
    public const string Active = "Active";
    public const string Dormant = "Dormant";
    public const string Inactive = "Inactive";

    public static string Compute(DateOnly? lastCompletedOn, DateOnly today, RosterThresholds thresholds)
    {
        if (lastCompletedOn is null) return NeverActivated;

        var daysAgo = today.DayNumber - lastCompletedOn.Value.DayNumber;
        if (daysAgo <= thresholds.ActiveDays) return Active;
        return daysAgo <= thresholds.DormantDays ? Dormant : Inactive;
    }

    /// <summary>
    /// "Today" for an Austrian organization is the Europe/Vienna calendar date,
    /// not the UTC date (00:00–02:00 local is still "yesterday" in UTC).
    /// </summary>
    public static DateOnly TodayInVienna(DateTimeOffset? utcNow = null)
    {
        var now = utcNow ?? DateTimeOffset.UtcNow;
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, ViennaZone.Value).DateTime);
    }

    private static readonly Lazy<TimeZoneInfo> ViennaZone = new(() =>
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
        }
        catch (TimeZoneNotFoundException)
        {
            // Windows without ICU IANA support.
            return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        }
    });
}
