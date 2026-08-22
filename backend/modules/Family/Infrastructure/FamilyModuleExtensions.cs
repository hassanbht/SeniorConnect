using Microsoft.Extensions.DependencyInjection;
using SeniorConnect.Modules.Family.Application;

namespace SeniorConnect.Modules.Family.Infrastructure;

public static class FamilyModuleExtensions
{
    public static IServiceCollection AddFamilyModule(this IServiceCollection services)
    {
        services.AddScoped<IFamilyService, FamilyService>();
        return services;
    }
}
