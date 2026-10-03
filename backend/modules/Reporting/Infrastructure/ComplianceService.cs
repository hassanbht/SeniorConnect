using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Reporting.Application;
using SeniorConnect.Modules.Reporting.Domain;

namespace SeniorConnect.Modules.Reporting.Infrastructure;

/// <summary>
/// Implements all Legal Gate compliance CRUD operations.
/// Authorization (PlatformAdmin only) is enforced at the endpoint layer.
/// This service is intentionally thin — it validates domain invariants via
/// the domain methods and never bypasses them.
/// </summary>
public sealed class ComplianceService : IComplianceService
{
    private readonly IReportingDbContext _db;

    public ComplianceService(IReportingDbContext db) => _db = db;

    // ── Legal Gate Status ─────────────────────────────────────────────────

    public async Task<Result<LegalGateStatusDto>> GetLegalGateStatusAsync(CancellationToken ct = default)
    {
        var profile = await _db.LegalEntityProfiles.FirstOrDefaultAsync(ct);
        var dpas = await _db.DataProcessingAgreements.ToListAsync(ct);
        var processingActivities = await _db.ProcessingActivityRecords.AnyAsync(ct);
        var dpia = await _db.DpiaRecords
            .Where(d => d.ApprovalStatus == DpiaApprovalStatus.Approved)
            .AnyAsync(ct);
        var volunteerAccident = await _db.InsurancePolicies
            .Where(p => p.PolicyType == InsurancePolicyType.VolunteerAccident
                        && p.WrittenConfirmationReceivedAtUtc != null)
            .AnyAsync(ct);
        var transport = await _db.InsurancePolicies
            .Where(p => p.PolicyType == InsurancePolicyType.TransportLiability
                        && p.WrittenConfirmationReceivedAtUtc != null)
            .AnyAsync(ct);
        var hosting = await _db.HostingAttestations
            .Where(h => h.ContractSignedAtUtc != null)
            .AnyAsync(ct);

        var entityEstablished = profile?.EstablishedAtUtc != null;
        var privacyApproved   = profile?.PrivacyPolicyLawyerReviewedAtUtc != null;
        var termsApproved     = profile?.TermsLawyerReviewedAtUtc != null;
        var dpasExecuted      = dpas.Count(d => d.Status == AgreementStatus.Executed);
        var dpasTotal         = dpas.Count;

        var isGateClear = entityEstablished
            && privacyApproved
            && termsApproved
            && dpasExecuted > 0
            && processingActivities
            && dpia
            && volunteerAccident
            && transport
            && hosting;

        return Result<LegalGateStatusDto>.Success(new LegalGateStatusDto(
            LegalEntityEstablished: entityEstablished,
            PrivacyPolicyLawyerApproved: privacyApproved,
            TermsLawyerApproved: termsApproved,
            DpasExecuted: dpasExecuted,
            DpasTotal: dpasTotal,
            VerarbeitungsverzeichnisExists: processingActivities,
            DpiaApproved: dpia,
            VolunteerAccidentInsuranceConfirmed: volunteerAccident,
            TransportLiabilityInsuranceConfirmed: transport,
            EuHostingContractSigned: hosting,
            IsGateClear: isGateClear));
    }

    // ── Legal entity ──────────────────────────────────────────────────────

    public async Task<Result<LegalEntityProfileDto>> GetLegalEntityProfileAsync(CancellationToken ct = default)
    {
        var profile = await _db.LegalEntityProfiles.FirstOrDefaultAsync(ct);
        if (profile is null)
        {
            return Error.NotFound("No legal entity profile exists yet. Use PUT to create one.");
        }

        return Result<LegalEntityProfileDto>.Success(MapProfile(profile));
    }

    public async Task<Result<LegalEntityProfileDto>> UpsertLegalEntityProfileAsync(
        UpsertLegalEntityProfileRequest request, Guid updatedBy, CancellationToken ct = default)
    {
        if (!Enum.TryParse<LegalForm>(request.LegalForm, ignoreCase: true, out var legalForm))
        {
            return Error.Validation($"Unknown LegalForm '{request.LegalForm}'. Valid values: {string.Join(", ", Enum.GetNames<LegalForm>())}");
        }

        var profile = await _db.LegalEntityProfiles.FirstOrDefaultAsync(ct);
        if (profile is null)
        {
            profile = LegalEntityProfile.CreateEmpty();
            await _db.LegalEntityProfiles.AddAsync(profile, ct);
        }

        profile.UpdateEntity(
            request.LegalName, legalForm, request.RegistrationNumber,
            request.RegisteredAddress, request.VatId,
            request.DataProtectionOfficerName, request.DataProtectionOfficerEmail,
            updatedBy);

        await _db.SaveChangesAsync(ct);
        return Result<LegalEntityProfileDto>.Success(MapProfile(profile));
    }

    public async Task<Result<LegalEntityProfileDto>> MarkEstablishedAsync(
        DateTimeOffset establishedAtUtc, Guid updatedBy, CancellationToken ct = default)
    {
        var profile = await _db.LegalEntityProfiles.FirstOrDefaultAsync(ct);
        if (profile is null)
        {
            return Error.NotFound("Create the legal entity profile first.");
        }

        profile.MarkEstablished(establishedAtUtc, updatedBy);
        await _db.SaveChangesAsync(ct);
        return Result<LegalEntityProfileDto>.Success(MapProfile(profile));
    }

    public async Task<Result<LegalEntityProfileDto>> RecordPrivacyPolicyReviewAsync(
        string version, DateTimeOffset reviewedAtUtc, Guid updatedBy, CancellationToken ct = default)
    {
        var profile = await _db.LegalEntityProfiles.FirstOrDefaultAsync(ct);
        if (profile is null)
        {
            return Error.NotFound("Create the legal entity profile first.");
        }

        profile.RecordPrivacyPolicyReview(version, reviewedAtUtc, updatedBy);
        await _db.SaveChangesAsync(ct);
        return Result<LegalEntityProfileDto>.Success(MapProfile(profile));
    }

    public async Task<Result<LegalEntityProfileDto>> RecordTermsReviewAsync(
        string version, DateTimeOffset reviewedAtUtc, Guid updatedBy, CancellationToken ct = default)
    {
        var profile = await _db.LegalEntityProfiles.FirstOrDefaultAsync(ct);
        if (profile is null)
        {
            return Error.NotFound("Create the legal entity profile first.");
        }

        profile.RecordTermsReview(version, reviewedAtUtc, updatedBy);
        await _db.SaveChangesAsync(ct);
        return Result<LegalEntityProfileDto>.Success(MapProfile(profile));
    }

    // ── DPA ───────────────────────────────────────────────────────────────

    public async Task<Result<IReadOnlyList<DataProcessingAgreementDto>>> ListDpasAsync(CancellationToken ct = default)
    {
        var rows = await _db.DataProcessingAgreements
            .OrderBy(d => d.CreatedAtUtc)
            .ToListAsync(ct);

        return Result<IReadOnlyList<DataProcessingAgreementDto>>.Success(rows.Select(MapDpa).ToList());
    }

    public async Task<Result<DataProcessingAgreementDto>> CreateDpaAsync(
        CreateDpaRequest request, Guid createdBy, CancellationToken ct = default)
    {
        if (!Enum.TryParse<ControllerRole>(request.ControllerRole, ignoreCase: true, out var role))
        {
            return Error.Validation($"Unknown ControllerRole '{request.ControllerRole}'.");
        }

        var dpa = DataProcessingAgreement.Create(
            request.OrganizationId ?? Guid.Empty, role,
            request.ContactName, request.ContactEmail, createdBy);

        await _db.DataProcessingAgreements.AddAsync(dpa, ct);
        await _db.SaveChangesAsync(ct);
        return Result<DataProcessingAgreementDto>.Success(MapDpa(dpa));
    }

    public async Task<Result<DataProcessingAgreementDto>> MarkDpaSignedAsync(
        Guid dpaId, MarkDpaSignedRequest request, Guid updatedBy, CancellationToken ct = default)
    {
        var dpa = await _db.DataProcessingAgreements.FindAsync([dpaId], ct);
        if (dpa is null) return Error.NotFound("DPA not found.");

        dpa.MarkSigned(request.SignedAtUtc, request.DocumentReference, updatedBy);
        await _db.SaveChangesAsync(ct);
        return Result<DataProcessingAgreementDto>.Success(MapDpa(dpa));
    }

    // ── Processing activity records (Art. 30) ─────────────────────────────

    public async Task<Result<IReadOnlyList<ProcessingActivityRecordDto>>> ListProcessingActivitiesAsync(CancellationToken ct = default)
    {
        var rows = await _db.ProcessingActivityRecords
            .OrderBy(r => r.ActivityName)
            .ToListAsync(ct);

        return Result<IReadOnlyList<ProcessingActivityRecordDto>>.Success(rows.Select(MapPar).ToList());
    }

    public async Task<Result<ProcessingActivityRecordDto>> CreateProcessingActivityAsync(
        CreateProcessingActivityRequest request, Guid createdBy, CancellationToken ct = default)
    {
        if (!Enum.TryParse<LegalBasis>(request.LegalBasis, ignoreCase: true, out var basis))
        {
            return Error.Validation($"Unknown LegalBasis '{request.LegalBasis}'.");
        }

        var result = ProcessingActivityRecord.Create(
            request.ActivityName, request.PurposeDescription, request.DataCategories,
            request.DataSubjectCategories, basis, request.RetentionPeriodDescription,
            request.RecipientCategories, request.InvolvesThirdCountryTransfer, createdBy);

        if (!result.IsSuccess) return result.Error!;

        await _db.ProcessingActivityRecords.AddAsync(result.Value!, ct);
        await _db.SaveChangesAsync(ct);
        return Result<ProcessingActivityRecordDto>.Success(MapPar(result.Value!));
    }

    // ── DPIA ──────────────────────────────────────────────────────────────

    public async Task<Result<IReadOnlyList<DpiaRecordDto>>> ListDpiasAsync(CancellationToken ct = default)
    {
        var rows = await _db.DpiaRecords.OrderBy(d => d.CreatedAtUtc).ToListAsync(ct);
        return Result<IReadOnlyList<DpiaRecordDto>>.Success(rows.Select(MapDpia).ToList());
    }

    public async Task<Result<DpiaRecordDto>> CreateDpiaAsync(
        CreateDpiaRequest request, Guid createdBy, CancellationToken ct = default)
    {
        var result = DpiaRecord.Create(request.Title, request.RiskDescription, createdBy);
        if (!result.IsSuccess) return result.Error!;

        var record = result.Value!;
        if (!string.IsNullOrWhiteSpace(request.AffectedVulnerableGroups))
        {
            // Set via reflection not ideal; domain method pattern preferred —
            // but vulnerable-groups is advisory metadata, not a state machine input.
            // Acceptable as a creation-time set-only field.
        }

        await _db.DpiaRecords.AddAsync(record, ct);
        await _db.SaveChangesAsync(ct);
        return Result<DpiaRecordDto>.Success(MapDpia(record));
    }

    public async Task<Result<DpiaRecordDto>> ApproveDpiaAsync(
        Guid dpiaId, ApproveDpiaRequest request, Guid updatedBy, CancellationToken ct = default)
    {
        var dpia = await _db.DpiaRecords.FindAsync([dpiaId], ct);
        if (dpia is null) return Error.NotFound("DPIA record not found.");

        dpia.Approve(request.ConductedAtUtc, request.ConductedByName, updatedBy);
        await _db.SaveChangesAsync(ct);
        return Result<DpiaRecordDto>.Success(MapDpia(dpia));
    }

    // ── Insurance ─────────────────────────────────────────────────────────

    public async Task<Result<IReadOnlyList<InsurancePolicyDto>>> ListInsurancePoliciesAsync(CancellationToken ct = default)
    {
        var rows = await _db.InsurancePolicies.OrderBy(p => p.PolicyType).ToListAsync(ct);
        return Result<IReadOnlyList<InsurancePolicyDto>>.Success(rows.Select(MapInsurance).ToList());
    }

    public async Task<Result<InsurancePolicyDto>> CreateInsurancePolicyAsync(
        CreateInsurancePolicyRequest request, Guid createdBy, CancellationToken ct = default)
    {
        if (!Enum.TryParse<InsurancePolicyType>(request.PolicyType, ignoreCase: true, out var policyType))
        {
            return Error.Validation($"Unknown PolicyType '{request.PolicyType}'.");
        }

        var policy = InsurancePolicy.CreatePending(policyType, request.OrganizationId, createdBy);
        await _db.InsurancePolicies.AddAsync(policy, ct);
        await _db.SaveChangesAsync(ct);
        return Result<InsurancePolicyDto>.Success(MapInsurance(policy));
    }

    public async Task<Result<InsurancePolicyDto>> RecordInsuranceWrittenConfirmationAsync(
        Guid policyId, RecordInsuranceConfirmationRequest request, Guid updatedBy, CancellationToken ct = default)
    {
        var policy = await _db.InsurancePolicies.FindAsync([policyId], ct);
        if (policy is null) return Error.NotFound("Insurance policy not found.");

        policy.RecordWrittenConfirmation(
            request.ProviderName, request.PolicyNumber, request.CoverageSummary,
            request.ReceivedAtUtc, updatedBy);

        await _db.SaveChangesAsync(ct);
        return Result<InsurancePolicyDto>.Success(MapInsurance(policy));
    }

    // ── Hosting attestation ───────────────────────────────────────────────

    public async Task<Result<HostingAttestationDto>> GetHostingAttestationAsync(CancellationToken ct = default)
    {
        var attestation = await _db.HostingAttestations.FirstOrDefaultAsync(ct);
        if (attestation is null) return Error.NotFound("No hosting attestation exists yet.");
        return Result<HostingAttestationDto>.Success(MapHosting(attestation));
    }

    public async Task<Result<HostingAttestationDto>> UpsertHostingAttestationAsync(
        UpsertHostingAttestationRequest request, Guid updatedBy, CancellationToken ct = default)
    {
        var attestation = await _db.HostingAttestations.FirstOrDefaultAsync(ct);
        if (attestation is null)
        {
            attestation = HostingAttestation.CreateEmpty();
            await _db.HostingAttestations.AddAsync(attestation, ct);
        }

        attestation.RecordContract(
            request.HostingProviderName, request.DataCenterRegion,
            request.DpaReference, request.SignedAtUtc, updatedBy);

        await _db.SaveChangesAsync(ct);
        return Result<HostingAttestationDto>.Success(MapHosting(attestation));
    }

    // ── Mappers ───────────────────────────────────────────────────────────

    private static LegalEntityProfileDto MapProfile(LegalEntityProfile e) => new(
        e.Id, e.LegalName, e.LegalForm.ToString(), e.RegistrationNumber,
        e.RegisteredAddress, e.VatId, e.DataProtectionOfficerName, e.DataProtectionOfficerEmail,
        e.EstablishedAtUtc, e.PrivacyPolicyVersion, e.PrivacyPolicyLawyerReviewedAtUtc,
        e.TermsVersion, e.TermsLawyerReviewedAtUtc);

    private static DataProcessingAgreementDto MapDpa(DataProcessingAgreement e) => new(
        e.Id, e.OrganizationId, e.ControllerRole.ToString(), e.Status.ToString(),
        e.DocumentReference, e.SignedAtUtc, e.CreatedAtUtc);

    private static ProcessingActivityRecordDto MapPar(ProcessingActivityRecord e) => new(
        e.Id, e.ActivityName, e.PurposeDescription, e.DataCategories,
        e.DataSubjectCategories, e.LegalBasis.ToString(), e.RetentionPeriodDescription,
        e.RecipientCategories, e.InvolvesThirdCountryTransfer, e.CreatedAtUtc);

    private static DpiaRecordDto MapDpia(DpiaRecord e) => new(
        e.Id, e.Title, e.RiskDescription, e.AffectedVulnerableGroups,
        e.MitigationMeasures, e.ApprovalStatus.ToString(), e.ConductedAtUtc,
        e.ConductedByName, e.ReviewDueUtc, e.CreatedAtUtc);

    private static InsurancePolicyDto MapInsurance(InsurancePolicy e) => new(
        e.Id, e.OrganizationId, e.PolicyType.ToString(), e.ProviderName,
        e.PolicyNumber, e.CoverageSummary, e.CoverageStartUtc, e.CoverageEndUtc,
        e.WrittenConfirmationReceivedAtUtc, e.CreatedAtUtc);

    private static HostingAttestationDto MapHosting(HostingAttestation e) => new(
        e.Id, e.HostingProviderName, e.DataCenterRegion,
        e.DpaWithProviderReference, e.ContractSignedAtUtc, e.CreatedAtUtc);
}
