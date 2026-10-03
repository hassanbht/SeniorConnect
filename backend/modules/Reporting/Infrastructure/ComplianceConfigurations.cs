using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeniorConnect.Modules.Reporting.Domain;

namespace SeniorConnect.Modules.Reporting.Infrastructure;

// P7-15 / Legal Gate — EF Core table mappings for all compliance schema entities.
// Each entity stays in the public schema alongside the rest of the platform;
// there is no dedicated compliance schema because these tables are operational
// records (platform admin fills them in as legal steps complete), not safeguarding
// data requiring schema-level isolation.

public sealed class LegalEntityProfileConfiguration : IEntityTypeConfiguration<LegalEntityProfile>
{
    public void Configure(EntityTypeBuilder<LegalEntityProfile> builder)
    {
        builder.ToTable("legal_entity_profiles");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.LegalName).HasColumnName("legal_name").HasMaxLength(300);
        builder.Property(e => e.LegalForm).HasColumnName("legal_form").HasConversion<string>().IsRequired();
        builder.Property(e => e.RegistrationNumber).HasColumnName("registration_number").HasMaxLength(100);
        builder.Property(e => e.RegisteredAddress).HasColumnName("registered_address").HasMaxLength(500);
        builder.Property(e => e.VatId).HasColumnName("vat_id").HasMaxLength(50);
        builder.Property(e => e.DataProtectionOfficerName).HasColumnName("dpo_name").HasMaxLength(200);
        builder.Property(e => e.DataProtectionOfficerEmail).HasColumnName("dpo_email").HasMaxLength(200);

        // The field the Legal Gate "Legal entity established" checkbox reads.
        builder.Property(e => e.EstablishedAtUtc).HasColumnName("established_at_utc").HasColumnType("timestamptz");

        builder.Property(e => e.PrivacyPolicyVersion).HasColumnName("privacy_policy_version").HasMaxLength(50);
        builder.Property(e => e.PrivacyPolicyLawyerReviewedAtUtc)
            .HasColumnName("privacy_policy_lawyer_reviewed_at_utc").HasColumnType("timestamptz");
        builder.Property(e => e.TermsVersion).HasColumnName("terms_version").HasMaxLength(50);
        builder.Property(e => e.TermsLawyerReviewedAtUtc)
            .HasColumnName("terms_lawyer_reviewed_at_utc").HasColumnType("timestamptz");

        builder.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.Ignore(e => e.DomainEvents);
    }
}

public sealed class DataProcessingAgreementConfiguration : IEntityTypeConfiguration<DataProcessingAgreement>
{
    public void Configure(EntityTypeBuilder<DataProcessingAgreement> builder)
    {
        builder.ToTable("data_processing_agreements", t =>
        {
            t.HasCheckConstraint(
                "ck_dpa_status",
                "status IN ('Draft','Sent','Executed','Expired')");
            t.HasCheckConstraint(
                "ck_dpa_controller_role",
                "controller_role IN ('PlatformIsController','PlatformIsProcessor','JointController')");
        });

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        // OrganizationId == null means platform-wide default DPA.
        builder.Property(e => e.OrganizationId).HasColumnName("organization_id");
        builder.Property(e => e.ControllerRole).HasColumnName("controller_role").HasConversion<string>().IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.CounterpartyContactName).HasColumnName("counterparty_contact_name").HasMaxLength(200);
        builder.Property(e => e.CounterpartyContactEmail).HasColumnName("counterparty_contact_email").HasMaxLength(200);
        builder.Property(e => e.DocumentReference).HasColumnName("document_reference").HasMaxLength(500);
        builder.Property(e => e.SignedAtUtc).HasColumnName("signed_at_utc").HasColumnType("timestamptz");

        builder.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => e.OrganizationId).HasDatabaseName("ix_dpa_organization_id");

        builder.Ignore(e => e.DomainEvents);
    }
}

public sealed class ProcessingActivityRecordConfiguration : IEntityTypeConfiguration<ProcessingActivityRecord>
{
    public void Configure(EntityTypeBuilder<ProcessingActivityRecord> builder)
    {
        // Verarbeitungsverzeichnis Art. 30 DSGVO
        builder.ToTable("processing_activity_records", t =>
        {
            t.HasCheckConstraint(
                "ck_par_legal_basis",
                "legal_basis IN ('Consent','Contract','LegitimateInterest','LegalObligation','VitalInterest')");
        });

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.ActivityName).HasColumnName("activity_name").HasMaxLength(300).IsRequired();
        builder.Property(e => e.PurposeDescription).HasColumnName("purpose_description").HasMaxLength(2000).IsRequired();
        builder.Property(e => e.DataCategories).HasColumnName("data_categories").HasMaxLength(1000).IsRequired();
        builder.Property(e => e.DataSubjectCategories).HasColumnName("data_subject_categories").HasMaxLength(500).IsRequired();
        builder.Property(e => e.LegalBasis).HasColumnName("legal_basis").HasConversion<string>().IsRequired();
        builder.Property(e => e.RetentionPeriodDescription).HasColumnName("retention_period_description").HasMaxLength(500).IsRequired();
        builder.Property(e => e.RecipientCategories).HasColumnName("recipient_categories").HasMaxLength(1000);
        builder.Property(e => e.InvolvesThirdCountryTransfer).HasColumnName("involves_third_country_transfer").IsRequired();

        builder.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.Ignore(e => e.DomainEvents);
    }
}

public sealed class DpiaRecordConfiguration : IEntityTypeConfiguration<DpiaRecord>
{
    public void Configure(EntityTypeBuilder<DpiaRecord> builder)
    {
        builder.ToTable("dpia_records", t =>
        {
            t.HasCheckConstraint(
                "ck_dpia_approval_status",
                "approval_status IN ('NotStarted','Draft','UnderReview','Approved')");
        });

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
        builder.Property(e => e.RiskDescription).HasColumnName("risk_description").HasMaxLength(5000).IsRequired();
        builder.Property(e => e.AffectedVulnerableGroups).HasColumnName("affected_vulnerable_groups").HasMaxLength(1000);
        builder.Property(e => e.MitigationMeasures).HasColumnName("mitigation_measures").HasMaxLength(5000);
        builder.Property(e => e.ApprovalStatus).HasColumnName("approval_status").HasConversion<string>().IsRequired();
        builder.Property(e => e.ConductedAtUtc).HasColumnName("conducted_at_utc").HasColumnType("timestamptz");
        builder.Property(e => e.ConductedByName).HasColumnName("conducted_by_name").HasMaxLength(200);
        builder.Property(e => e.ReviewDueUtc).HasColumnName("review_due_utc").HasColumnType("timestamptz");

        builder.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.Ignore(e => e.DomainEvents);
    }
}

public sealed class InsurancePolicyConfiguration : IEntityTypeConfiguration<InsurancePolicy>
{
    public void Configure(EntityTypeBuilder<InsurancePolicy> builder)
    {
        builder.ToTable("insurance_policies", t =>
        {
            t.HasCheckConstraint(
                "ck_insurance_policy_type",
                "policy_type IN ('VolunteerAccident','TransportLiability','GeneralLiability')");
        });

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        // OrganizationId == null = platform-wide policy (independent volunteers).
        builder.Property(e => e.OrganizationId).HasColumnName("organization_id");
        builder.Property(e => e.PolicyType).HasColumnName("policy_type").HasConversion<string>().IsRequired();
        builder.Property(e => e.ProviderName).HasColumnName("provider_name").HasMaxLength(200);
        builder.Property(e => e.PolicyNumber).HasColumnName("policy_number").HasMaxLength(100);
        builder.Property(e => e.CoverageSummary).HasColumnName("coverage_summary").HasMaxLength(2000);
        builder.Property(e => e.CoverageStartUtc).HasColumnName("coverage_start_utc").HasColumnType("timestamptz");
        builder.Property(e => e.CoverageEndUtc).HasColumnName("coverage_end_utc").HasColumnType("timestamptz");

        // This is the field the Legal Gate "Written confirmation received" checkbox reads
        // and the field that resolves the "Unknown" InsuranceContext on Activity/HelpRequest.
        builder.Property(e => e.WrittenConfirmationReceivedAtUtc)
            .HasColumnName("written_confirmation_received_at_utc").HasColumnType("timestamptz");

        builder.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(e => new { e.OrganizationId, e.PolicyType }).HasDatabaseName("ix_insurance_policy_org_type");

        builder.Ignore(e => e.DomainEvents);
    }
}

public sealed class HostingAttestationConfiguration : IEntityTypeConfiguration<HostingAttestation>
{
    public void Configure(EntityTypeBuilder<HostingAttestation> builder)
    {
        builder.ToTable("hosting_attestations");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.HostingProviderName).HasColumnName("hosting_provider_name").HasMaxLength(200);
        builder.Property(e => e.DataCenterRegion).HasColumnName("data_center_region").HasMaxLength(100);
        builder.Property(e => e.DpaWithProviderReference).HasColumnName("dpa_with_provider_reference").HasMaxLength(500);
        builder.Property(e => e.ContractSignedAtUtc).HasColumnName("contract_signed_at_utc").HasColumnType("timestamptz");

        builder.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");

        builder.Ignore(e => e.DomainEvents);
    }
}
