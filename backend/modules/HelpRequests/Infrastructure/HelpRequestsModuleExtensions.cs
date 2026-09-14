using Microsoft.Extensions.DependencyInjection;
using SeniorConnect.Modules.HelpRequests.Application;
using SeniorConnect.Modules.HelpRequests.Contracts;
using SeniorConnect.Modules.HelpRequests.Domain;

namespace SeniorConnect.Modules.HelpRequests.Infrastructure;

public static class HelpRequestsModuleExtensions
{
    public static IServiceCollection AddHelpRequestsModule(this IServiceCollection services)
    {
        services.AddSingleton<IActivitySafetyPolicy, ActivitySafetyPolicy>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<IHelpRequestService, HelpRequestService>();
        services.AddScoped<IVoiceRequestParser, VoiceRequestParser>();
        services.AddScoped<IHelpRequestDiscoveryReader, HelpRequestDiscoveryReader>();
        return services;
    }
}
