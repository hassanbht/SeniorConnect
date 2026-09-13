using Microsoft.Extensions.DependencyInjection;
using SeniorConnect.Modules.Geography.Application;

namespace SeniorConnect.Modules.Geography.Infrastructure;

public static class GeographyModuleExtensions
{
    public static IServiceCollection AddGeographyModule(this IServiceCollection services)
    {
        services.AddScoped<IGeographyReferenceService, GeographyReferenceService>();
        services.AddScoped<IGeocodingProvider, GeocodingProviderStub>();
        services.AddScoped<IProximityService, ProximityService>();
        services.AddScoped<IStoreLocationService, StoreLocationService>();

        return services;
    }
}