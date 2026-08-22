using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeniorConnect.Modules.HelpRequests.Domain;

namespace SeniorConnect.Modules.HelpRequests.Infrastructure;

public sealed class ActivityCategoryConfiguration : IEntityTypeConfiguration<ActivityCategory>
{
    public void Configure(EntityTypeBuilder<ActivityCategory> builder)
    {
        builder.ToTable("activity_categories", t =>
        {
            t.HasCheckConstraint("ck_activity_categories_safety", "default_safety_level BETWEEN 1 AND 5");
            t.HasCheckConstraint("ck_activity_categories_blocked_needs_referral", "NOT is_blocked OR referral_group IS NOT NULL");
        });

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Code).HasColumnName("code").IsRequired();
        builder.HasIndex(c => c.Code).IsUnique();

        builder.Property(c => c.NameKey).HasColumnName("name_key").IsRequired();
        builder.Property(c => c.DefaultSafetyLevel).HasColumnName("default_safety_level").HasDefaultValue(1).IsRequired();
        builder.Property(c => c.IsBlocked).HasColumnName("is_blocked").HasDefaultValue(false).IsRequired();
        builder.Property(c => c.ReferralGroup).HasColumnName("referral_group");
        builder.Property(c => c.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();

        builder.Ignore(c => c.DomainEvents);
    }
}

public sealed class ReferralDirectoryConfiguration : IEntityTypeConfiguration<ReferralDirectory>
{
    public void Configure(EntityTypeBuilder<ReferralDirectory> builder)
    {
        builder.ToTable("referral_directory");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.ReferralGroup).HasColumnName("referral_group").IsRequired();
        builder.Property(r => r.RegionCode).HasColumnName("region_code").IsRequired();
        builder.Property(r => r.Name).HasColumnName("name").IsRequired();
        builder.Property(r => r.Phone).HasColumnName("phone");
        builder.Property(r => r.Website).HasColumnName("website");
        builder.Property(r => r.Address).HasColumnName("address");
        builder.Property(r => r.NoteKey).HasColumnName("note_key");
        builder.Property(r => r.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();

        builder.HasIndex(r => new { r.ReferralGroup, r.RegionCode }).HasFilter("is_active");
        builder.Ignore(r => r.DomainEvents);
    }
}
