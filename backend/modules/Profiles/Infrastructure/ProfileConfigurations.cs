using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeniorConnect.Modules.Profiles.Domain;

namespace SeniorConnect.Modules.Profiles.Infrastructure;

public sealed class SupportProfileConfiguration : IEntityTypeConfiguration<SupportProfile>
{
    public void Configure(EntityTypeBuilder<SupportProfile> builder)
    {
        builder.ToTable("support_profiles", t =>
        {
            t.HasCheckConstraint("ck_support_contact_method", "preferred_contact_method IN ('app','phone','sms','family')");
            t.HasCheckConstraint("ck_support_vulnerability_reason", "NOT vulnerability_flag OR vulnerability_reason IS NOT NULL");
            t.HasCheckConstraint("ck_support_geo_pair", "(latitude IS NULL) = (longitude IS NULL)");
        });

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(s => s.AddressLine).HasColumnName("address_line");
        builder.Property(s => s.PostalCode).HasColumnName("postal_code");
        builder.Property(s => s.City).HasColumnName("city");
        builder.Property(s => s.Country).HasColumnName("country").HasDefaultValue("AT").IsRequired();
        builder.Property(s => s.Latitude).HasColumnName("latitude");
        builder.Property(s => s.Longitude).HasColumnName("longitude");
        builder.Property(s => s.MobilityNote).HasColumnName("mobility_note");
        builder.Property(s => s.LivingSituation).HasColumnName("living_situation");
        builder.Property(s => s.PreferredContactMethod).HasColumnName("preferred_contact_method").HasConversion<string>().HasDefaultValue(ContactMethod.App).IsRequired();
        builder.Property(s => s.VulnerabilityFlag).HasColumnName("vulnerability_flag").HasDefaultValue(false).IsRequired();
        builder.Property(s => s.VulnerabilitySetByUserId).HasColumnName("vulnerability_set_by_user_id");
        builder.Property(s => s.VulnerabilityReason).HasColumnName("vulnerability_reason");

        builder.Property(s => s.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();
        builder.Property(s => s.CreatedBy).HasColumnName("created_by");
        builder.Property(s => s.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(s => new { s.Latitude, s.Longitude })
            .HasDatabaseName("ix_support_profiles_geo")
            .HasFilter("latitude IS NOT NULL");

        builder.HasIndex(s => s.PostalCode)
            .HasDatabaseName("ix_support_profiles_postal");
    }
}

public sealed class VolunteerProfileConfiguration : IEntityTypeConfiguration<VolunteerProfile>
{
    public void Configure(EntityTypeBuilder<VolunteerProfile> builder)
    {
        builder.ToTable("volunteer_profiles", t =>
        {
            t.HasCheckConstraint("ck_volunteer_distance", "max_distance_km BETWEEN 1 AND 100");
            t.HasCheckConstraint("ck_volunteer_weekly", "max_activities_per_week BETWEEN 1 AND 40");
            t.HasCheckConstraint("ck_volunteer_reliability", "reliability_score IS NULL OR reliability_score BETWEEN 0 AND 1");
            t.HasCheckConstraint("ck_volunteer_geo_pair", "(latitude IS NULL) = (longitude IS NULL)");
        });

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id");
        builder.Property(v => v.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(v => v.Bio).HasColumnName("bio");
        builder.Property(v => v.PostalCode).HasColumnName("postal_code");
        builder.Property(v => v.Latitude).HasColumnName("latitude");
        builder.Property(v => v.Longitude).HasColumnName("longitude");
        builder.Property(v => v.MaxDistanceKm).HasColumnName("max_distance_km").HasDefaultValue(10).IsRequired();
        builder.Property(v => v.MaxActivitiesPerWeek).HasColumnName("max_activities_per_week").HasDefaultValue((short)3).IsRequired();
        builder.Property(v => v.HasCar).HasColumnName("has_car").HasDefaultValue(false).IsRequired();
        builder.Property(v => v.IsAcceptingRequests).HasColumnName("is_accepting_requests").HasDefaultValue(true).IsRequired();
        builder.Property(v => v.ReliabilityScore).HasColumnName("reliability_score").HasPrecision(4, 3);
        builder.Property(v => v.ActiveSinceUtc).HasColumnName("active_since_utc").HasColumnType("timestamptz");

        builder.Property(v => v.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();
        builder.Property(v => v.CreatedBy).HasColumnName("created_by");
        builder.Property(v => v.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();
        builder.Property(v => v.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(v => new { v.Latitude, v.Longitude })
            .HasDatabaseName("ix_volunteer_profiles_geo")
            .HasFilter("latitude IS NOT NULL AND is_accepting_requests");
    }
}

public sealed class InterestConfiguration : IEntityTypeConfiguration<Interest>
{
    public void Configure(EntityTypeBuilder<Interest> builder)
    {
        builder.ToTable("interests");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.Code).HasColumnName("code").IsRequired();
        builder.Property(i => i.NameKey).HasColumnName("name_key").IsRequired();
        builder.Property(i => i.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();

        builder.HasIndex(i => i.Code)
            .IsUnique();
    }
}

public sealed class LanguageConfiguration : IEntityTypeConfiguration<Language>
{
    public void Configure(EntityTypeBuilder<Language> builder)
    {
        builder.ToTable("languages");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.IsoCode).HasColumnName("iso_code").IsRequired();
        builder.Property(l => l.NameKey).HasColumnName("name_key").IsRequired();

        builder.HasIndex(l => l.IsoCode)
            .IsUnique();
    }
}

public sealed class UserLanguageConfiguration : IEntityTypeConfiguration<UserLanguage>
{
    public void Configure(EntityTypeBuilder<UserLanguage> builder)
    {
        builder.ToTable("user_languages", t =>
        {
            t.HasCheckConstraint("ck_user_languages_proficiency", "proficiency IN ('a1','a2','b1','b2','c1','c2','native')");
        });

        builder.HasKey(ul => new { ul.UserId, ul.LanguageId });
        builder.Property(ul => ul.UserId).HasColumnName("user_id");
        builder.Property(ul => ul.LanguageId).HasColumnName("language_id");
        builder.Property(ul => ul.Proficiency).HasColumnName("proficiency").HasConversion<string>().HasDefaultValue(LanguageProficiency.B1).IsRequired();
    }
}

public sealed class UserInterestConfiguration : IEntityTypeConfiguration<UserInterest>
{
    public void Configure(EntityTypeBuilder<UserInterest> builder)
    {
        builder.ToTable("user_interests");

        builder.HasKey(x => new { x.UserId, x.InterestId });
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.InterestId).HasColumnName("interest_id");
    }
}

public sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("skills");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.Code).HasColumnName("code").IsRequired();
        builder.Property(s => s.NameKey).HasColumnName("name_key").IsRequired();
        builder.Property(s => s.RequiresVerification).HasColumnName("requires_verification").HasDefaultValue(false).IsRequired();
        builder.Property(s => s.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();

        builder.HasIndex(s => s.Code)
            .IsUnique();
    }
}

public sealed class VolunteerSkillConfiguration : IEntityTypeConfiguration<VolunteerSkill>
{
    public void Configure(EntityTypeBuilder<VolunteerSkill> builder)
    {
        builder.ToTable("volunteer_skills");

        builder.HasKey(vs => new { vs.VolunteerProfileId, vs.SkillId });
        builder.Property(vs => vs.VolunteerProfileId).HasColumnName("volunteer_profile_id");
        builder.Property(vs => vs.SkillId).HasColumnName("skill_id");
        builder.Property(vs => vs.VerifiedAtUtc).HasColumnName("verified_at_utc").HasColumnType("timestamptz");
        builder.Property(vs => vs.VerifiedByOrgId).HasColumnName("verified_by_org_id");
    }
}

public sealed class AvailabilitySlotConfiguration : IEntityTypeConfiguration<AvailabilitySlot>
{
    public void Configure(EntityTypeBuilder<AvailabilitySlot> builder)
    {
        builder.ToTable("availability_slots", t =>
        {
            t.HasCheckConstraint("ck_availability_dow", "day_of_week BETWEEN 0 AND 6");
            t.HasCheckConstraint("ck_availability_order", "start_time < end_time");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(a => a.DayOfWeek).HasColumnName("day_of_week").IsRequired();
        builder.Property(a => a.StartTime).HasColumnName("start_time").HasColumnType("time").IsRequired();
        builder.Property(a => a.EndTime).HasColumnName("end_time").HasColumnType("time").IsRequired();
        builder.Property(a => a.ValidFrom).HasColumnName("valid_from").HasColumnType("date");
        builder.Property(a => a.ValidUntil).HasColumnName("valid_until").HasColumnType("date");

        builder.HasIndex(a => new { a.UserId, a.DayOfWeek })
            .HasDatabaseName("ix_availability_user");
    }
}
