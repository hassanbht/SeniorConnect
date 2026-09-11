using Microsoft.Extensions.DependencyInjection;
using SeniorConnect.Modules.Community.Application;

namespace SeniorConnect.Modules.Community.Infrastructure;

public static class CommunityModuleExtensions
{
    public static IServiceCollection AddCommunityModule(this IServiceCollection services)
    {
        services.AddScoped<ICommunityService, CommunityService>();
        services.AddSingleton<IMessageModerationService, LocalMessageModerationService>();
        return services;
    }
}
