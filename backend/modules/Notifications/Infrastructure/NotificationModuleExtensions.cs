using Microsoft.Extensions.DependencyInjection;
using SeniorConnect.Modules.Notifications.Application;

namespace SeniorConnect.Modules.Notifications.Infrastructure;

public static class NotificationModuleExtensions
{
    public static IServiceCollection AddNotificationModule(this IServiceCollection services)
    {
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }
}
