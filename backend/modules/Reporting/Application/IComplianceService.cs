using SeniorConnect.Domain;
using SeniorConnect.Modules.Reporting.Domain;

namespace SeniorConnect.Modules.Reporting.Application;

// P7-15 / Legal Gate — service interface for reading and updating compliance
// records. Only PlatformAdmin can call these; authorization is enforced at
// the endpoint layer and verified by ComplianceLegalGateTests.

public interface IComplianceService
{
    // ── Legal entity ──────────────────────────────────────────────────────
    Task<Result<LegalGateStatusDto>> GetLegalGateStatusAsync(CancellationToken ct = default);

    Task<Result<LegalEntityProfileDto>> GetLegalEntityProfileAsync(CancellationToken ct = default);

    Task<Result<LegalEntityProfileDto>> UpsertLegalEntityProfileAsync(
        UpsertLegalEntityProfileRequest request, Guid updatedBy, CancellationToken ct = default);

    Task<Result<LegalEntityProfileDto>> MarkEstablishedAsync(
        DateTimeOffset establishedAtUtc, Guid updatedBy, CancellationToken ct = default);

    Task<Result<LegalEntityProfileDto>> RecordPrivacyPolicyReviewAsync(
        string version, DateTimeOffset reviewedAtUtc, Guid updatedBy, CancellationToken ct = default);

    Task<Result<LegalEntityProfileDto>> RecordTermsReviewAsync(
        string version, DateTimeOffset reviewedAtUtc, Guid updatedBy, CancellationToken ct = default);

    // ── DPA ───────────────────────────────────────────────────────────────
    Task<Result<IReadOnlyList<DataProcessingAgreementDto>>> ListDpasAsync(CancellationToken ct = default);

    Task<Result<DataProcessingAgreementDto>> CreateDpaAsync(
        CreateDpaRequest request, Guid createdBy, CancellationToken ct = default);

    Task<Result<DataProcessingAgreementDto>> MarkDpaSignedAsync(
        Guid dpaId, MarkDpaSignedRequest request, Guid updatedBy, CancellationToken ct = default);

    // ── Verarbeitungsverzeichnis (Art. 30) ────────────────────────────────
    Task<Result<IReadOnlyList<ProcessingActivityRecordDto>>> ListProcessingActivitiesAsync(CancellationToken ct = default);

    Task<Result<ProcessingActivityRecordDto>> CreateProcessingActivityAsync(
        CreateProcessingActivityRequest request, Guid createdBy, CancellationToken ct = default);

    // ── DPIA ─────────────────────────────────────────────────────────────
    Task<Result<IReadOnlyList<DpiaRecordDto>>> ListDpiasAsync(CancellationToken ct = default);

    Task<Result<DpiaRecordDto>> CreateDpiaAsync(
        CreateDpiaRequest request, Guid createdBy, CancellationToken ct = default);

    Task<Result<DpiaRecordDto>> ApproveDpiaAsync(
        Guid dpiaId, ApproveDpiaRequest request, Guid updatedBy, CancellationToken ct = default);

    // ── Insurance ─────────────────────────────────────────────────────────
    Task<Result<IReadOnlyList<InsurancePolicyDto>>> ListInsurancePoliciesAsync(CancellationToken ct = default);

    Task<Result<InsurancePolicyDto>> CreateInsurancePolicyAsync(
        CreateInsurancePolicyRequest request, Guid createdBy, CancellationToken ct = default);

    Task<Result<InsurancePolicyDto>> RecordInsuranceWrittenConfirmationAsync(
        Guid policyId, RecordInsuranceConfirmationRequest request, Guid updatedBy, CancellationToken ct = default);

    // ── Hosting attestation ───────────────────────────────────────────────
    Task<Result<HostingAttestationDto>> GetHostingAttestationAsync(CancellationToken ct = default);

    Task<Result<HostingAttestationDto>> UpsertHostingAttestationAsync(
        UpsertHostingAttestationRequest request, Guid updatedBy, CancellationToken ct = default);
}
