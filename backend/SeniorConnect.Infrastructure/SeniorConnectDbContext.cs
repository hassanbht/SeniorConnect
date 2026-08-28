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
using SeniorConnect.Modules.Profiles.Application;
using SeniorConnect.Modules.Organizations.Application;
using SeniorConnect.Modules.HelpRequests.Application;
using SeniorConnect.Modules.TrustSafety.Application;
using SeniorConnect.Modules.Reporting.Application;

using SeniorConnect.Modules.Community.Domain;
using SeniorConnect.Modules.Community.Application;
using SeniorConnect.Modules.Family.Domain;
using SeniorConnect.Modules.Family.Application;
using SeniorConnect.Modules.Notifications.Domain;
using SeniorConnect.Modules.Notifications.Application;

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
    ITenantContext tenant) : DbContext(options), IIdentityDbContext, IProfilesDbContext, IOrganizationsDbContext, IHelpRequestsDbContext, ITrustSafetyDbContext, IReportingDbContext, ICommunityDbContext, IFamilyDbContext, INotificationsDbContext
{
    // --- Identity & GDPR ---
    public DbSet<User> Users => Set<User>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserCapability> UserCapabilities => Set<UserCapability>();
    public DbSet<Verification> Verifications => Set<Verification>();
    public DbSet<TrustLevelSnapshot> TrustLevelSnapshots => Set<TrustLevelSnapshot>();
    public DbSet<Consent> Consents => Set<Consent>();
    public DbSet<AccountDeletionRequest> AccountDeletionRequests => Set<AccountDeletionRequest>();

    // --- Profiles ---
    public DbSet<SupportProfile> SupportProfiles => Set<SupportProfile>();
    public DbSet<VolunteerProfile> VolunteerProfiles => Set<VolunteerProfile>();
    public DbSet<Interest> Interests => Set<Interest>();
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<UserLanguage> UserLanguages => Set<UserLanguage>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<VolunteerSkill> VolunteerSkills => Set<VolunteerSkill>();
    public DbSet<AvailabilitySlot> AvailabilitySlots => Set<AvailabilitySlot>();

    // --- Audit ---
    public DbSet<SeniorConnect.Modules.Reporting.Domain.AuditEntry> AuditEntries => Set<SeniorConnect.Modules.Reporting.Domain.AuditEntry>();

    // --- Organizations ---
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationBranch> OrganizationBranches => Set<OrganizationBranch>();
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();
    public DbSet<OrganizationPolicy> OrganizationPolicies => Set<OrganizationPolicy>();

    // --- Activities & Help Requests ---
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivityCategory> ActivityCategories => Set<ActivityCategory>();
    public DbSet<ReferralDirectory> ReferralDirectories => Set<ReferralDirectory>();
    public DbSet<HelpRequest> HelpRequests => Set<HelpRequest>();
    public DbSet<HelpRequestStatusHistory> HelpRequestStatusHistories => Set<HelpRequestStatusHistory>();

    // --- Trust & Safety ---
    public DbSet<VolunteerApplication> VolunteerApplications => Set<VolunteerApplication>();
    public DbSet<VolunteerApplicationStep> VolunteerApplicationSteps => Set<VolunteerApplicationStep>();
    public DbSet<BuddyAssignment> BuddyAssignments => Set<BuddyAssignment>();
    public DbSet<KeyCustody> KeyCustodies => Set<KeyCustody>();
    public DbSet<ExpenseRecord> ExpenseRecords => Set<ExpenseRecord>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();

    // --- Community ---
    public DbSet<CommunityGroup> CommunityGroups => Set<CommunityGroup>();
    public DbSet<GroupMembership> GroupMemberships => Set<GroupMembership>();
    public DbSet<CommunityEvent> CommunityEvents => Set<CommunityEvent>();
    public DbSet<EventRegistration> EventRegistrations => Set<EventRegistration>();
    public DbSet<MessageThread> MessageThreads => Set<MessageThread>();
    public DbSet<ThreadMessage> ThreadMessages => Set<ThreadMessage>();

    // --- Family ---
    public DbSet<FamilyRelationship> FamilyRelationships => Set<FamilyRelationship>();
    public DbSet<FamilyPermission> FamilyPermissions => Set<FamilyPermission>();
    public DbSet<SeniorAccessLog> SeniorAccessLogs => Set<SeniorAccessLog>();
    public DbSet<TrustedContact> TrustedContacts => Set<TrustedContact>();
    public DbSet<SafetyAlert> SafetyAlerts => Set<SafetyAlert>();
    public DbSet<Zugangskarte> Zugangskarten => Set<Zugangskarte>();

    // --- Notifications ---
    public DbSet<NotificationMessage> NotificationMessages => Set<NotificationMessage>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<NotificationBudgetTracker> NotificationBudgetTrackers => Set<NotificationBudgetTracker>();

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
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SupportProfileConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditEntryConfiguration).Assembly);

        // --- Tenant isolation (BR-TENANT-03 & ADR-007) -----------------------
        // Note: OrganizationId == null admits community-scoped rows.
        modelBuilder.Entity<Organization>().HasQueryFilter(o => !o.IsDeleted);
        modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);

        modelBuilder.Entity<CommunityGroup>().HasQueryFilter(g =>
            !g.IsDeleted
            && (tenant.IsPlatformScope
                || g.OrganizationId == null
                || g.OrganizationId == tenant.OrganizationId));

        modelBuilder.Entity<CommunityEvent>().HasQueryFilter(e =>
            !e.IsDeleted
            && (tenant.IsPlatformScope
                || e.OrganizationId == null
                || e.OrganizationId == tenant.OrganizationId));

        modelBuilder.Entity<ThreadMessage>().HasQueryFilter(m => !m.IsDeleted);
        modelBuilder.Entity<TrustedContact>().HasQueryFilter(c => !c.IsDeleted);

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

        modelBuilder.Entity<HelpRequest>().HasQueryFilter(h =>
            !h.IsDeleted
            && (tenant.IsPlatformScope
                || h.OrganizationId == null
                || h.OrganizationId == tenant.OrganizationId));

        modelBuilder.Entity<VolunteerApplication>().HasQueryFilter(v =>
            tenant.IsPlatformScope
            || v.OrganizationId == null
            || v.OrganizationId == tenant.OrganizationId);

        // --- Concurrency -----------------------------------------------------
        modelBuilder.Entity<Activity>().Property<uint>("xmin").HasColumnType("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        modelBuilder.Entity<Organization>().Property<uint>("xmin").HasColumnType("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        modelBuilder.Entity<HelpRequest>().Property<uint>("xmin").HasColumnType("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();

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

        // --- Family & Delegation ---------------------------------------------
        modelBuilder.Entity<FamilyRelationship>(b =>
        {
            b.HasMany(r => r.Permissions)
             .WithOne()
             .HasForeignKey(p => p.FamilyRelationshipId)
             .OnDelete(DeleteBehavior.Cascade);
            b.Navigation(r => r.Permissions).UsePropertyAccessMode(PropertyAccessMode.Field);
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
