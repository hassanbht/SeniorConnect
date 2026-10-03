namespace SeniorConnect.Modules.Identity.Infrastructure;

/// <summary>
/// Deployment-integration phase: email provider abstraction configuration.
/// Binds the "Email" configuration section; every secret-bearing member
/// (Username, Password) also reads the EMAIL_USERNAME / EMAIL_PASSWORD
/// environment variables so credentials live in the environment / secret
/// store and never in a committed file. Defaults are secure and honest:
/// Provider "none" → every send fails with EMAIL_PROVIDER_NOT_CONFIGURED.
/// </summary>
public sealed class EmailProviderOptions
{
    public const string SectionName = "Email";

    /// <summary>"none" (default — sends fail honestly) or "smtp".</summary>
    public string Provider { get; set; } = "none";

    /// <summary>SMTP relay host. Required when Provider = "smtp".</summary>
    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    /// <summary>Envelope From address / sender identity.</summary>
    public string? SenderAddress { get; set; }

    public string? SenderName { get; set; }

    /// <summary>SMTP username — from the environment / secret store, never committed.</summary>
    public string? Username { get; set; }

    /// <summary>SMTP password — from the environment / secret store, never committed.</summary>
    public string? Password { get; set; }

    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>SMTP client timeout in milliseconds (derived from TimeoutSeconds).</summary>
    public int TimeoutMilliseconds => TimeoutSeconds * 1000;

    /// <summary>Retries for transient failures only (timeouts, busy relay).</summary>
    public int MaxRetries { get; set; } = 1;

    /// <summary>Environment-variable overrides, so secrets can come from the secret store.</summary>
    public static EmailProviderOptions Resolve(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        // Manual key mapping: the Identity module does not reference the
        // configuration Binder package, and the key set is small and stable.
        string? Value(string key) => configuration[$"{SectionName}:{key}"];

        var options = new EmailProviderOptions();
        options.Provider = Value(nameof(Provider)) ?? options.Provider;
        options.Host = Value(nameof(Host)) ?? options.Host;
        options.Port = int.TryParse(Value(nameof(Port)), out var port) ? port : options.Port;
        options.SenderAddress = Value(nameof(SenderAddress)) ?? options.SenderAddress;
        options.SenderName = Value(nameof(SenderName)) ?? options.SenderName;
        options.Username = Value(nameof(Username)) ?? options.Username;
        options.Password = Value(nameof(Password)) ?? options.Password;
        options.TimeoutSeconds = int.TryParse(Value(nameof(TimeoutSeconds)), out var timeout) ? timeout : options.TimeoutSeconds;
        options.MaxRetries = int.TryParse(Value(nameof(MaxRetries)), out var retries) ? retries : options.MaxRetries;
        options.Username = Environment.GetEnvironmentVariable("EMAIL_USERNAME") ?? options.Username;
        options.Password = Environment.GetEnvironmentVariable("EMAIL_PASSWORD") ?? options.Password;
        return options;
    }

    /// <summary>
    /// True only when a real provider is selected AND its endpoint is usable.
    /// A misconfigured Smtp selection (missing host/sender) must not silently
    /// downgrade to "nothing sends" — it fails loudly at startup instead.
    /// </summary>
    public bool IsConfigured =>
        string.Equals(Provider, "smtp", System.StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(Host)
        && !string.IsNullOrWhiteSpace(SenderAddress);

    /// <summary>
    /// Fails fast at startup when Provider = "smtp" is selected but the
    /// endpoint is incomplete — a required-configuration failure must be
    /// explicit (deployment-integration rule), never a silent no-send.
    /// </summary>
    public void EnsureValidSelection()
    {
        var providerSelected = !string.Equals(Provider, "none", System.StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(Provider);
        if (providerSelected && !IsConfigured)
        {
            throw new Microsoft.Extensions.Options.OptionsValidationException(
                SectionName,
                typeof(EmailProviderOptions),
                [$"Email:Provider '{Provider}' is selected but Email:Host / Email:SenderAddress are not fully configured. Complete the deployment configuration or set Email:Provider to 'none'."]);
        }
    }
}
