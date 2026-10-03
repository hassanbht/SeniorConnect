using Microsoft.Extensions.DependencyInjection;
using SeniorConnect.Modules.Identity.Application;

namespace SeniorConnect.Modules.Identity.Infrastructure;

public static class IdentityModuleExtensions
{
    /// <param name="isDevelopment">
    /// Only a Development host gets the external-login stubs. Everywhere else
    /// Google tokens are really validated and ID Austria is refused.
    /// </param>
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, bool isDevelopment)
    {
        services.AddSingleton<IIdentityHashingService, IdentityHashingService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddScoped<ISmsSender>(sp => new SmsSender(
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SmsSender>>(), isDevelopment));
        // Deployment-integration phase: production-honest email provider.
        // Configuration comes from the Email section / EMAIL_USERNAME /
        // EMAIL_PASSWORD environment variables; the committed default is
        // Provider "none" → sends fail with EMAIL_PROVIDER_NOT_CONFIGURED
        // outside Development instead of pretending delivery. Only a
        // Development host gets the log-based fallback (same rule as SmsSender).
        services.AddScoped<IEmailSender>(sp => new SmtpEmailSender(
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SmtpEmailSender>>(),
            EmailProviderOptions.Resolve(sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()),
            isDevelopment));
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<ITrustLevelCalculator, TrustLevelCalculator>();
        services.AddScoped<ICapabilityService, CapabilityService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IPhotoStorage, LocalDiskPhotoStorage>();
        services.AddScoped<IPrivacyService, PrivacyService>();
        services.AddScoped<IIdentityVerificationProvider, ManualVerificationProvider>();
        services.AddScoped<IIdentityVerificationProvider, OrganizationVerificationProvider>();
        if (isDevelopment)
        {
            services.AddScoped<IGoogleIdTokenValidator, GoogleIdTokenValidatorStub>();
            services.AddScoped<IIdAustriaClient, IdAustriaClientStub>();
        }
        else
        {
            services.AddScoped<IGoogleIdTokenValidator, GoogleIdTokenValidator>();
            services.AddScoped<IIdAustriaClient, DisabledIdAustriaClient>();
        }
        services.AddScoped<SeniorConnect.Modules.Identity.Contracts.ITrustLevelReader, TrustLevelReader>();
        services.AddScoped<SeniorConnect.Modules.Identity.Contracts.IUserContactReader, UserContactReader>();
        services.AddScoped<SeniorConnect.Modules.Identity.Contracts.IUserAccountStatusReader, UserAccountStatusReader>();
        services.AddScoped<SeniorConnect.Modules.Identity.Contracts.ISeniorAccountProvisioner, SeniorAccountProvisioner>();
        services.AddScoped<SeniorConnect.Modules.Identity.Contracts.IUserSessionIssuer, SeniorAccountProvisioner>();
        services.AddScoped<SeniorConnect.Modules.Identity.Contracts.IVerificationFactsReader, VerificationFactsReader>();
        services.AddScoped<SeniorConnect.Modules.Identity.Contracts.IAddressVerificationInvalidator, AddressVerificationInvalidator>();
        services.AddScoped<IVerificationService, VerificationService>();

        return services;
    }
}
