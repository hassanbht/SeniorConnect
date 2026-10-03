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
        // B-2: same adapter registers the boundary port Community converts through.
        // Resolve via the interface: only ISafeguardingService is registered in the container.
        services.AddScoped<SeniorConnect.Modules.TrustSafety.Contracts.ISafeguardingCaseRaiser>(sp => sp.GetRequiredService<ISafeguardingService>() as SeniorConnect.Modules.TrustSafety.Contracts.ISafeguardingCaseRaiser
            ?? throw new InvalidOperationException("ISafeguardingService does not implement ISafeguardingCaseRaiser."));
        services.AddScoped<IIdentityVerificationProvider, IdAustriaVerificationProvider>();
        services.AddScoped<SeniorConnect.Modules.TrustSafety.Contracts.ISafetyBoundaryReader, SafetyBoundaryReader>();
        services.AddScoped<SeniorConnect.Modules.TrustSafety.Contracts.ISafeguardingCaseReader, SafeguardingCaseReader>();
        services.AddScoped<SeniorConnect.Modules.TrustSafety.Contracts.ISafeguardingRestrictionReader, SafeguardingRestrictionReader>();
        services.AddScoped<SeniorConnect.Modules.TrustSafety.Contracts.ISafeguardingOperationalReader, SafeguardingOperationalReader>();
        return services;
    }
}
