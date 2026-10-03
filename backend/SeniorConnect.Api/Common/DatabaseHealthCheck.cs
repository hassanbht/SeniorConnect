using Microsoft.Extensions.Diagnostics.HealthChecks;
using SeniorConnect.Infrastructure;

namespace SeniorConnect.Api.Common;

/// <summary>P1-05 / P9-02: readiness probe that actually reaches PostgreSQL.</summary>
public sealed class DatabaseHealthCheck(SeniorConnectDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy("Database reachable.")
            : HealthCheckResult.Unhealthy("Database unreachable.");
    }
}
