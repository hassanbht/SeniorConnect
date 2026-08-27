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
