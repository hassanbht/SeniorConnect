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
        // ADR-029: staff two-factor authorization, consumed by Identity (optional, fail-closed).
        services.AddScoped<IStaffSecurityAuthorizer, OrganizationStaffSecurityAuthorizer>();
        return services;
    }
}
