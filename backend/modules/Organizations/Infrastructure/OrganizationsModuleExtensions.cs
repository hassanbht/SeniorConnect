using Microsoft.Extensions.DependencyInjection;
using SeniorConnect.Modules.Organizations.Application;
using SeniorConnect.Modules.Organizations.Contracts;

namespace SeniorConnect.Modules.Organizations.Infrastructure;

public static class OrganizationsModuleExtensions
{
    public static IServiceCollection AddOrganizationsModule(this IServiceCollection services)
    {
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IOrganizationCoordinatorReader, OrganizationCoordinatorReader>();
        services.AddScoped<OrganizationAccessGuard>();
        services.AddScoped<IOrganizationAccessGuard>(sp => sp.GetRequiredService<OrganizationAccessGuard>());
        services.AddScoped<IOrganizationDirectory>(sp => sp.GetRequiredService<OrganizationAccessGuard>());
        return services;
    }
}
