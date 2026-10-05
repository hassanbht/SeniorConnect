using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using SeniorConnect.Modules.Identity.Application;

namespace SeniorConnect.Modules.Identity.Infrastructure;

/// <summary>
/// Server-held pepper (HMAC key) for every stored identifier hash. Resolved
/// through configuration like <see cref="JwtSettings"/> so it is never
/// committed and never identical across deployments.
/// </summary>
public sealed record IdentityHashingSettings(byte[] Pepper)
{
    /// <summary>Development-only fallback. Startup refuses it outside Development.</summary>
    public static readonly byte[] DevelopmentPepper =
        SHA256.HashData(Encoding.UTF8.GetBytes("SeniorConnect_identity_dev_hmac_pepper_do_not_use_in_production"));

    private const int MinimumPepperLength = 32;

    public bool IsDevelopmentPepper => Pepper.AsSpan().SequenceEqual(DevelopmentPepper);

    /// <summary>Builds settings from a raw pepper string (tests, design-time hosts).</summary>
    public static IdentityHashingSettings FromPepper(string pepper) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(pepper)));

    public static IdentityHashingSettings Resolve(IConfiguration configuration)
    {
        var pepper = configuration["IDENTITY_HASH_PEPPER"] ?? configuration["Identity:HashPepper"];
        return string.IsNullOrWhiteSpace(pepper)
            ? new IdentityHashingSettings(DevelopmentPepper)
            : FromPepper(pepper);
    }

    /// <summary>Fails startup when a non-Development host runs with a missing or short pepper.</summary>
    public void EnsureSafeFor(bool isDevelopment)
    {
        if (isDevelopment)
        {
            return;
        }

        if (IsDevelopmentPepper || Pepper.Length < MinimumPepperLength)
        {
            throw new InvalidOperationException(
                $"IDENTITY_HASH_PEPPER must be set to a private value of at least {MinimumPepperLength} characters outside Development.");
        }
    }
}

public sealed class IdentityHashingService : IIdentityHashingService
{
    private readonly IdentityHashingSettings _settings;

    public IdentityHashingService(IdentityHashingSettings? settings = null)
    {
        // The default is the development pepper, so a test or design-time host
        // can construct this directly. Any real host goes through
        // AddIdentityModule, which refuses a missing pepper outside Development.
        _settings = settings ?? IdentityHashingSettings.Resolve(EmptyConfiguration.Instance);
    }

    /// <summary>
    /// An <see cref="IConfiguration"/> that answers "not configured" for
    /// everything, so a test or design-time host falls back to the development
    /// pepper. A real host always supplies its configuration to
    /// <c>AddIdentityModule</c>, which refuses a missing pepper outside Development.
    /// </summary>
    private sealed class EmptyConfiguration : Microsoft.Extensions.Configuration.IConfiguration
    {
        public static readonly EmptyConfiguration Instance = new();

        public string? this[string key]
        {
            get => null;
            set { }
        }

        public Microsoft.Extensions.Configuration.IConfigurationSection GetSection(string key) => null!;

        public System.Collections.Generic.IEnumerable<Microsoft.Extensions.Configuration.IConfigurationSection> GetChildren() =>
            Array.Empty<Microsoft.Extensions.Configuration.IConfigurationSection>();

        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() =>
            new Microsoft.Extensions.Primitives.CancellationChangeToken(CancellationToken.None);

        #pragma warning disable CA1822 // IConfiguration interface member; cannot be static
        public string[] GetChildKeys(
            Microsoft.Extensions.Configuration.IConfigurationBuilder builder,
            Action<string>? keyFilter) => [];
#pragma warning restore CA1822

        public Microsoft.Extensions.Primitives.IChangeToken OnChange(Action<object, string?> listener) =>
            GetReloadToken();
    }

    public string HashDestination(string destination)
    {
        var normalized = destination.Trim().ToLowerInvariant();
        return ComputeHmac($"{normalized}:dest");
    }

    public string HashCode(string code)
    {
        var normalized = code.Trim();
        return ComputeHmac($"{normalized}:code");
    }

    public string? HashIp(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return null;
        }

        return ComputeHmac($"{ipAddress.Trim()}:ip");
    }

    public string HashToken(string token)
    {
        return ComputeHmac($"{token}:token");
    }

    /// <summary>
    /// HMAC-SHA256 under a server-held pepper rather than a committed salt.
    /// The previous fixed salt was in source control, so a 6-digit OTP hash
    /// (900k candidates) or a phone number was reversible by precomputation;
    /// without the pepper the stored hashes are no longer brute-forceable.
    /// Deterministic by design: DestinationHash/TokenHash are database lookup
    /// keys, so a per-record salt is not applicable here.
    /// </summary>
    private string ComputeHmac(string input)
    {
        var bytes = HMACSHA256.HashData(_settings.Pepper, Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
