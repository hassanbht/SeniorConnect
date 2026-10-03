using Microsoft.Extensions.DependencyInjection;
using SeniorConnect.Modules.Community.Application;
using SeniorConnect.Modules.Community.Contracts;

namespace SeniorConnect.Modules.Community.Infrastructure;

public static class CommunityModuleExtensions
{
    public static IServiceCollection AddCommunityModule(this IServiceCollection services)
    {
        // CommunityService depends on IOrganizationCoordinatorReader — requires
        // builder.Services.AddOrganizationsModule() to have run first in Program.cs.
        services.AddScoped<ICommunityService, CommunityService>();

        // New moderation abstraction. Rule-based implementation is the MVP;
        // AI implementation can be swapped in later without changing domain/API.
        services.AddScoped<ICommunityModerationService, RuleBasedCommunityModerationService>();

        // Legacy adapter for contextual thread messages until threads are migrated
        // to CommunityPost/CommunityComment.
        services.AddScoped<IMessageModerationService, LocalMessageModerationService>();

        services.AddScoped<ICommunityDiscoveryReader, CommunityDiscoveryReader>();
        return services;
    }
}
