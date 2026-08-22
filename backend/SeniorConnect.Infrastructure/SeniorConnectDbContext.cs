using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.HelpRequests.Domain;
using SeniorConnect.Modules.HelpRequests.Infrastructure;
using SeniorConnect.Modules.Reporting.Domain;
using SeniorConnect.Modules.Reporting.Infrastructure;
using SeniorConnect.Modules.TrustSafety.Domain;
using SeniorConnect.Modules.TrustSafety.Infrastructure;
using SeniorConnect.Modules.Organizations.Domain;
using SeniorConnect.Modules.Organizations.Infrastructure;
using SeniorConnect.Modules.Identity.Domain;
using SeniorConnect.Modules.Identity.Infrastructure;
using SeniorConnect.Modules.Profiles.Domain;
using SeniorConnect.Modules.Profiles.Infrastructure;
using SeniorConnect.Domain;
using FunderEntity = SeniorConnect.Modules.Reporting.Domain.Funder;

using SeniorConnect.Modules.Identity.Application;

namespace SeniorConnect.Infrastructure;

public interface ITenantContext
{
    Guid? OrganizationId { get; }

    /// <summary>
    /// True for background jobs and platform administration. Never settable
    /// from a request; the middleware cannot produce a principal with this set.
    /// </summary>
    bool IsPlatformScope { get; }
}

public sealed class SeniorConnectDbContext(
    DbContextOptions<SeniorConnectDbContext> options,
    ITenantContext tenant) : DbContext(options), IIdentityDbContext
{
    // --- Identity ---
    public DbSet<User> Users => Set<User>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserCapability> UserCapabilities => Set<UserCapability>();
    public DbSet<Verification> Verifications => Set<Verification>();
    public DbSet<TrustLevelSnapshot> TrustLevelSnapshots => Set<TrustLevelSnapshot>();
    public DbSet<Consent> Consents => Set<Consent>();

    // --- Profiles ---
    public DbSet<SeniorProfile> SeniorProfiles => Set<SeniorProfile>();
    public DbSet<VolunteerProfile> VolunteerProfiles => Set<VolunteerProfile>();
    public DbSet<Interest> Interests => Set<Interest>();
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<UserLanguage> UserLanguages => Set<UserLanguage>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<VolunteerSkill> VolunteerSkills => Set<VolunteerSkill>();
    public DbSet<AvailabilitySlot> AvailabilitySlots => Set<AvailabilitySlot>();

    // --- Audit ---
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    // --- Organizations ---
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationBranch> OrganizationBranches => Set<OrganizationBranch>();
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();
    public DbSet<OrganizationPolicy> OrganizationPolicies => Set<OrganizationPolicy>();

    // --- Activities ---
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivityCategory> ActivityCategories => Set<ActivityCategory>();
    public DbSet<ReferralDirectory> ReferralDirectories => Set<ReferralDirectory>();

    // --- Onboarding ---
    public DbSet<VolunteerApplication> VolunteerApplications => Set<VolunteerApplication>();
    public DbSet<VolunteerApplicationStep> VolunteerApplicationSteps => Set<VolunteerApplicationStep>();

    // --- Funders ---
    public DbSet<FunderEntity> Funders => Set<FunderEntity>();
    public DbSet<FundingRelationship> FundingRelationships => Set<FundingRelationship>();
    public DbSet<FunderMembership> FunderMemberships => Set<FunderMembership>();

    // --- Views ---
    public DbSet<FunderMonthlyReportView> FunderMonthlyReports => Set<FunderMonthlyReportView>();
    public DbSet<VolunteerHoursView> VolunteerHours => Set<VolunteerHoursView>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");

        // Apply Configurations from all Module Infrastructure assemblies
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SeniorConnectDbContext).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ActivityConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrganizationConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VolunteerApplicationConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FunderConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UserConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SeniorProfileConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditEntryConfiguration).Assembly);

        // --- Tenant isolation (BR-TENANT-03 & ADR-007) -----------------------
        // Note: OrganizationId == null admits community-scoped rows.
        modelBuilder.Entity<Organization>().HasQueryFilter(o => !o.IsDeleted);
        modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);

        modelBuilder.Entity<OrganizationBranch>().HasQueryFilter(b =>
            tenant.IsPlatformScope
            || b.OrganizationId == null
            || b.OrganizationId == tenant.OrganizationId);

        modelBuilder.Entity<OrganizationMembership>().HasQueryFilter(m =>
            tenant.IsPlatformScope
            || m.OrganizationId == null
            || m.OrganizationId == tenant.OrganizationId);

        modelBuilder.Entity<OrganizationPolicy>().HasQueryFilter(p =>
            tenant.IsPlatformScope
            || p.OrganizationId == null
            || p.OrganizationId == tenant.OrganizationId);

        modelBuilder.Entity<Activity>().HasQueryFilter(a =>
            !a.IsDeleted
            && (tenant.IsPlatformScope
                || a.OrganizationId == null
                || a.OrganizationId == tenant.OrganizationId));

        modelBuilder.Entity<VolunteerApplication>().HasQueryFilter(v =>
            tenant.IsPlatformScope
            || v.OrganizationId == null
            || v.OrganizationId == tenant.OrganizationId);

        // --- Concurrency -----------------------------------------------------
        modelBuilder.Entity<Activity>().Property<uint>("xmin").HasColumnType("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        modelBuilder.Entity<Organization>().Property<uint>("xmin").HasColumnType("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();

        // --- Cross-module relationships --------------------------------------
        modelBuilder.Entity<User>()
            .HasMany<Interest>()
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "user_interests",
                j => j.HasOne<Interest>().WithMany().HasForeignKey("interest_id").OnDelete(DeleteBehavior.Restrict),
                j => j.HasOne<User>().WithMany().HasForeignKey("user_id").OnDelete(DeleteBehavior.Cascade),
                j =>
                {
                    j.ToTable("user_interests");
                    j.Property<Guid>("user_id").HasColumnName("user_id");
                    j.Property<Guid>("interest_id").HasColumnName("interest_id");
                    j.HasKey("user_id", "interest_id");
                });

        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(new AuditInterceptor());
        base.OnConfiguring(optionsBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveColumnType("timestamptz");
        base.ConfigureConventions(configurationBuilder);
    }
}
