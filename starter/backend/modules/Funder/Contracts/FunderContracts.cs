using SeniorConnect.SharedKernel;

namespace SeniorConnect.Modules.Funder.Contracts;

// =============================================================================
// ADR-017 / BR-FUNDER.
//
// Everything in this file is reachable by a funder principal. Therefore:
//
//   · every property is classified PublicProfile or Operational
//   · no property may be named after, or contain, an identifier
//   · a cohort below 10 is suppressed to null with IsSuppressed = true
//
// FunderApiSurfaceTests asserts all three. If you add a property here that
// carries personal data, the build fails. That is the point.
// =============================================================================

/// <summary>One row of the funder's impact dashboard. Aggregate only.</summary>
public sealed record FunderImpactRow(
    [property: DataClass(DataClass.Operational)] Guid OrganizationId,
    [property: DataClass(DataClass.Operational)] string OrganizationName,
    [property: DataClass(DataClass.Operational)] DateOnly Month,
    [property: DataClass(DataClass.Operational)] string CategoryCode,

    // Null when suppressed. BR-FUNDER-03.
    [property: DataClass(DataClass.Operational)] int? PeopleSupported,
    [property: DataClass(DataClass.Operational)] int? Volunteers,
    [property: DataClass(DataClass.Operational)] int? ActivityCount,
    [property: DataClass(DataClass.Operational)] decimal? Hours,

    [property: DataClass(DataClass.Operational)] bool IsSuppressed
) : IFunderVisible;

public sealed record FunderImpactReport(
    [property: DataClass(DataClass.Operational)] Guid FunderId,
    [property: DataClass(DataClass.Operational)] DateOnly From,
    [property: DataClass(DataClass.Operational)] DateOnly To,
    [property: DataClass(DataClass.Operational)] IReadOnlyList<FunderImpactRow> Rows,

    /// <summary>
    /// Shown verbatim in the UI so a council reader understands why some cells
    /// are empty: "Werte unter 10 Personen werden aus Datenschutzgründen nicht
    /// angezeigt."
    /// </summary>
    [property: DataClass(DataClass.Operational)] string SuppressionNoticeKey
) : IFunderVisible;

public static class FunderSuppression
{
    /// <summary>BR-FUNDER-03.</summary>
    public const int MinimumCohortSize = 10;

    public static FunderImpactRow Suppress(
        Guid organizationId,
        string organizationName,
        DateOnly month,
        string categoryCode,
        int rawPeople,
        int rawVolunteers,
        int rawActivities,
        decimal rawHours)
    {
        var suppressed = rawPeople < MinimumCohortSize
                      || rawVolunteers < MinimumCohortSize;

        // When suppressing, EVERY measure in the row goes — not just the
        // headcount. Leaving hours visible while hiding the count permits
        // inference. The API layer additionally suppresses complementary cells
        // so a small cohort cannot be recovered by subtracting from a total.
        return new FunderImpactRow(
            organizationId,
            organizationName,
            month,
            categoryCode,
            suppressed ? null : rawPeople,
            suppressed ? null : rawVolunteers,
            suppressed ? null : rawActivities,
            suppressed ? null : rawHours,
            suppressed);
    }
}
