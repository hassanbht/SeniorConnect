using Microsoft.Extensions.DependencyInjection;
using SeniorConnect.Modules.Reporting.Application;

namespace SeniorConnect.Modules.Reporting.Infrastructure;

public static class ReportingModuleExtensions
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services)
    {
        services.AddScoped<IFunderService, FunderService>();
        services.AddScoped<IReportingService, ReportingService>();
        return services;
    }
}
