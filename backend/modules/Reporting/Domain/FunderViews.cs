namespace SeniorConnect.Modules.Reporting.Domain;

/// <summary>
/// Keyless entity mapped to SQL View `v_funder_monthly_report`.
/// BR-FUNDER-02: Contains NO personal data, names, emails or addresses.
/// BR-FUNDER-03: Small cell cohort suppression (&lt;10) is applied.
/// </summary>
public sealed class FunderMonthlyReportView
{
    public Guid FunderId { get; set; }
    public Guid OrganizationId { get; set; }
    public DateOnly Month { get; set; }
    public string CategoryCode { get; set; } = null!;
    public int? ActivityCount { get; set; }
    public int? DistinctVolunteers { get; set; }
    public int? DistinctPeopleSupported { get; set; }
    public double? Hours { get; set; }
    public bool IsSuppressed { get; set; }
}

/// <summary>
/// Keyless entity mapped to SQL View `v_volunteer_hours`.
/// F1: Single source of truth for confirmed volunteer hours (non-deleted,
/// status Confirmed), one row per volunteer/org/day/subject/category.
/// </summary>
public sealed class VolunteerHoursView
{
    public Guid VolunteerUserId { get; set; }
    public Guid? OrganizationId { get; set; }

    /// <summary>Person supported. Staff-side reporting only — never on the funder surface.</summary>
    public Guid? SubjectUserId { get; set; }
    public string CategoryCode { get; set; } = null!;
    public DateOnly OccurredOn { get; set; }
    public DateOnly OccurredMonth { get; set; }
    public long ActivityCount { get; set; }
    public long Minutes { get; set; }
    public double Hours { get; set; }
}
