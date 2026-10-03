using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeniorConnect.Modules.Identity.Domain;

namespace SeniorConnect.Modules.Identity.Infrastructure;

public sealed class UserExternalLoginConfiguration : IEntityTypeConfiguration<UserExternalLogin>
{
    public void Configure(EntityTypeBuilder<UserExternalLogin> builder)
    {
        builder.ToTable("user_external_logins", t =>
        {
            t.HasCheckConstraint("ck_user_external_logins_provider", "provider IN ('google','id_austria')");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.Provider).HasColumnName("provider").IsRequired();
        builder.Property(x => x.ProviderKey).HasColumnName("provider_key").IsRequired();
        builder.Property(x => x.Email).HasColumnName("email");
        builder.Property(x => x.DisplayName).HasColumnName("display_name");
        builder.Property(x => x.LinkedAtUtc).HasColumnName("linked_at_utc").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();

        builder.HasIndex(x => new { x.Provider, x.ProviderKey })
            .HasDatabaseName("ux_user_external_logins_provider_key")
            .IsUnique();

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("ix_user_external_logins_user");
    }
}

public sealed class EmailVerificationTokenConfiguration : IEntityTypeConfiguration<EmailVerificationToken>
{
    public void Configure(EntityTypeBuilder<EmailVerificationToken> builder)
    {
        builder.ToTable("email_verification_tokens");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").IsRequired();
        builder.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.UsedAtUtc).HasColumnName("used_at_utc").HasColumnType("timestamptz");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("ix_email_verification_tokens_user")
            .HasFilter("used_at_utc IS NULL");
    }
}

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", t =>
        {
            t.HasCheckConstraint("ck_users_status", "status IN ('Active','Suspended','Deactivated','Deleted')");
            t.HasCheckConstraint("ck_users_locale", "preferred_locale IN ('de','en','fa')");
            t.HasCheckConstraint("ck_users_contact", "email IS NOT NULL OR phone IS NOT NULL");
            t.HasCheckConstraint("ck_users_auth_method", "primary_auth_method IN ('PhoneOtp','EmailMagicLink','Password','Google','IdAustria','EmailPassword')");
            t.HasCheckConstraint("ck_users_password_only_for_password_auth", "(primary_auth_method IN ('Password','EmailPassword')) = (password_hash IS NOT NULL)");
            t.HasCheckConstraint("ck_users_phone_auth_needs_phone", "primary_auth_method <> 'PhoneOtp' OR phone IS NOT NULL");
        });

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.Email).HasColumnName("email");
        builder.Property(u => u.EmailVerifiedAtUtc).HasColumnName("email_verified_at_utc").HasColumnType("timestamptz");
        builder.Property(u => u.Phone).HasColumnName("phone");
        builder.Property(u => u.PhoneVerifiedAtUtc).HasColumnName("phone_verified_at_utc").HasColumnType("timestamptz");
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash");
        builder.Property(u => u.TotpSecret).HasColumnName("totp_secret");
        builder.Property(u => u.TotpEnabledAtUtc).HasColumnName("totp_enabled_at_utc").HasColumnType("timestamptz");
        builder.Property(u => u.PrimaryAuthMethod).HasColumnName("primary_auth_method").HasConversion<string>().HasDefaultValue(AuthMethod.PhoneOtp).IsRequired();
        builder.Property(u => u.DisplayName).HasColumnName("display_name").IsRequired();
        builder.Property(u => u.PhotoUrl).HasColumnName("photo_url");
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
            t.HasCheckConstraint("ck_otp_channel", "channel IN ('Sms','Email')");
            t.HasCheckConstraint("ck_otp_purpose", "purpose IN ('Login','Registration','PhoneChange','EmailChange','Recovery','PhoneVerification')");
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
            t.HasCheckConstraint("ck_user_capabilities_source", "source IN ('Derived','Granted')");
            t.HasCheckConstraint("ck_user_capabilities_reason", "source = 'Derived' OR reason IS NOT NULL");
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
            t.HasCheckConstraint("ck_verifications_type", "type IN ('Email','Phone','Identity','Address','Organization','Training','BackgroundCheck')");
            t.HasCheckConstraint("ck_verifications_status", "status IN ('NotStarted','Pending','Submitted','Verified','Rejected','Expired','Revoked')");
            // Phase 4: was lowercase ('manual', ...) while EF writes the enum
            // name ('Manual'), so no verification row could ever be inserted
            // on PostgreSQL. Synced to the stored values.
            t.HasCheckConstraint("ck_verifications_provider", "provider IN ('Manual','Organization','IdAustria','Kyc')");
            t.HasCheckConstraint("ck_verifications_method", "method IS NULL OR method IN ('DocumentSighted','OrganizationRecord','OrganizationMembership')");
            t.HasCheckConstraint("ck_verifications_revoked_needs_date", "status <> 'Revoked' OR revoked_at_utc IS NOT NULL");
            t.HasCheckConstraint("ck_verifications_verified_needs_date", "status <> 'Verified' OR verified_at_utc IS NOT NULL");
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
        builder.Property(v => v.Method).HasColumnName("method").HasConversion<string>();
        builder.Property(v => v.RevokedAtUtc).HasColumnName("revoked_at_utc").HasColumnType("timestamptz");
        builder.Property(v => v.RevokedByUserId).HasColumnName("revoked_by_user_id");
        builder.Property(v => v.RevocationReason).HasColumnName("revocation_reason");

        builder.Property(v => v.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();
        builder.Property(v => v.CreatedBy).HasColumnName("created_by");
        builder.Property(v => v.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();
        builder.Property(v => v.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(v => new { v.UserId, v.Type, v.Status })
            .HasDatabaseName("ix_verifications_user_type");

        builder.HasIndex(v => v.ValidUntilUtc)
            .HasDatabaseName("ix_verifications_expiring")
            .HasFilter("status = 'Verified' AND valid_until_utc IS NOT NULL");
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
            t.HasCheckConstraint("ck_consents_type", "consent_type IN ('Terms','Privacy','NotificationsPush','NotificationsSms','NotificationsEmail','ResearchStatistics')");
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
