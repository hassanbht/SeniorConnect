using Microsoft.Extensions.DependencyInjection;
using SeniorConnect.Modules.HelpRequests.Application;

namespace SeniorConnect.Modules.HelpRequests.Infrastructure;

public static class HelpRequestsModuleExtensions
{
    public static IServiceCollection AddHelpRequestsModule(this IServiceCollection services)
    {
        services.AddScoped<IActivityService, ActivityService>();
        return services;
    }
}
