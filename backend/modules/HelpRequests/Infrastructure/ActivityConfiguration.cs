using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeniorConnect.Modules.HelpRequests.Domain;

namespace SeniorConnect.Modules.HelpRequests.Infrastructure;

public sealed class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.ToTable("activities", t =>
        {
            // Mirrors the CHECK constraints in 002_phase2_wedge.sql. EF does not
            // generate these from the domain, so they are declared here and
            // verified by 900_schema_tests.sql against a real database.
            t.HasCheckConstraint("ck_activities_duration",
                "duration_minutes BETWEEN 1 AND 1440");

            // BR-TRANSPORT-04, backstop for the domain guard in Activity.Confirm.
            // Literal casing MUST match EF's default enum-to-string conversion
            // (the C# member name, e.g. "Confirmed") — a prior lowercase
            // version of this constraint never matched any real row and was
            // silently a no-op (found + fixed 2026-09).
            t.HasCheckConstraint("ck_activities_transport_insurance",
                "transport_mode <> 'VolunteerPrivateVehicle' "
                + "OR status <> 'Confirmed' "
                + "OR insurance_context <> 'Unknown'");

            t.HasCheckConstraint("ck_activities_distinct_parties",
                "subject_user_id IS NULL OR subject_user_id <> volunteer_user_id");
        });

        builder.HasKey(a => a.Id);

        // 2026-09 fix: every property below was previously unmapped and fell
        // through to EF's default PascalCase column names (e.g.
        // "DurationMinutes"), while the CHECK constraints above and the rest
        // of this schema assume snake_case (e.g. "duration_minutes"). That
        // mismatch meant every constraint on this table referenced a
        // nonexistent column — `dotnet ef database update` against a real
        // Postgres database would fail outright on this migration. EF's
        // InMemory test provider never surfaced it because it does not
        // create or check CHECK constraints at all.
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.OrganizationId).HasColumnName("organization_id");
        builder.Property(a => a.BranchId).HasColumnName("branch_id");
        builder.Property(a => a.VolunteerUserId).HasColumnName("volunteer_user_id");
        builder.Property(a => a.SubjectUserId).HasColumnName("subject_user_id");
        builder.Property(a => a.CategoryId).HasColumnName("category_id");
        builder.Property(a => a.HelpRequestId).HasColumnName("help_request_id");
        builder.Property(a => a.EventId).HasColumnName("event_id");
        builder.Property(a => a.OccurredOn).HasColumnName("occurred_on");
        builder.Property(a => a.DurationMinutes).HasColumnName("duration_minutes");
        builder.Property(a => a.LocationType).HasColumnName("location_type").HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(a => a.InsuranceContext).HasColumnName("insurance_context").HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.InsuranceDisclaimerAcceptedAtUtc).HasColumnName("insurance_disclaimer_accepted_at_utc");
        builder.Property(a => a.InsuranceDisclaimerAcceptedByUserId).HasColumnName("insurance_disclaimer_accepted_by_user_id");
        builder.Property(a => a.TransportMode).HasColumnName("transport_mode").HasConversion<string>().HasMaxLength(40);
        builder.Property(a => a.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.LoggedByUserId).HasColumnName("logged_by_user_id");
        builder.Property(a => a.LoggedAtUtc).HasColumnName("logged_at_utc");
        builder.Property(a => a.ConfirmedByUserId).HasColumnName("confirmed_by_user_id");
        builder.Property(a => a.ConfirmedAtUtc).HasColumnName("confirmed_at_utc");
        builder.Property(a => a.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by");
        builder.Property(a => a.IsDeleted).HasColumnName("is_deleted");

        // InvolvesTransport is derived from TransportMode in the domain, so it
        // is not persisted twice. The SQL column exists for the CHECK
        // constraint and for reporting; EF maps only the source of truth.
        builder.Ignore(a => a.InvolvesTransport);
        builder.Ignore(a => a.DomainEvents);

        builder.HasIndex(a => new { a.OrganizationId, a.OccurredOn })
               .HasFilter("status = 'Confirmed'");
        builder.HasIndex(a => new { a.VolunteerUserId, a.OccurredOn });
    }
}
