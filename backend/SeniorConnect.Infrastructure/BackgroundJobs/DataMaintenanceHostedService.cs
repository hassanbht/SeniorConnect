using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SeniorConnect.Modules.Identity.Domain;

namespace SeniorConnect.Infrastructure.BackgroundJobs;

/// <summary>
/// P2-18 / P2-24 / P7-08: Background worker executing scheduled data maintenance:
/// - Refresh materialized view (mv_volunteer_roster)
/// - Mark lapsed verifications Expired (trust level already excludes them
///   live via Verification.IsExpired — this keeps the persisted Status
///   truthful for coordinator-facing lists and audit history)
/// - Tier-2 GDPR retention purge for expired deactivated accounts
/// </summary>
public sealed class DataMaintenanceHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataMaintenanceHostedService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

    public DataMaintenanceHostedService(
        IServiceProvider serviceProvider,
        ILogger<DataMaintenanceHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DataMaintenanceHostedService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformMaintenanceAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error occurred during background data maintenance.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task PerformMaintenanceAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeniorConnectDbContext>();

        // 1. Refresh Materialized Views (P2-18)
        try
        {
            await db.Database.ExecuteSqlRawAsync("REFRESH MATERIALIZED VIEW CONCURRENTLY mv_volunteer_roster;", ct);
            _logger.LogInformation("Refreshed materialized view mv_volunteer_roster.");
        }
        catch (Exception)
        {
            // Non-concurrent fallback if concurrent index not built or not yet populated
            try
            {
                await db.Database.ExecuteSqlRawAsync("REFRESH MATERIALIZED VIEW mv_volunteer_roster;", ct);
                _logger.LogInformation("Refreshed materialized view mv_volunteer_roster (non-concurrent fallback).");
            }
            catch (Exception fallbackEx)
            {
                _logger.LogWarning(fallbackEx, "Materialized view mv_volunteer_roster refresh skipped.");
            }
        }

        // 2. Expire lapsed verifications (P2-24)
        var nowUtc = DateTimeOffset.UtcNow;
        var lapsedVerifications = await db.Verifications
            .Where(v => v.Status == VerificationStatus.Verified && v.ValidUntilUtc != null && v.ValidUntilUtc < nowUtc)
            .ToListAsync(ct);

        foreach (var verification in lapsedVerifications)
        {
            verification.Expire();
        }

        if (lapsedVerifications.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Marked {Count} lapsed verification(s) as Expired.", lapsedVerifications.Count);
        }

        // 3. Execute Tier-2 GDPR Deletion Purge (P7-08)
        var now = DateTimeOffset.UtcNow;
        var pendingPurges = await db.AccountDeletionRequests
            .Where(r => r.Status == DeletionTierStatus.Tier1Deactivated && r.ScheduledTier2PurgeUtc <= now)
            .ToListAsync(ct);

        foreach (var purge in pendingPurges)
        {
            purge.ExecuteTier2Purge();

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == purge.UserId, ct);
            if (user is not null)
            {
                user.AnonymizeForGdpr();
            }

            _logger.LogInformation("Executed Tier-2 purge for user {UserId}", purge.UserId);
        }

        if (pendingPurges.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
