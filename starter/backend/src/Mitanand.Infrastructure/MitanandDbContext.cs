using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Activities.Domain;
using SeniorConnect.SharedKernel;

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
    ITenantContext tenant) : DbContext(options)
{
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivityCategory> ActivityCategories => Set<ActivityCategory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SeniorConnectDbContext).Assembly);

        // --- Tenant isolation ------------------------------------------------
        //
        // ADR-002 + BR-TENANT-03. Note the `OrganizationId == null` clause: a
        // community activity with no organization must stay visible. Dropping
        // it would silently break ADR-007, which is why an architecture test
        // asserts the filter exists on every IOrganizationScoped entity.
        modelBuilder.Entity<Activity>().HasQueryFilter(a =>
            !a.IsDeleted
            && (tenant.IsPlatformScope
                || a.OrganizationId == null
                || a.OrganizationId == tenant.OrganizationId));

        // --- Concurrency -----------------------------------------------------
        // Postgres system column. No explicit RowVersion property needed.
        modelBuilder.Entity<Activity>().UseXminAsConcurrencyToken();

        base.OnModelCreating(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Every enum is stored as text with a CHECK constraint, never as an int
        // and never as a Postgres enum type — ALTER TYPE is painful and this
        // product will alter them.
        builder.Properties<DateTimeOffset>().HaveColumnType("timestamptz");
        base.ConfigureConventions(builder);
    }
}
