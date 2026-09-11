using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SeniorConnect.Modules.Matching.Application;
using SeniorConnect.Modules.Matching.Domain;

namespace SeniorConnect.Modules.Matching.Infrastructure;

public static class MatchingModuleExtensions
{
    public static IServiceCollection AddMatchingModule(this IServiceCollection services, IConfiguration configuration)
    {
        // P3-09 / Gate 3 item 6: weights are configuration, changing one
        // must never require a rebuild. Uses record constructor binding
        // (net6+); falls back to MatchingConfig's own defaults if the
        // "Matching:Weights" section is absent.
        var weights = configuration.GetSection("Matching:Weights").Get<MatchingConfig>() ?? new MatchingConfig();
        services.AddSingleton(Options.Create(weights));
        services.AddScoped<IMatchingService, MatchingService>();
        return services;
    }
}
