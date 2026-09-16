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

public sealed record MetricWithGrowthDto(
    int CurrentValue,
    int PreviousYearValue,
    int Growth,
    string FormattedGrowth);

public sealed record AnnualStatisticsReportDto(
    Guid OrganizationId,
    string OrganizationName,
    int Year,
    MetricWithGrowthDto TotalVolunteers,
    MetricWithGrowthDto VolunteerPool,
    MetricWithGrowthDto NetworkPartners,
    MetricWithGrowthDto Placements,
    MetricWithGrowthDto InsuredPersons,
    MetricWithGrowthDto EventsAndProjects,
    double TotalHours,
    int TotalActivities,
    IReadOnlyDictionary<string, double> HoursByCategory,
    DateTimeOffset GeneratedAtUtc);
