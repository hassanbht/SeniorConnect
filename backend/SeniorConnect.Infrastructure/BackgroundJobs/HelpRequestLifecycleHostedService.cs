using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SeniorConnect.Modules.Matching.Application;

namespace SeniorConnect.Infrastructure.BackgroundJobs;

/// <summary>
/// P3-13 / P3-18: periodically widens stale help-request offers (tier 1 →
/// 2 → 3 → escalate to coordinator) and dispatches the T-24h / T-2h
/// assignment reminders. Mirrors DataMaintenanceHostedService's pattern.
/// </summary>
public sealed class HelpRequestLifecycleHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HelpRequestLifecycleHostedService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

    public HelpRequestLifecycleHostedService(
        IServiceProvider serviceProvider,
        ILogger<HelpRequestLifecycleHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HelpRequestLifecycleHostedService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var matchingService = scope.ServiceProvider.GetRequiredService<IMatchingService>();

                var remindersSent = await matchingService.DispatchDueAssignmentRemindersAsync(stoppingToken);
                var offersAdvanced = await matchingService.AdvanceStaleOffersAsync(stoppingToken);

                if (remindersSent > 0 || offersAdvanced > 0)
                {
                    _logger.LogInformation(
                        "HelpRequest lifecycle sweep: {RemindersSent} reminder(s) sent, {OffersAdvanced} offer(s) advanced/escalated.",
                        remindersSent, offersAdvanced);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error occurred during help-request lifecycle sweep.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }
}
