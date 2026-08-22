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
