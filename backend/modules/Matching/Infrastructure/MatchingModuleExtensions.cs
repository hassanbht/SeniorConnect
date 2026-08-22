using Microsoft.Extensions.DependencyInjection;
using SeniorConnect.Modules.Matching.Application;

namespace SeniorConnect.Modules.Matching.Infrastructure;

public static class MatchingModuleExtensions
{
    public static IServiceCollection AddMatchingModule(this IServiceCollection services)
    {
        services.AddScoped<IMatchingService, MatchingService>();
        return services;
    }
}
