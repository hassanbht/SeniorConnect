using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeniorConnect.Modules.Reporting.Domain;

namespace SeniorConnect.Modules.Reporting.Infrastructure;

public sealed class FunderConfiguration : IEntityTypeConfiguration<Domain.Funder>
{
    public void Configure(EntityTypeBuilder<Domain.Funder> builder)
    {
        builder.ToTable("funders", t =>
        {
            t.HasCheckConstraint("ck_funders_type", "type IN ('municipality','foundation','public_body','corporate')");
            t.HasCheckConstraint("ck_funders_status", "status IN ('active','suspended','ended')");
        });

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id");
        builder.Property(f => f.Name).HasColumnName("name").IsRequired();
        builder.Property(f => f.Type).HasColumnName("type").HasConversion<string>().IsRequired();
        builder.Property(f => f.ContactEmail).HasColumnName("contact_email");
        builder.Property(f => f.ContactPhone).HasColumnName("contact_phone");
        builder.Property(f => f.Status).HasColumnName("status").HasConversion<string>().HasDefaultValue(FunderStatus.Active).IsRequired();

        builder.Property(f => f.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(f => f.CreatedBy).HasColumnName("created_by");
        builder.Property(f => f.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(f => f.UpdatedBy).HasColumnName("updated_by");

        builder.Ignore(f => f.DomainEvents);
    }
}

public sealed class FundingRelationshipConfiguration : IEntityTypeConfiguration<FundingRelationship>
{
    public void Configure(EntityTypeBuilder<FundingRelationship> builder)
    {
        builder.ToTable("funding_relationships", t =>
        {
            t.HasCheckConstraint("ck_funding_dates", "valid_until IS NULL OR valid_until > valid_from");
        });

        builder.HasKey(fr => fr.Id);
        builder.Property(fr => fr.Id).HasColumnName("id");
        builder.Property(fr => fr.FunderId).HasColumnName("funder_id").IsRequired();
        builder.Property(fr => fr.OrganizationId).HasColumnName("organization_id").IsRequired();
        builder.Property(fr => fr.ValidFrom).HasColumnName("valid_from").IsRequired();
        builder.Property(fr => fr.ValidUntil).HasColumnName("valid_until");
        builder.Property(fr => fr.ReportingScopeJson).HasColumnName("reporting_scope").HasColumnType("jsonb").HasDefaultValue("{}").IsRequired();

        builder.Property(fr => fr.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(fr => fr.CreatedBy).HasColumnName("created_by");

        builder.Ignore(fr => fr.DomainEvents);
    }
}

public sealed class FunderMembershipConfiguration : IEntityTypeConfiguration<FunderMembership>
{
    public void Configure(EntityTypeBuilder<FunderMembership> builder)
    {
        builder.ToTable("funder_memberships", t =>
        {
            t.HasCheckConstraint("ck_funder_memberships_role", "role IN ('viewer','admin')");
            t.HasCheckConstraint("ck_funder_memberships_status", "status IN ('invited','active','suspended','left')");
        });

        builder.HasKey(fm => fm.Id);
        builder.Property(fm => fm.Id).HasColumnName("id");
        builder.Property(fm => fm.FunderId).HasColumnName("funder_id").IsRequired();
        builder.Property(fm => fm.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(fm => fm.Role).HasColumnName("role").HasConversion<string>().HasDefaultValue(FunderRole.Viewer).IsRequired();
        builder.Property(fm => fm.Status).HasColumnName("status").HasConversion<string>().HasDefaultValue(FunderMembershipStatus.Invited).IsRequired();
        builder.Property(fm => fm.JoinedAtUtc).HasColumnName("joined_at_utc").HasColumnType("timestamptz");

        builder.Ignore(fm => fm.DomainEvents);
    }
}

public sealed class FunderMonthlyReportViewConfiguration : IEntityTypeConfiguration<FunderMonthlyReportView>
{
    public void Configure(EntityTypeBuilder<FunderMonthlyReportView> builder)
    {
        builder.HasNoKey();
        builder.ToView("v_funder_monthly_report");

        builder.Property(v => v.FunderId).HasColumnName("funder_id");
        builder.Property(v => v.OrganizationId).HasColumnName("organization_id");
        builder.Property(v => v.Month).HasColumnName("month");
        builder.Property(v => v.CategoryCode).HasColumnName("category_code");
        builder.Property(v => v.ActivityCount).HasColumnName("activity_count");
        builder.Property(v => v.DistinctVolunteers).HasColumnName("distinct_volunteers");
        builder.Property(v => v.DistinctPeopleSupported).HasColumnName("distinct_people_supported");
        builder.Property(v => v.Hours).HasColumnName("hours");
        builder.Property(v => v.IsSuppressed).HasColumnName("is_suppressed");
    }
}

public sealed class VolunteerHoursViewConfiguration : IEntityTypeConfiguration<VolunteerHoursView>
{
    public void Configure(EntityTypeBuilder<VolunteerHoursView> builder)
    {
        builder.HasNoKey();
        builder.ToView("v_volunteer_hours");

        builder.Property(v => v.VolunteerUserId).HasColumnName("volunteer_user_id");
        builder.Property(v => v.OrganizationId).HasColumnName("organization_id");
        builder.Property(v => v.OccurredOn).HasColumnName("occurred_on");
        builder.Property(v => v.OccurredMonth).HasColumnName("occurred_month");
        builder.Property(v => v.ActivityCount).HasColumnName("activity_count");
        builder.Property(v => v.Minutes).HasColumnName("minutes");
        builder.Property(v => v.Hours).HasColumnName("hours");
    }
}
