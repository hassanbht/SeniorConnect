using Microsoft.Extensions.DependencyInjection;
using SeniorConnect.Modules.Identity.Application;

namespace SeniorConnect.Modules.Identity.Infrastructure;

public static class IdentityModuleExtensions
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddSingleton<IIdentityHashingService, IdentityHashingService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ISmsSender, SmsSender>();
        services.AddScoped<IEmailSender, EmailSender>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<ITrustLevelCalculator, TrustLevelCalculator>();
        services.AddScoped<ICapabilityService, CapabilityService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IPrivacyService, PrivacyService>();
        services.AddScoped<IIdentityVerificationProvider, ManualVerificationProvider>();
        services.AddScoped<IIdentityVerificationProvider, OrganizationVerificationProvider>();
        services.AddScoped<SeniorConnect.Modules.Identity.Contracts.ITrustLevelReader, TrustLevelReader>();

        return services;
    }
}
