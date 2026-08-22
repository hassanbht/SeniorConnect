using Microsoft.Extensions.DependencyInjection;
using SeniorConnect.Modules.TrustSafety.Application;

namespace SeniorConnect.Modules.TrustSafety.Infrastructure;

public static class TrustSafetyModuleExtensions
{
    public static IServiceCollection AddTrustSafetyModule(this IServiceCollection services)
    {
        services.AddScoped<IOnboardingService, OnboardingService>();
        return services;
    }
}
