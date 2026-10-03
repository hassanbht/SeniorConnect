using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SeniorConnect.Infrastructure;

public sealed class SeniorConnectDbContextFactory : IDesignTimeDbContextFactory<SeniorConnectDbContext>
{
    public SeniorConnectDbContext CreateDbContext(string[] args)
    {
        LoadEnvFile();

        var dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? "seniorconnect_db";
        var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "seniorconnect_admin";
        var dbPass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "SeniorConnect_SecurePass_2026!";
        var sslMode = Environment.GetEnvironmentVariable("DB_SSL_MODE") ?? "Prefer";

        var envConnectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPass};SSL Mode={sslMode};";
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") ?? envConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<SeniorConnectDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new SeniorConnectDbContext(optionsBuilder.Options, new DesignTimeTenantContext());
    }

    private static void LoadEnvFile()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            var envPath = Path.Combine(current.FullName, ".env");
            if (File.Exists(envPath))
            {
                foreach (var line in File.ReadAllLines(envPath))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#')) continue;
                    var idx = trimmed.IndexOf('=');
                    if (idx > 0)
                    {
                        var key = trimmed.Substring(0, idx).Trim();
                        var val = trimmed.Substring(idx + 1).Trim();
                        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                        {
                            Environment.SetEnvironmentVariable(key, val);
                        }
                    }
                }
                break;
            }
            current = current.Parent;
        }
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid? OrganizationId => null;
        public bool IsPlatformScope => true;
    }
}
