using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SeniorConnect.Infrastructure;

public sealed class SeniorConnectDbContextFactory : IDesignTimeDbContextFactory<SeniorConnectDbContext>
{
    public SeniorConnectDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SeniorConnectDbContext>();
        
        // Use Npgsql with dummy connection string for design-time migrations compilation
        optionsBuilder.UseNpgsql("Host=localhost;Database=SeniorConnect;Username=postgres;Password=postgres");

        return new SeniorConnectDbContext(optionsBuilder.Options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid? OrganizationId => null;
        public bool IsPlatformScope => true;
    }
}
