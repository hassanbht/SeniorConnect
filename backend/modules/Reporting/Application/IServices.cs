using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Reporting.Application;

/// <summary>ADR-017: callerUserId must hold an ACTIVE funder membership, else 404.</summary>
public interface IFunderService
{
    Task<Result<FunderSummaryDto>> GetFunderSummaryAsync(
        Guid callerUserId,
        Guid funderId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<FunderMonthlyReportItemDto>>> GetMonthlyReportsAsync(
        Guid callerUserId,
        Guid funderId,
        DateOnly? fromMonth = null,
        DateOnly? toMonth = null,
        CancellationToken cancellationToken = default);

    Task<Result<MultiOrgFunderDashboardDto>> GetMultiOrgDashboardAsync(
        Guid callerUserId,
        Guid funderId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}

public interface IReportingService
{
    Task<Result<ImpactReportSummaryDto>> GetImpactSummaryAsync(
        Guid organizationId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task<Result<byte[]>> ExportImpactCsvAsync(
        Guid organizationId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task<Result<byte[]>> ExportImpactPdfAsync(
        Guid organizationId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task<Result<byte[]>> ExportImpactXlsxAsync(
        Guid organizationId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task<Result<AnnualStatisticsReportDto>> GetAnnualReportAsync(
        Guid organizationId,
        int year,
        CancellationToken cancellationToken = default);

    Task<Result<byte[]>> ExportAnnualReportPdfAsync(
        Guid organizationId,
        int year,
        CancellationToken cancellationToken = default);
}

public interface IEsgReportingService
{
    Task<Result<CorporateEsgSummaryDto>> GetCorporateEsgSummaryAsync(
        Guid companyOrganizationId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task<Result<byte[]>> ExportEsgCertificatePdfAsync(
        Guid companyOrganizationId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
