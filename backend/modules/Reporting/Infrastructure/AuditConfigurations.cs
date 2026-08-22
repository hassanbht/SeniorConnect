using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeniorConnect.Modules.Reporting.Domain;

namespace SeniorConnect.Modules.Reporting.Infrastructure;

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(a => a.ActorOrganizationId).HasColumnName("actor_organization_id");
        builder.Property(a => a.Action).HasColumnName("action").IsRequired();
        builder.Property(a => a.SubjectType).HasColumnName("subject_type").IsRequired();
        builder.Property(a => a.SubjectId).HasColumnName("subject_id");
        builder.Property(a => a.Reason).HasColumnName("reason");
        builder.Property(a => a.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb").HasDefaultValue("{}").IsRequired();
        builder.Property(a => a.CorrelationId).HasColumnName("correlation_id");
        builder.Property(a => a.IpHash).HasColumnName("ip_hash");
        builder.Property(a => a.AtUtc).HasColumnName("at_utc").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();

        builder.HasIndex(a => new { a.SubjectType, a.SubjectId, a.AtUtc })
            .HasDatabaseName("ix_audit_subject")
            .IsDescending(false, false, true);

        builder.HasIndex(a => new { a.ActorUserId, a.AtUtc })
            .HasDatabaseName("ix_audit_actor")
            .IsDescending(false, true);

        builder.HasIndex(a => new { a.Action, a.AtUtc })
            .HasDatabaseName("ix_audit_action")
            .IsDescending(false, true);
    }
}
