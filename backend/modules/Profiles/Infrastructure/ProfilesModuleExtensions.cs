using Microsoft.Extensions.DependencyInjection;
using SeniorConnect.Modules.Profiles.Application;
using SeniorConnect.Modules.Profiles.Contracts;
using SeniorConnect.Modules.Profiles.Contracts;

namespace SeniorConnect.Modules.Profiles.Infrastructure;

public static class ProfilesModuleExtensions
{
    public static IServiceCollection AddProfilesModule(this IServiceCollection services)
    {
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IReferenceDataService, ReferenceDataService>();
        services.AddScoped<IVolunteerReliabilityUpdater, VolunteerReliabilityUpdater>();
        services.AddScoped<ISupportProfileProvisioner, SupportProfileProvisioner>();

        return services;
    }
}
