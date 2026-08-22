using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeniorConnect.Modules.TrustSafety.Domain;

namespace SeniorConnect.Modules.TrustSafety.Infrastructure;

public sealed class VolunteerApplicationConfiguration : IEntityTypeConfiguration<VolunteerApplication>
{
    public void Configure(EntityTypeBuilder<VolunteerApplication> builder)
    {
        builder.ToTable("volunteer_applications", t =>
        {
            t.HasCheckConstraint("ck_applications_status", "status IN ('open','approved','declined','withdrawn')");
            t.HasCheckConstraint("ck_applications_decline_reason", "status <> 'declined' OR decline_reason IS NOT NULL");
            t.HasCheckConstraint("ck_applications_decided_pair", "(decided_at_utc IS NULL) = (decided_by_user_id IS NULL)");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.OrganizationId).HasColumnName("organization_id");
        builder.Property(a => a.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(a => a.Status).HasColumnName("status").HasConversion<string>().HasDefaultValue(ApplicationStatus.Open).IsRequired();
        builder.Property(a => a.Motivation).HasColumnName("motivation");
        builder.Property(a => a.AppliedAtUtc).HasColumnName("applied_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(a => a.DecidedAtUtc).HasColumnName("decided_at_utc").HasColumnType("timestamptz");
        builder.Property(a => a.DecidedByUserId).HasColumnName("decided_by_user_id");
        builder.Property(a => a.DeclineReason).HasColumnName("decline_reason");

        builder.Property(a => a.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by");

        builder.Ignore(a => a.DomainEvents);
    }
}

public sealed class VolunteerApplicationStepConfiguration : IEntityTypeConfiguration<VolunteerApplicationStep>
{
    public void Configure(EntityTypeBuilder<VolunteerApplicationStep> builder)
    {
        builder.ToTable("volunteer_application_steps", t =>
        {
            t.HasCheckConstraint("ck_application_steps_step", "step IN ('interview','background_check','confidentiality_agreement','briefing','approval')");
            t.HasCheckConstraint("ck_application_steps_status", "status IN ('not_started','in_progress','completed','blocked','skipped')");
        });

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.ApplicationId).HasColumnName("application_id").IsRequired();
        builder.Property(s => s.Step).HasColumnName("step").HasConversion<string>().IsRequired();
        builder.Property(s => s.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<string>().HasDefaultValue(StepStatus.NotStarted).IsRequired();
        builder.Property(s => s.SlaDays).HasColumnName("sla_days").HasDefaultValue(14).IsRequired();
        builder.Property(s => s.OpenedAtUtc).HasColumnName("opened_at_utc").HasColumnType("timestamptz");
        builder.Property(s => s.CompletedAtUtc).HasColumnName("completed_at_utc").HasColumnType("timestamptz");
        builder.Property(s => s.CompletedByUserId).HasColumnName("completed_by_user_id");
        builder.Property(s => s.Note).HasColumnName("note");

        builder.Ignore(s => s.DomainEvents);
    }
}
