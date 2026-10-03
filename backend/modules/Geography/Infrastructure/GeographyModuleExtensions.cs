using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SeniorConnect.Modules.Geography.Application;

namespace SeniorConnect.Modules.Geography.Infrastructure;

public static class GeographyModuleExtensions
{
    public static IServiceCollection AddGeographyModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IGeographyReferenceService, GeographyReferenceService>();
        services.AddScoped<IProximityService, ProximityService>();
        services.AddScoped<IStoreLocationService, StoreLocationService>();

        // Real provider when configured; otherwise every lookup answers
        // GEOCODING_NOT_CONFIGURED. Never a fake/sample provider.
        var geocoding = configuration.GetSection(GeocodingOptions.SectionName).Get<GeocodingOptions>() ?? new GeocodingOptions();
        services.AddSingleton(geocoding);
        if (geocoding.ConfigurationProblem() is null)
        {
            services.AddHttpClient<IGeocodingProvider, NominatimGeocodingProvider>()
                // The default handler logs full request URIs — which would
                // contain the private address and the API key.
                .RemoveAllLoggers();
        }
        else
        {
            services.AddScoped<IGeocodingProvider, UnconfiguredGeocodingProvider>();
        }

        return services;
    }
}
