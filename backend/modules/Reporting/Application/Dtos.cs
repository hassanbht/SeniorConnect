namespace SeniorConnect.Modules.Reporting.Application;

public sealed record FunderMonthlyReportItemDto(
    Guid OrganizationId,
    DateOnly Month,
    string CategoryCode,
    string ActivityCount,
    string DistinctVolunteers,
    string DistinctPeopleSupported,
    string TotalHours,
    bool IsSuppressed);

public sealed record FunderSummaryDto(
    Guid FunderId,
    string FunderName,
    int TotalFundedOrganizations,
    string OverallHours,
    string OverallPeopleSupported);

public sealed record ImpactReportSummaryDto(
    Guid OrganizationId,
    DateOnly From,
    DateOnly To,
    int TotalActivities,
    double TotalHours,
    int ActiveVolunteersCount,
    int PeopleSupportedCount,
    IReadOnlyDictionary<string, double> HoursByCategory);

public sealed record CorporateEsgSummaryDto(
    Guid CompanyOrganizationId,
    string CompanyName,
    DateOnly From,
    DateOnly To,
    int ParticipatingEmployeesCount,
    double TotalVolunteerHours,
    int BeneficiariesSupportedCount,
    IReadOnlyDictionary<string, double> HoursByCategory,
    IReadOnlyList<string> SdgsImpacted,
    double EstimatedSocialValueEur);

public sealed record MultiOrgFunderDashboardDto(
    Guid FunderId,
    string FunderName,
    string RegionName,
    DateOnly From,
    DateOnly To,
    int ActiveOrganizationsCount,
    string TotalAggregatedHours,
    string TotalAggregatedBeneficiaries,
    IReadOnlyList<FunderMonthlyReportItemDto> OrganizationMetrics);

/// <summary>
/// P2-20: every figure is derived from recorded data. A figure the platform
/// does not record is <c>null</c> ("not available") — never estimated.
/// </summary>
public sealed record MetricWithGrowthDto(
    int CurrentValue,
    int PreviousYearValue,
    int Growth,
    string FormattedGrowth);

public sealed record AnnualStatisticsReportDto(
    Guid OrganizationId,
    string? OrganizationName,
    int Year,
    // Distinct volunteers with at least one confirmed activity in the year.
    MetricWithGrowthDto TotalVolunteers,
    // Not recorded by the platform → null.
    MetricWithGrowthDto? VolunteerPool,
    // Not recorded by the platform → null.
    MetricWithGrowthDto? NetworkPartners,
    // Confirmed activities in the year.
    MetricWithGrowthDto Placements,
    // Not recorded by the platform → null.
    MetricWithGrowthDto? InsuredPersons,
    // Not recorded by the Reporting module → null.
    MetricWithGrowthDto? EventsAndProjects,
    double TotalHours,
    int TotalActivities,
    IReadOnlyDictionary<string, double> HoursByCategory,
    DateTimeOffset GeneratedAtUtc);
