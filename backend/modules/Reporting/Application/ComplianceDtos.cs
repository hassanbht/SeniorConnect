using SeniorConnect.Modules.Reporting.Domain;

namespace SeniorConnect.Modules.Reporting.Application;

// DTO layer for the Legal Gate compliance endpoints.
// No PII from user records. These are operational/administrative records only.

// ── Summary ──────────────────────────────────────────────────────────────────

/// <summary>
/// One-stop status read for the Legal Gate dashboard:
/// which of the 8 mandatory Legal Gate items are complete.
/// An item is "complete" when its timestamp field is non-null.
/// </summary>
public sealed record LegalGateStatusDto(
    bool LegalEntityEstablished,
    bool PrivacyPolicyLawyerApproved,
    bool TermsLawyerApproved,
    int DpasExecuted,
    int DpasTotal,
    bool VerarbeitungsverzeichnisExists,
    bool DpiaApproved,
    bool VolunteerAccidentInsuranceConfirmed,
    bool TransportLiabilityInsuranceConfirmed,
    bool EuHostingContractSigned,
    /// <summary>True when every mandatory item is complete.</summary>
    bool IsGateClear);

// ── Legal entity ──────────────────────────────────────────────────────────────

public sealed record LegalEntityProfileDto(
    Guid Id,
    string? LegalName,
    string LegalForm,
    string? RegistrationNumber,
    string? RegisteredAddress,
    string? VatId,
    string? DataProtectionOfficerName,
    string? DataProtectionOfficerEmail,
    DateTimeOffset? EstablishedAtUtc,
    string? PrivacyPolicyVersion,
    DateTimeOffset? PrivacyPolicyLawyerReviewedAtUtc,
    string? TermsVersion,
    DateTimeOffset? TermsLawyerReviewedAtUtc);

public sealed record UpsertLegalEntityProfileRequest(
    string? LegalName,
    string LegalForm,
    string? RegistrationNumber,
    string? RegisteredAddress,
    string? VatId,
    string? DataProtectionOfficerName,
    string? DataProtectionOfficerEmail);

// ── DPA ───────────────────────────────────────────────────────────────────────

public sealed record DataProcessingAgreementDto(
    Guid Id,
    Guid? OrganizationId,
    string ControllerRole,
    string Status,
    string? DocumentReference,
    DateTimeOffset? SignedAtUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record CreateDpaRequest(
    Guid? OrganizationId,
    string ControllerRole,
    string? ContactName,
    string? ContactEmail);

public sealed record MarkDpaSignedRequest(
    DateTimeOffset SignedAtUtc,
    string? DocumentReference);

// ── Processing activity record (Verarbeitungsverzeichnis) ─────────────────────

public sealed record ProcessingActivityRecordDto(
    Guid Id,
    string ActivityName,
    string PurposeDescription,
    string DataCategories,
    string DataSubjectCategories,
    string LegalBasis,
    string RetentionPeriodDescription,
    string? RecipientCategories,
    bool InvolvesThirdCountryTransfer,
    DateTimeOffset CreatedAtUtc);

public sealed record CreateProcessingActivityRequest(
    string ActivityName,
    string PurposeDescription,
    string DataCategories,
    string DataSubjectCategories,
    string LegalBasis,
    string RetentionPeriodDescription,
    string? RecipientCategories,
    bool InvolvesThirdCountryTransfer);

// ── DPIA ──────────────────────────────────────────────────────────────────────

public sealed record DpiaRecordDto(
    Guid Id,
    string Title,
    string RiskDescription,
    string? AffectedVulnerableGroups,
    string? MitigationMeasures,
    string ApprovalStatus,
    DateTimeOffset? ConductedAtUtc,
    string? ConductedByName,
    DateTimeOffset? ReviewDueUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record CreateDpiaRequest(
    string Title,
    string RiskDescription,
    string? AffectedVulnerableGroups);

public sealed record ApproveDpiaRequest(
    DateTimeOffset ConductedAtUtc,
    string ConductedByName,
    string? MitigationMeasures,
    DateTimeOffset? ReviewDueUtc);

// ── Insurance ─────────────────────────────────────────────────────────────────

public sealed record InsurancePolicyDto(
    Guid Id,
    Guid? OrganizationId,
    string PolicyType,
    string? ProviderName,
    string? PolicyNumber,
    string? CoverageSummary,
    DateTimeOffset? CoverageStartUtc,
    DateTimeOffset? CoverageEndUtc,
    /// <summary>Non-null = Written confirmation received. This is what Legal Gate checks.</summary>
    DateTimeOffset? WrittenConfirmationReceivedAtUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record CreateInsurancePolicyRequest(
    string PolicyType,
    Guid? OrganizationId);

public sealed record RecordInsuranceConfirmationRequest(
    string ProviderName,
    string? PolicyNumber,
    string CoverageSummary,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset? CoverageStartUtc,
    DateTimeOffset? CoverageEndUtc);

// ── Hosting attestation ───────────────────────────────────────────────────────

public sealed record HostingAttestationDto(
    Guid Id,
    string? HostingProviderName,
    string? DataCenterRegion,
    string? DpaWithProviderReference,
    DateTimeOffset? ContractSignedAtUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record UpsertHostingAttestationRequest(
    string HostingProviderName,
    string DataCenterRegion,
    string? DpaReference,
    DateTimeOffset SignedAtUtc);
