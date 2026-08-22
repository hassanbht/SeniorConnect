using Microsoft.Extensions.DependencyInjection;
using SeniorConnect.Modules.Profiles.Application;

namespace SeniorConnect.Modules.Profiles.Infrastructure;

public static class ProfilesModuleExtensions
{
    public static IServiceCollection AddProfilesModule(this IServiceCollection services)
    {
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IReferenceDataService, ReferenceDataService>();

        return services;
    }
}
