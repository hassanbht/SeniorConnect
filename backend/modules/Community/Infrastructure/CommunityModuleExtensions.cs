using Microsoft.Extensions.DependencyInjection;
using SeniorConnect.Modules.Community.Application;

namespace SeniorConnect.Modules.Community.Infrastructure;

public static class CommunityModuleExtensions
{
    public static IServiceCollection AddCommunityModule(this IServiceCollection services)
    {
        // CommunityService depends on IOrganizationCoordinatorReader — requires
        // builder.Services.AddOrganizationsModule() to have run first in Program.cs.
        services.AddScoped<ICommunityService, CommunityService>();
        services.AddSingleton<IMessageModerationService, LocalMessageModerationService>();
        return services;
    }
}
