using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeniorConnect.Modules.Identity.Domain;

namespace SeniorConnect.Modules.Identity.Infrastructure;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", t =>
        {
            t.HasCheckConstraint("ck_users_status", "status IN ('active','suspended','deactivated','deleted')");
            t.HasCheckConstraint("ck_users_locale", "preferred_locale IN ('de','en','fa')");
            t.HasCheckConstraint("ck_users_contact", "email IS NOT NULL OR phone IS NOT NULL");
            t.HasCheckConstraint("ck_users_auth_method", "primary_auth_method IN ('phone_otp','email_magic_link','password')");
            t.HasCheckConstraint("ck_users_password_only_for_password_auth", "(primary_auth_method = 'password') = (password_hash IS NOT NULL)");
            t.HasCheckConstraint("ck_users_phone_auth_needs_phone", "primary_auth_method <> 'phone_otp' OR phone IS NOT NULL");
        });

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.Email).HasColumnName("email");
        builder.Property(u => u.EmailVerifiedAtUtc).HasColumnName("email_verified_at_utc").HasColumnType("timestamptz");
        builder.Property(u => u.Phone).HasColumnName("phone");
        builder.Property(u => u.PhoneVerifiedAtUtc).HasColumnName("phone_verified_at_utc").HasColumnType("timestamptz");
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash");
        builder.Property(u => u.PrimaryAuthMethod).HasColumnName("primary_auth_method").HasConversion<string>().HasDefaultValue(AuthMethod.PhoneOtp).IsRequired();
        builder.Property(u => u.DisplayName).HasColumnName("display_name").IsRequired();
        builder.Property(u => u.DateOfBirth).HasColumnName("date_of_birth");
        builder.Property(u => u.PreferredLocale).HasColumnName("preferred_locale").HasDefaultValue("de").IsRequired();
        builder.Property(u => u.SeniorModeDefault).HasColumnName("senior_mode_default").HasDefaultValue(false).IsRequired();
        builder.Property(u => u.Status).HasColumnName("status").HasConversion<string>().HasDefaultValue(UserStatus.Active).IsRequired();
        builder.Property(u => u.LastLoginAtUtc).HasColumnName("last_login_at_utc").HasColumnType("timestamptz");

        builder.Property(u => u.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();
        builder.Property(u => u.CreatedBy).HasColumnName("created_by");
        builder.Property(u => u.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();
        builder.Property(u => u.UpdatedBy).HasColumnName("updated_by");
        builder.Property(u => u.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();

        builder.HasIndex(u => u.Email)
            .HasDatabaseName("ux_users_email")
            .IsUnique()
            .HasFilter("email IS NOT NULL AND NOT is_deleted");

        builder.HasIndex(u => u.Phone)
            .HasDatabaseName("ux_users_phone")
            .IsUnique()
            .HasFilter("phone IS NOT NULL AND NOT is_deleted");
    }
}

public sealed class OtpChallengeConfiguration : IEntityTypeConfiguration<OtpChallenge>
{
    public void Configure(EntityTypeBuilder<OtpChallenge> builder)
    {
        builder.ToTable("otp_challenges", t =>
        {
            t.HasCheckConstraint("ck_otp_channel", "channel IN ('sms','email')");
            t.HasCheckConstraint("ck_otp_purpose", "purpose IN ('login','registration','phone_change','email_change','recovery')");
            t.HasCheckConstraint("ck_otp_attempts", "attempts <= max_attempts");
        });

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.UserId).HasColumnName("user_id");
        builder.Property(o => o.Channel).HasColumnName("channel").HasConversion<string>().IsRequired();
        builder.Property(o => o.DestinationHash).HasColumnName("destination_hash").IsRequired();
        builder.Property(o => o.CodeHash).HasColumnName("code_hash").IsRequired();
        builder.Property(o => o.Purpose).HasColumnName("purpose").HasConversion<string>().HasDefaultValue(OtpPurpose.Login).IsRequired();
        builder.Property(o => o.Attempts).HasColumnName("attempts").HasDefaultValue((short)0).IsRequired();
        builder.Property(o => o.MaxAttempts).HasColumnName("max_attempts").HasDefaultValue((short)5).IsRequired();
        builder.Property(o => o.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();
        builder.Property(o => o.ExpiresAtUtc).HasColumnName("expires_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(o => o.ConsumedAtUtc).HasColumnName("consumed_at_utc").HasColumnType("timestamptz");
        builder.Property(o => o.IpHash).HasColumnName("ip_hash");

        builder.HasIndex(o => new { o.DestinationHash, o.CreatedAtUtc })
            .HasDatabaseName("ix_otp_active")
            .HasFilter("consumed_at_utc IS NULL")
            .IsDescending(false, true);

        builder.HasIndex(o => o.ExpiresAtUtc)
            .HasDatabaseName("ix_otp_cleanup");
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(r => r.TokenHash).HasColumnName("token_hash").IsRequired();
        builder.Property(r => r.DeviceLabel).HasColumnName("device_label");
        builder.Property(r => r.IsPersonalDevice).HasColumnName("is_personal_device").HasDefaultValue(true).IsRequired();
        builder.Property(r => r.IssuedAtUtc).HasColumnName("issued_at_utc").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();
        builder.Property(r => r.ExpiresAtUtc).HasColumnName("expires_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(r => r.RevokedAtUtc).HasColumnName("revoked_at_utc").HasColumnType("timestamptz");
        builder.Property(r => r.ReplacedById).HasColumnName("replaced_by_id");

        builder.HasIndex(r => r.UserId)
            .HasDatabaseName("ix_refresh_tokens_user")
            .HasFilter("revoked_at_utc IS NULL");

        builder.HasIndex(r => r.TokenHash)
            .HasDatabaseName("ux_refresh_tokens_hash")
            .IsUnique();
    }
}

public sealed class UserCapabilityConfiguration : IEntityTypeConfiguration<UserCapability>
{
    public void Configure(EntityTypeBuilder<UserCapability> builder)
    {
        builder.ToTable("user_capabilities", t =>
        {
            t.HasCheckConstraint("ck_user_capabilities_source", "source IN ('derived','granted')");
            t.HasCheckConstraint("ck_user_capabilities_reason", "source = 'derived' OR reason IS NOT NULL");
        });

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(c => c.Capability).HasColumnName("capability").IsRequired();
        builder.Property(c => c.Source).HasColumnName("source").HasConversion<string>().HasDefaultValue(CapabilitySource.Derived).IsRequired();
        builder.Property(c => c.GrantedByUserId).HasColumnName("granted_by_user_id");
        builder.Property(c => c.GrantedByOrgId).HasColumnName("granted_by_org_id");
        builder.Property(c => c.Reason).HasColumnName("reason");
        builder.Property(c => c.GrantedAtUtc).HasColumnName("granted_at_utc").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();
        builder.Property(c => c.ExpiresAtUtc).HasColumnName("expires_at_utc").HasColumnType("timestamptz");

        builder.HasIndex(c => new { c.UserId, c.Capability, c.GrantedByOrgId })
            .HasDatabaseName("ux_user_capabilities")
            .IsUnique();

        builder.HasIndex(c => c.ExpiresAtUtc)
            .HasDatabaseName("ix_user_capabilities_expiring")
            .HasFilter("expires_at_utc IS NOT NULL");
    }
}

public sealed class VerificationConfiguration : IEntityTypeConfiguration<Verification>
{
    public void Configure(EntityTypeBuilder<Verification> builder)
    {
        builder.ToTable("verifications", t =>
        {
            t.HasCheckConstraint("ck_verifications_type", "type IN ('email','phone','identity','address','organization','training','background_check')");
            t.HasCheckConstraint("ck_verifications_status", "status IN ('not_started','pending','submitted','verified','rejected','expired')");
            t.HasCheckConstraint("ck_verifications_provider", "provider IN ('manual','organization','id_austria','kyc')");
            t.HasCheckConstraint("ck_verifications_verified_needs_date", "status <> 'verified' OR verified_at_utc IS NOT NULL");
        });

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id");
        builder.Property(v => v.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(v => v.Type).HasColumnName("type").HasConversion<string>().IsRequired();
        builder.Property(v => v.Status).HasColumnName("status").HasConversion<string>().HasDefaultValue(VerificationStatus.NotStarted).IsRequired();
        builder.Property(v => v.Provider).HasColumnName("provider").HasConversion<string>().HasDefaultValue(VerificationProvider.Manual).IsRequired();
        builder.Property(v => v.VerifiedByUserId).HasColumnName("verified_by_user_id");
        builder.Property(v => v.VerifiedByOrganizationId).HasColumnName("verified_by_organization_id");
        builder.Property(v => v.VerifiedAtUtc).HasColumnName("verified_at_utc").HasColumnType("timestamptz");
        builder.Property(v => v.ValidUntilUtc).HasColumnName("valid_until_utc").HasColumnType("timestamptz");
        builder.Property(v => v.ExternalReference).HasColumnName("external_reference");
        builder.Property(v => v.RejectionReason).HasColumnName("rejection_reason");

        builder.Property(v => v.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();
        builder.Property(v => v.CreatedBy).HasColumnName("created_by");
        builder.Property(v => v.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();
        builder.Property(v => v.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(v => new { v.UserId, v.Type, v.Status })
            .HasDatabaseName("ix_verifications_user_type");

        builder.HasIndex(v => v.ValidUntilUtc)
            .HasDatabaseName("ix_verifications_expiring")
            .HasFilter("status = 'verified' AND valid_until_utc IS NOT NULL");
    }
}

public sealed class TrustLevelSnapshotConfiguration : IEntityTypeConfiguration<TrustLevelSnapshot>
{
    public void Configure(EntityTypeBuilder<TrustLevelSnapshot> builder)
    {
        builder.ToTable("trust_level_snapshots", t =>
        {
            t.HasCheckConstraint("ck_trust_level_range", "level BETWEEN 0 AND 5");
        });

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(s => s.Level).HasColumnName("level").IsRequired();
        builder.Property(s => s.ReasonJson).HasColumnName("reason").HasColumnType("jsonb").HasDefaultValue("{}").IsRequired();
        builder.Property(s => s.ComputedAtUtc).HasColumnName("computed_at_utc").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();

        builder.HasIndex(s => new { s.UserId, s.ComputedAtUtc })
            .HasDatabaseName("ix_trust_snapshots_user")
            .IsDescending(false, true);
    }
}

public sealed class ConsentConfiguration : IEntityTypeConfiguration<Consent>
{
    public void Configure(EntityTypeBuilder<Consent> builder)
    {
        builder.ToTable("consents", t =>
        {
            t.HasCheckConstraint("ck_consents_type", "consent_type IN ('terms','privacy','notifications_push','notifications_sms','notifications_email','research_statistics')");
        });

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(c => c.ConsentType).HasColumnName("consent_type").HasConversion<string>().IsRequired();
        builder.Property(c => c.DocumentVersion).HasColumnName("document_version").IsRequired();
        builder.Property(c => c.Granted).HasColumnName("granted").IsRequired();
        builder.Property(c => c.GrantedAtUtc).HasColumnName("granted_at_utc").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();
        builder.Property(c => c.WithdrawnAtUtc).HasColumnName("withdrawn_at_utc").HasColumnType("timestamptz");
        builder.Property(c => c.IpHash).HasColumnName("ip_hash");

        builder.HasIndex(c => new { c.UserId, c.ConsentType, c.GrantedAtUtc })
            .HasDatabaseName("ix_consents_user")
            .IsDescending(false, false, true);
    }
}
