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
