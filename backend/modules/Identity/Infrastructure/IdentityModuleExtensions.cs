using Microsoft.Extensions.DependencyInjection;
using SeniorConnect.Modules.Identity.Application;

namespace SeniorConnect.Modules.Identity.Infrastructure;

public static class IdentityModuleExtensions
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddSingleton<IIdentityHashingService, IdentityHashingService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddScoped<ISmsSender, SmsSender>();
        services.AddScoped<IEmailSender, EmailSender>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<ITrustLevelCalculator, TrustLevelCalculator>();
        services.AddScoped<ICapabilityService, CapabilityService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IPhotoStorage, LocalDiskPhotoStorage>();
        services.AddScoped<IPrivacyService, PrivacyService>();
        services.AddScoped<IIdentityVerificationProvider, ManualVerificationProvider>();
        services.AddScoped<IIdentityVerificationProvider, OrganizationVerificationProvider>();
        services.AddScoped<IGoogleIdTokenValidator, GoogleIdTokenValidatorStub>();
        services.AddScoped<IIdAustriaClient, IdAustriaClientStub>();
        services.AddScoped<SeniorConnect.Modules.Identity.Contracts.ITrustLevelReader, TrustLevelReader>();
        services.AddScoped<SeniorConnect.Modules.Identity.Contracts.IUserContactReader, UserContactReader>();
        services.AddScoped<SeniorConnect.Modules.Identity.Contracts.ISeniorAccountProvisioner, SeniorAccountProvisioner>();
        services.AddScoped<SeniorConnect.Modules.Identity.Contracts.IUserSessionIssuer, SeniorAccountProvisioner>();

        return services;
    }
}
