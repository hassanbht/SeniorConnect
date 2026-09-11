using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SeniorConnect.Modules.Notifications.Application;

namespace SeniorConnect.Infrastructure.BackgroundJobs;

/// <summary>
/// P2-16: runs the silent-volunteer reminder sweep daily, once per
/// organization. Safe to run more than once a day or alongside a manual
/// trigger — VolunteerProfile.TryMarkMonthlyReminderSent guarantees at most
/// one reminder per volunteer per calendar month regardless of how many
/// times this fires.
/// </summary>
public sealed class MonthlyVolunteerReminderHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MonthlyVolunteerReminderHostedService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24);

    public MonthlyVolunteerReminderHostedService(
        IServiceProvider serviceProvider,
        ILogger<MonthlyVolunteerReminderHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MonthlyVolunteerReminderHostedService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SeniorConnectDbContext>();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                var organizationIds = await db.Organizations
                    .Select(o => o.Id)
                    .ToListAsync(stoppingToken);

                foreach (var organizationId in organizationIds)
                {
                    var (silentCount, dispatchedCount) = await VolunteerEngagementJobs
                        .SendMonthlySilentVolunteerRemindersAsync(db, notificationService, organizationId, stoppingToken);

                    if (dispatchedCount > 0)
                    {
                        _logger.LogInformation(
                            "Org {OrganizationId}: {SilentCount} silent volunteer(s), {DispatchedCount} reminder(s) sent.",
                            organizationId, silentCount, dispatchedCount);
                    }
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error occurred during monthly volunteer reminder sweep.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }
}
