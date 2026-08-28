using Microsoft.Extensions.DependencyInjection;
using SeniorConnect.Modules.TrustSafety.Application;

namespace SeniorConnect.Modules.TrustSafety.Infrastructure;

public static class TrustSafetyModuleExtensions
{
    public static IServiceCollection AddTrustSafetyModule(this IServiceCollection services)
    {
        services.AddScoped<IOnboardingService, OnboardingService>();
        services.AddScoped<ITrustSafetyService, TrustSafetyService>();
        services.AddScoped<ISafeguardingService, SafeguardingService>();
        services.AddScoped<IIdentityVerificationProvider, IdAustriaVerificationProvider>();
        services.AddScoped<SeniorConnect.Modules.TrustSafety.Contracts.ISafetyBoundaryReader, SafetyBoundaryReader>();
        return services;
    }
}
