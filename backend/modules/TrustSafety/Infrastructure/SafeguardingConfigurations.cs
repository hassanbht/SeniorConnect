using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeniorConnect.Modules.TrustSafety.Domain;

namespace SeniorConnect.Modules.TrustSafety.Infrastructure;

public sealed class SafeguardingCaseConfiguration : IEntityTypeConfiguration<SafeguardingCase>
{
    public void Configure(EntityTypeBuilder<SafeguardingCase> builder)
    {
        builder.ToTable("cases", "safeguarding");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.OrganizationId).HasColumnName("organization_id");
        builder.Property(c => c.SubjectUserId).HasColumnName("subject_user_id").IsRequired();
        builder.Property(c => c.ReporterUserId).HasColumnName("reporter_user_id").IsRequired();
        builder.Property(c => c.Severity).HasColumnName("severity").HasConversion<string>().IsRequired();
        builder.Property(c => c.Category).HasColumnName("category").HasMaxLength(100).IsRequired();
        builder.Property(c => c.Summary).HasColumnName("summary").IsRequired();
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(c => c.AssignedOfficerUserId).HasColumnName("assigned_officer_user_id");
        builder.Property(c => c.ResolutionNotes).HasColumnName("resolution_notes");
        builder.Property(c => c.ResolvedAtUtc).HasColumnName("resolved_at_utc").HasColumnType("timestamptz");
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by");

        builder.Ignore(c => c.DomainEvents);
    }
}

public sealed class SafeguardingCaseNoteConfiguration : IEntityTypeConfiguration<SafeguardingCaseNote>
{
    public void Configure(EntityTypeBuilder<SafeguardingCaseNote> builder)
    {
        builder.ToTable("case_notes", "safeguarding");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasColumnName("id");
        builder.Property(n => n.CaseId).HasColumnName("case_id").IsRequired();
        builder.Property(n => n.AuthorOfficerUserId).HasColumnName("author_officer_user_id").IsRequired();
        builder.Property(n => n.NoteText).HasColumnName("note_text").IsRequired();
        builder.Property(n => n.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();

        builder.Ignore(n => n.DomainEvents);
    }
}

public sealed class SafeguardingAccessLogConfiguration : IEntityTypeConfiguration<SafeguardingAccessLog>
{
    public void Configure(EntityTypeBuilder<SafeguardingAccessLog> builder)
    {
        builder.ToTable("access_logs", "safeguarding");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.CaseId).HasColumnName("case_id").IsRequired();
        builder.Property(l => l.AccessedByUserId).HasColumnName("accessed_by_user_id").IsRequired();
        builder.Property(l => l.Action).HasColumnName("action").HasMaxLength(50).IsRequired();
        builder.Property(l => l.AccessedAtUtc).HasColumnName("accessed_at_utc").HasColumnType("timestamptz").IsRequired();

        builder.Ignore(l => l.DomainEvents);
    }
}

/// <summary>Phase 4: safeguarding restrictions live in the isolated schema, next to their case.</summary>
public sealed class SafeguardingRestrictionConfiguration : IEntityTypeConfiguration<SafeguardingRestriction>
{
    public void Configure(EntityTypeBuilder<SafeguardingRestriction> builder)
    {
        builder.ToTable("restrictions", "safeguarding", t =>
        {
            t.HasCheckConstraint("ck_restrictions_any_kind",
                "blocks_discovery OR blocks_location OR blocks_contact OR blocks_assignment");
            t.HasCheckConstraint("ck_restrictions_lifted_needs_reason",
                "lifted_at_utc IS NULL OR (lifted_by_officer_user_id IS NOT NULL AND lift_reason IS NOT NULL)");
        });

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.CaseId).HasColumnName("case_id").IsRequired();
        builder.Property(r => r.SubjectUserId).HasColumnName("subject_user_id").IsRequired();
        builder.Property(r => r.CounterpartUserId).HasColumnName("counterpart_user_id");
        builder.Property(r => r.BlocksDiscovery).HasColumnName("blocks_discovery").IsRequired();
        builder.Property(r => r.BlocksLocation).HasColumnName("blocks_location").IsRequired();
        builder.Property(r => r.BlocksContact).HasColumnName("blocks_contact").IsRequired();
        builder.Property(r => r.BlocksAssignment).HasColumnName("blocks_assignment").IsRequired();
        builder.Property(r => r.CreatedByOfficerUserId).HasColumnName("created_by_officer_user_id").IsRequired();
        builder.Property(r => r.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(r => r.LiftedAtUtc).HasColumnName("lifted_at_utc").HasColumnType("timestamptz");
        builder.Property(r => r.LiftedByOfficerUserId).HasColumnName("lifted_by_officer_user_id");
        builder.Property(r => r.LiftReason).HasColumnName("lift_reason");

        builder.HasOne<SafeguardingCase>().WithMany().HasForeignKey(r => r.CaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => r.SubjectUserId).HasDatabaseName("ix_restrictions_active_subject").HasFilter("lifted_at_utc IS NULL");
        builder.HasIndex(r => r.CounterpartUserId).HasDatabaseName("ix_restrictions_active_counterpart").HasFilter("lifted_at_utc IS NULL AND counterpart_user_id IS NOT NULL");

        builder.Ignore(r => r.DomainEvents);
    }
}

/// <summary>
/// Phase 4: blocks had no configuration. The table name and columns are kept
/// as they are; the pair becomes unique so blocking twice is idempotent.
/// </summary>
public sealed class UserBlockConfiguration : IEntityTypeConfiguration<UserBlock>
{
    public void Configure(EntityTypeBuilder<UserBlock> builder)
    {
        builder.HasIndex(b => new { b.BlockingUserId, b.BlockedUserId }).IsUnique().HasDatabaseName("ux_user_blocks_pair");
        builder.HasIndex(b => b.BlockedUserId).HasDatabaseName("ix_user_blocks_blocked");
    }
}

/// <summary>
/// S-2 (Option B, 2026-09-30): content-free operational task. Lives in the
/// safeguarding schema because it is created by the safeguarding flow, but it
/// carries no case reference, reason or restriction kind (BR-SG-05).
/// </summary>
public sealed class OperationalTaskConfiguration : IEntityTypeConfiguration<OperationalTask>
{
    public void Configure(EntityTypeBuilder<OperationalTask> builder)
    {
        builder.ToTable("operational_tasks", "safeguarding");

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.OrganizationId).HasColumnName("organization_id");
        builder.Property(t => t.SubjectUserId).HasColumnName("subject_user_id").IsRequired();
        builder.Property(t => t.TaskKind).HasColumnName("task_kind").HasMaxLength(60).IsRequired();
        builder.Property(t => t.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(t => t.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(t => t.CompletedAtUtc).HasColumnName("completed_at_utc").HasColumnType("timestamptz");
        builder.Property(t => t.CompletedByUserId).HasColumnName("completed_by_user_id");
        builder.Property(t => t.CancelledAtUtc).HasColumnName("cancelled_at_utc").HasColumnType("timestamptz");
        builder.Property(t => t.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").IsRequired();

        // S-2: at most one OPEN task per (organization, subject) — idempotent creation.
        builder.HasIndex(t => new { t.OrganizationId, t.SubjectUserId, t.CompletedAtUtc, t.CancelledAtUtc })
            .HasFilter("completed_at_utc IS NULL AND cancelled_at_utc IS NULL")
            .HasDatabaseName("ix_operational_tasks_open_per_subject");
    }
}
