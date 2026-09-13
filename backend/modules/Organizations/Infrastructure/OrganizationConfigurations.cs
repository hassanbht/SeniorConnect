using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeniorConnect.Modules.Organizations.Domain;

namespace SeniorConnect.Modules.Organizations.Infrastructure;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations", t =>
        {
            t.HasCheckConstraint("ck_organizations_type", "type IN ('ngo','association','parish','company','other')");
            t.HasCheckConstraint("ck_organizations_status", "status IN ('active','suspended','archived')");
        });

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.Name).HasColumnName("name").IsRequired();
        builder.Property(o => o.LegalName).HasColumnName("legal_name");
        builder.Property(o => o.Type).HasColumnName("type").HasConversion<string>().IsRequired();
        builder.Property(o => o.Status).HasColumnName("status").HasConversion<string>().HasDefaultValue(OrganizationStatus.Active).IsRequired();
        builder.Property(o => o.SupportEmail).HasColumnName("support_email");
        builder.Property(o => o.SupportPhone).HasColumnName("support_phone");
        builder.Property(o => o.BrandingJson).HasColumnName("branding").HasColumnType("jsonb").HasDefaultValue("{}").IsRequired();

        builder.Property(o => o.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(o => o.CreatedBy).HasColumnName("created_by");
        builder.Property(o => o.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(o => o.UpdatedBy).HasColumnName("updated_by");
        builder.Property(o => o.IsDeleted).HasColumnName("is_deleted").IsRequired();
    }
}

public sealed class OrganizationBranchConfiguration : IEntityTypeConfiguration<OrganizationBranch>
{
    public void Configure(EntityTypeBuilder<OrganizationBranch> builder)
    {
        builder.ToTable("organization_branches", t =>
        {
            t.HasCheckConstraint("ck_branches_geo_pair", "(latitude IS NULL) = (longitude IS NULL)");
        });

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id");
        builder.Property(b => b.OrganizationId).HasColumnName("organization_id");
        builder.Property(b => b.Name).HasColumnName("name").IsRequired();
        builder.Property(b => b.Address).HasColumnName("address");
        builder.Property(b => b.PostalCode).HasColumnName("postal_code");
        builder.Property(b => b.City).HasColumnName("city");
        builder.Property(b => b.Latitude).HasColumnName("latitude");
        builder.Property(b => b.Longitude).HasColumnName("longitude");
        builder.Property(b => b.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();

        builder.Property(b => b.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(b => b.CreatedBy).HasColumnName("created_by");
        builder.Property(b => b.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(b => b.UpdatedBy).HasColumnName("updated_by");
    }
}

public sealed class OrganizationMembershipConfiguration : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
    {
        builder.ToTable("organization_memberships", t =>
        {
            t.HasCheckConstraint("ck_memberships_role", "role IN ('staff','coordinator','admin','safeguarding_officer','volunteer','client')");
            t.HasCheckConstraint("ck_memberships_status", "status IN ('invited','active','suspended','left')");
        });

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.OrganizationId).HasColumnName("organization_id");
        builder.Property(m => m.BranchId).HasColumnName("branch_id");
        builder.Property(m => m.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(m => m.Role).HasColumnName("role").HasConversion<string>().IsRequired();
        builder.Property(m => m.Status).HasColumnName("status").HasConversion<string>().HasDefaultValue(MembershipStatus.Invited).IsRequired();
        builder.Property(m => m.JoinedAtUtc).HasColumnName("joined_at_utc").HasColumnType("timestamptz");
        builder.Property(m => m.LeftAtUtc).HasColumnName("left_at_utc").HasColumnType("timestamptz");

        builder.Property(m => m.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(m => m.CreatedBy).HasColumnName("created_by");
        builder.Property(m => m.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(m => m.UpdatedBy).HasColumnName("updated_by");
    }
}

public sealed class OrganizationPolicyConfiguration : IEntityTypeConfiguration<OrganizationPolicy>
{
    public void Configure(EntityTypeBuilder<OrganizationPolicy> builder)
    {
        builder.ToTable("organization_policies");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.OrganizationId).HasColumnName("organization_id");
        builder.Property(p => p.PolicyKey).HasColumnName("policy_key").IsRequired();
        builder.Property(p => p.PolicyValueJson).HasColumnName("policy_value").HasColumnType("jsonb").IsRequired();
        builder.Property(p => p.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");
    }
}

public sealed class OrganizationIntakeFormConfiguration : IEntityTypeConfiguration<OrganizationIntakeForm>
{
    public void Configure(EntityTypeBuilder<OrganizationIntakeForm> builder)
    {
        builder.ToTable("organization_intake_forms", t =>
        {
            t.HasCheckConstraint("ck_intake_forms_type", "form_type IN ('volunteer','help_seeker')");
        });

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id");
        builder.Property(f => f.OrganizationId).HasColumnName("organization_id").IsRequired();
        builder.Property(f => f.FormType).HasColumnName("form_type").HasConversion<string>().IsRequired();
        builder.Property(f => f.Title).HasColumnName("title").IsRequired();
        builder.Property(f => f.Description).HasColumnName("description");
        builder.Property(f => f.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(f => f.Version).HasColumnName("version").HasDefaultValue(1).IsRequired();

        builder.Property(f => f.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(f => f.CreatedBy).HasColumnName("created_by");
        builder.Property(f => f.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(f => f.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(f => new { f.OrganizationId, f.FormType, f.IsActive })
            .HasDatabaseName("ix_intake_forms_org_type_active");
    }
}

public sealed class IntakeFormSectionConfiguration : IEntityTypeConfiguration<IntakeFormSection>
{
    public void Configure(EntityTypeBuilder<IntakeFormSection> builder)
    {
        builder.ToTable("intake_form_sections");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.FormId).HasColumnName("form_id").IsRequired();
        builder.Property(s => s.Title).HasColumnName("title").IsRequired();
        builder.Property(s => s.Description).HasColumnName("description");
        builder.Property(s => s.SortOrder).HasColumnName("sort_order").IsRequired();

        builder.Property(s => s.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(s => s.CreatedBy).HasColumnName("created_by");
        builder.Property(s => s.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(s => new { s.FormId, s.SortOrder })
            .HasDatabaseName("ix_intake_form_sections_form_order");
    }
}

public sealed class IntakeFormFieldConfiguration : IEntityTypeConfiguration<IntakeFormField>
{
    public void Configure(EntityTypeBuilder<IntakeFormField> builder)
    {
        builder.ToTable("intake_form_fields", t =>
        {
            t.HasCheckConstraint("ck_intake_fields_type", "field_type IN ('text','textarea','single_choice','multi_choice','boolean','date','time_slots')");
        });

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id");
        builder.Property(f => f.SectionId).HasColumnName("section_id").IsRequired();
        builder.Property(f => f.FieldKey).HasColumnName("field_key").IsRequired();
        builder.Property(f => f.LabelKey).HasColumnName("label_key").IsRequired();
        builder.Property(f => f.FieldType).HasColumnName("field_type").HasConversion<string>().IsRequired();
        builder.Property(f => f.IsRequired).HasColumnName("is_required").HasDefaultValue(false).IsRequired();
        builder.Property(f => f.OptionsJson).HasColumnName("options_json").HasColumnType("jsonb");
        builder.Property(f => f.SortOrder).HasColumnName("sort_order").IsRequired();

        builder.Property(f => f.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(f => f.CreatedBy).HasColumnName("created_by");
        builder.Property(f => f.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(f => f.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(f => new { f.SectionId, f.SortOrder })
            .HasDatabaseName("ix_intake_form_fields_section_order");
    }
}

public sealed class IntakeFormSubmissionConfiguration : IEntityTypeConfiguration<IntakeFormSubmission>
{
    public void Configure(EntityTypeBuilder<IntakeFormSubmission> builder)
    {
        builder.ToTable("intake_form_submissions", t =>
        {
            t.HasCheckConstraint("ck_submissions_status", "status IN ('draft','submitted','approved','declined')");
        });

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.FormId).HasColumnName("form_id").IsRequired();
        builder.Property(s => s.OrganizationId).HasColumnName("organization_id");
        builder.Property(s => s.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<string>().HasDefaultValue(SubmissionStatus.Submitted).IsRequired();
        builder.Property(s => s.SubmissionDataJson).HasColumnName("submission_data_json").HasColumnType("jsonb").IsRequired();
        builder.Property(s => s.CriminalClearanceDeclared).HasColumnName("criminal_clearance_declared").HasDefaultValue(false).IsRequired();
        builder.Property(s => s.CriminalClearanceDeclaredAtUtc).HasColumnName("criminal_clearance_declared_at_utc").HasColumnType("timestamptz");
        builder.Property(s => s.GdprConsentAccepted).HasColumnName("gdpr_consent_accepted").HasDefaultValue(false).IsRequired();
        builder.Property(s => s.GdprConsentAcceptedAtUtc).HasColumnName("gdpr_consent_accepted_at_utc").HasColumnType("timestamptz");
        builder.Property(s => s.EventInvitationOptIn).HasColumnName("event_invitation_opt_in").HasDefaultValue(false).IsRequired();
        builder.Property(s => s.SubmittedAtUtc).HasColumnName("submitted_at_utc").HasColumnType("timestamptz");
        builder.Property(s => s.DecidedAtUtc).HasColumnName("decided_at_utc").HasColumnType("timestamptz");
        builder.Property(s => s.DecidedByUserId).HasColumnName("decided_by_user_id");
        builder.Property(s => s.ReviewNotes).HasColumnName("review_notes");

        builder.Property(s => s.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(s => s.CreatedBy).HasColumnName("created_by");
        builder.Property(s => s.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(s => new { s.FormId, s.OrganizationId, s.UserId })
            .HasDatabaseName("ix_submissions_form_org_user");

        builder.HasIndex(s => new { s.OrganizationId, s.Status })
            .HasDatabaseName("ix_submissions_org_status");
    }
}
