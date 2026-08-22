using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeniorConnect.Modules.Activities.Domain;

namespace SeniorConnect.Modules.Activities.Infrastructure;

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
            t.HasCheckConstraint("ck_activities_transport_insurance",
                "transport_mode <> 'volunteer_private_vehicle' "
                + "OR status <> 'confirmed' "
                + "OR insurance_context <> 'unknown'");

            t.HasCheckConstraint("ck_activities_distinct_parties",
                "subject_user_id IS NULL OR subject_user_id <> volunteer_user_id");
        });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.Source).HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.LocationType).HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.InsuranceContext).HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.TransportMode).HasConversion<string>().HasMaxLength(40);

        builder.Property(a => a.Notes).HasMaxLength(2000);

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
