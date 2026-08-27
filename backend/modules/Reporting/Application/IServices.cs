using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Reporting.Application;

public interface IFunderService
{
    Task<Result<FunderSummaryDto>> GetFunderSummaryAsync(
        Guid funderId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<FunderMonthlyReportItemDto>>> GetMonthlyReportsAsync(
        Guid funderId,
        DateOnly? fromMonth = null,
        DateOnly? toMonth = null,
        CancellationToken cancellationToken = default);

    Task<Result<MultiOrgFunderDashboardDto>> GetMultiOrgDashboardAsync(
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
