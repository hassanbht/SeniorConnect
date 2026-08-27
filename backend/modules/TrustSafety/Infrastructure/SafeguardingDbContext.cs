using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.TrustSafety.Application;
using SeniorConnect.Modules.TrustSafety.Domain;

namespace SeniorConnect.Modules.TrustSafety.Infrastructure;

public sealed class SafeguardingDbContext(DbContextOptions<SafeguardingDbContext> options)
    : DbContext(options), ISafeguardingDbContext
{
    public DbSet<SafeguardingCase> SafeguardingCases => Set<SafeguardingCase>();
    public DbSet<SafeguardingCaseNote> SafeguardingCaseNotes => Set<SafeguardingCaseNote>();
    public DbSet<SafeguardingAccessLog> SafeguardingAccessLogs => Set<SafeguardingAccessLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("safeguarding");
        modelBuilder.ApplyConfiguration(new SafeguardingCaseConfiguration());
        modelBuilder.ApplyConfiguration(new SafeguardingCaseNoteConfiguration());
        modelBuilder.ApplyConfiguration(new SafeguardingAccessLogConfiguration());
    }
}
