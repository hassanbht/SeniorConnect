using Microsoft.Extensions.DependencyInjection;
using SeniorConnect.Modules.Organizations.Application;

namespace SeniorConnect.Modules.Organizations.Infrastructure;

public static class OrganizationsModuleExtensions
{
    public static IServiceCollection AddOrganizationsModule(this IServiceCollection services)
    {
        services.AddScoped<IOrganizationService, OrganizationService>();
        return services;
    }
}
