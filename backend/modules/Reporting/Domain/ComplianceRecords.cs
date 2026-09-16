using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Reporting.Domain;

// P7-15 / Legal Gate (BUILD-CHECKLIST.md, docs/decisions): these entities
// hold the FIELDS the Legal Gate needs, not the legal work itself. Every
// timestamp here starts null and stays null until a human actually does the
// corresponding real-world step (founding the entity, signing a DPA, an
// insurer confirming cover in writing). BR-GDPR-06 already says no real
// personal data may enter the system before these are filled in — this
// module is what makes that checkable instead of a paragraph in a markdown
// file nobody re-reads.

public enum LegalForm
{
    Unspecified,
    Einzelunternehmen,
    Verein,
    Gmbh,
    Other
}

public sealed class LegalEntityProfile : Entity, IAuditable
{
    private LegalEntityProfile() { }

    [DataClass(DataClass.Operational)]
    public string? LegalName { get; private set; }

    [DataClass(DataClass.Operational)]
    public LegalForm LegalForm { get; private set; } = LegalForm.Unspecified;

    /// <summary>Firmenbuchnummer (GmbH) or ZVR-Zahl (Verein) — whichever applies.</summary>
    [DataClass(DataClass.Operational)]
    public string? RegistrationNumber { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? RegisteredAddress { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? VatId { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? DataProtectionOfficerName { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? DataProtectionOfficerEmail { get; private set; }

    /// <summary>Null until the entity is actually founded. This is the field
    /// the Legal Gate's "Legal entity established" checkbox reads.</summary>
    [DataClass(DataClass.Operational)]
    public DateTimeOffset? EstablishedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? PrivacyPolicyVersion { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? PrivacyPolicyLawyerReviewedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? TermsVersion { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? TermsLawyerReviewedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static LegalEntityProfile CreateEmpty()
    {
        var now = DateTimeOffset.UtcNow;
        return new LegalEntityProfile { Id = Guid.CreateVersion7(), CreatedAtUtc = now, UpdatedAtUtc = now };
    }

    public void UpdateEntity(
        string? legalName, LegalForm legalForm, string? registrationNumber,
        string? registeredAddress, string? vatId,
        string? dpoName, string? dpoEmail, Guid? updatedBy)
    {
        LegalName = legalName;
        LegalForm = legalForm;
        RegistrationNumber = registrationNumber;
        RegisteredAddress = registeredAddress;
        VatId = vatId;
        DataProtectionOfficerName = dpoName;
        DataProtectionOfficerEmail = dpoEmail;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void MarkEstablished(DateTimeOffset establishedAtUtc, Guid? updatedBy)
    {
        EstablishedAtUtc = establishedAtUtc;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void RecordPrivacyPolicyReview(string version, DateTimeOffset reviewedAtUtc, Guid? updatedBy)
    {
        PrivacyPolicyVersion = version;
        PrivacyPolicyLawyerReviewedAtUtc = reviewedAtUtc;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void RecordTermsReview(string version, DateTimeOffset reviewedAtUtc, Guid? updatedBy)
    {
        TermsVersion = version;
        TermsLawyerReviewedAtUtc = reviewedAtUtc;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }
}

public enum ControllerRole
{
    PlatformIsController,
    PlatformIsProcessor,
    JointController
}

public enum AgreementStatus
{
    Draft,
    Sent,
    Executed,
    Expired
}

/// <summary>One row per partner Organization — the DPA the Legal Gate requires
/// before that org's staff or beneficiaries enter real personal data.</summary>
public sealed class DataProcessingAgreement : Entity, IOrganizationScoped, IAuditable
{
    private DataProcessingAgreement() { }

    [DataClass(DataClass.Operational)]
    public Guid? OrganizationId { get; private set; }

    [DataClass(DataClass.Operational)]
    public ControllerRole ControllerRole { get; private set; }

    [DataClass(DataClass.Operational)]
    public AgreementStatus Status { get; private set; } = AgreementStatus.Draft;

    [DataClass(DataClass.PersonalData)]
    public string? CounterpartyContactName { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? CounterpartyContactEmail { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? DocumentReference { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? SignedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static DataProcessingAgreement Create(
        Guid organizationId, ControllerRole role,
        string? contactName, string? contactEmail, Guid? createdBy)
    {
        var now = DateTimeOffset.UtcNow;
        return new DataProcessingAgreement
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = organizationId,
            ControllerRole = role,
            Status = AgreementStatus.Draft,
            CounterpartyContactName = contactName,
            CounterpartyContactEmail = contactEmail,
            CreatedAtUtc = now,
            CreatedBy = createdBy,
            UpdatedAtUtc = now,
            UpdatedBy = createdBy
        };
    }

    public void MarkSigned(DateTimeOffset signedAtUtc, string? documentReference, Guid? updatedBy)
    {
        Status = AgreementStatus.Executed;
        SignedAtUtc = signedAtUtc;
        DocumentReference = documentReference;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }
}

public enum LegalBasis
{
    Consent,
    Contract,
    LegitimateInterest,
    LegalObligation,
    VitalInterest
}

/// <summary>One row per processing purpose — the Verarbeitungsverzeichnis
/// (DSGVO Art. 30) the Legal Gate requires. A documentation artifact given a
/// schema, not a new business process.</summary>
public sealed class ProcessingActivityRecord : Entity, IAuditable
{
    private ProcessingActivityRecord() { }

    [DataClass(DataClass.Operational)]
    public string ActivityName { get; private set; } = string.Empty;

    [DataClass(DataClass.Operational)]
    public string PurposeDescription { get; private set; } = string.Empty;

    [DataClass(DataClass.Operational)]
    public string DataCategories { get; private set; } = string.Empty;

    [DataClass(DataClass.Operational)]
    public string DataSubjectCategories { get; private set; } = string.Empty;

    [DataClass(DataClass.Operational)]
    public LegalBasis LegalBasis { get; private set; }

    [DataClass(DataClass.Operational)]
    public string RetentionPeriodDescription { get; private set; } = string.Empty;

    [DataClass(DataClass.Operational)]
    public string? RecipientCategories { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool InvolvesThirdCountryTransfer { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static Result<ProcessingActivityRecord> Create(
        string activityName, string purposeDescription, string dataCategories,
        string dataSubjectCategories, LegalBasis legalBasis, string retentionPeriodDescription,
        string? recipientCategories, bool involvesThirdCountryTransfer, Guid? createdBy)
    {
        if (string.IsNullOrWhiteSpace(activityName))
        {
            return Error.Validation("Activity name is required.");
        }

        var now = DateTimeOffset.UtcNow;
        return Result<ProcessingActivityRecord>.Success(new ProcessingActivityRecord
        {
            Id = Guid.CreateVersion7(),
            ActivityName = activityName.Trim(),
            PurposeDescription = purposeDescription,
            DataCategories = dataCategories,
            DataSubjectCategories = dataSubjectCategories,
            LegalBasis = legalBasis,
            RetentionPeriodDescription = retentionPeriodDescription,
            RecipientCategories = recipientCategories,
            InvolvesThirdCountryTransfer = involvesThirdCountryTransfer,
            CreatedAtUtc = now,
            CreatedBy = createdBy,
            UpdatedAtUtc = now,
            UpdatedBy = createdBy
        });
    }
}

public enum DpiaApprovalStatus
{
    NotStarted,
    Draft,
    UnderReview,
    Approved
}

public sealed class DpiaRecord : Entity, IAuditable
{
    private DpiaRecord() { }

    [DataClass(DataClass.Operational)]
    public string Title { get; private set; } = string.Empty;

    [DataClass(DataClass.Operational)]
    public string RiskDescription { get; private set; } = string.Empty;

    [DataClass(DataClass.Operational)]
    public string? AffectedVulnerableGroups { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? MitigationMeasures { get; private set; }

    [DataClass(DataClass.Operational)]
    public DpiaApprovalStatus ApprovalStatus { get; private set; } = DpiaApprovalStatus.NotStarted;

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? ConductedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? ConductedByName { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? ReviewDueUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static Result<DpiaRecord> Create(string title, string riskDescription, Guid? createdBy)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Error.Validation("Title is required.");
        }

        var now = DateTimeOffset.UtcNow;
        return Result<DpiaRecord>.Success(new DpiaRecord
        {
            Id = Guid.CreateVersion7(),
            Title = title.Trim(),
            RiskDescription = riskDescription,
            ApprovalStatus = DpiaApprovalStatus.NotStarted,
            CreatedAtUtc = now,
            CreatedBy = createdBy,
            UpdatedAtUtc = now,
            UpdatedBy = createdBy
        });
    }

    public void Approve(DateTimeOffset conductedAtUtc, string conductedByName, Guid? updatedBy)
    {
        ApprovalStatus = DpiaApprovalStatus.Approved;
        ConductedAtUtc = conductedAtUtc;
        ConductedByName = conductedByName;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }
}

public enum InsurancePolicyType
{
    VolunteerAccident,
    TransportLiability,
    GeneralLiability
}

/// <summary>P0-02 / BR-TRANSPORT / BR-SAFETY-06: the written insurance answer
/// the Legal Gate and the transport-liability rule both depend on. A policy
/// with no WrittenConfirmationReceivedAtUtc is exactly the "Unknown"
/// insurance_context state that already blocks Level-3+ confirmation
/// elsewhere in the system — this table is what that state should resolve
/// against once a broker actually answers in writing.</summary>
public sealed class InsurancePolicy : Entity, IOrganizationScoped, IAuditable
{
    private InsurancePolicy() { }

    /// <summary>Null = platform-wide default policy, not tied to one organization.</summary>
    [DataClass(DataClass.Operational)]
    public Guid? OrganizationId { get; private set; }

    [DataClass(DataClass.Operational)]
    public InsurancePolicyType PolicyType { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? ProviderName { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? PolicyNumber { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? CoverageSummary { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? CoverageStartUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? CoverageEndUtc { get; private set; }

    /// <summary>The field the Legal Gate checkbox actually reads — not "do we
    /// have a broker call booked" but "did they confirm cover in writing".</summary>
    [DataClass(DataClass.Operational)]
    public DateTimeOffset? WrittenConfirmationReceivedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static InsurancePolicy CreatePending(
        InsurancePolicyType policyType, Guid? organizationId, Guid? createdBy)
    {
        var now = DateTimeOffset.UtcNow;
        return new InsurancePolicy
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = organizationId,
            PolicyType = policyType,
            CreatedAtUtc = now,
            CreatedBy = createdBy,
            UpdatedAtUtc = now,
            UpdatedBy = createdBy
        };
    }

    public void RecordWrittenConfirmation(
        string providerName, string? policyNumber, string coverageSummary,
        DateTimeOffset receivedAtUtc, Guid? updatedBy)
    {
        ProviderName = providerName;
        PolicyNumber = policyNumber;
        CoverageSummary = coverageSummary;
        WrittenConfirmationReceivedAtUtc = receivedAtUtc;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }
}

/// <summary>Singleton-ish record: the EU-hosting contractual attestation the
/// Legal Gate requires. Usually exactly one row.</summary>
public sealed class HostingAttestation : Entity, IAuditable
{
    private HostingAttestation() { }

    [DataClass(DataClass.Operational)]
    public string? HostingProviderName { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? DataCenterRegion { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? DpaWithProviderReference { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? ContractSignedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static HostingAttestation CreateEmpty()
    {
        var now = DateTimeOffset.UtcNow;
        return new HostingAttestation { Id = Guid.CreateVersion7(), CreatedAtUtc = now, UpdatedAtUtc = now };
    }

    public void RecordContract(
        string hostingProviderName, string dataCenterRegion,
        string? dpaReference, DateTimeOffset signedAtUtc, Guid? updatedBy)
    {
        HostingProviderName = hostingProviderName;
        DataCenterRegion = dataCenterRegion;
        DpaWithProviderReference = dpaReference;
        ContractSignedAtUtc = signedAtUtc;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }
}
