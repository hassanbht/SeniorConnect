using FluentAssertions;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Domain;
using SeniorConnect.Modules.Identity.Infrastructure;
using SeniorConnect.Modules.TrustSafety.Application;
using SeniorConnect.Modules.TrustSafety.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.TrustSafety.Tests;

/// <summary>
/// Wave-2 (BR-TRUST-06): the external identity provider must never assert a
/// verification it cannot substantiate.
///
/// The provider performs no cryptographic validation of any upstream assertion,
/// so reporting success would turn a client-supplied token reference into a
/// trusted Identity verification — the input to every home-visit safety gate.
/// </summary>
public sealed class IdAustriaProviderFailClosedTests
{
    private static IdentityVerificationRequest Request(Guid? userId = null) => new(
        UserId: userId ?? Guid.NewGuid(),
        ProviderType: VerificationProviderType.IdAustria,
        ExternalTokenReference: "ID_AUSTRIA_TOKEN_FROM_THE_CLIENT");

    [Fact]
    public async Task A_disabled_provider_refuses_in_every_environment()
    {
        var development = new IdAustriaVerificationProvider(
            new IdAustriaOptions { Enabled = false }, isDevelopment: true);
        var production = new IdAustriaVerificationProvider(
            new IdAustriaOptions { Enabled = false }, isDevelopment: false);

        (await development.VerifyIdentityAsync(Request())).Error!.Code
            .Should().Be(IdAustriaVerificationProvider.RefusalCode);
        (await production.VerifyIdentityAsync(Request())).Error!.Code
            .Should().Be(IdAustriaVerificationProvider.RefusalCode);
    }

    [Fact]
    public async Task An_enabled_provider_still_refuses_outside_development()
    {
        var production = new IdAustriaVerificationProvider(
            new IdAustriaOptions { Enabled = true, SharedSecret = "a-secret-long-enough-for-production-32" },
            isDevelopment: false);

        var result = await production.VerifyIdentityAsync(Request());

        result.IsFailure.Should().BeTrue(
            "without real upstream validation a production host must not stamp an identity as verified");
        result.Error!.Code.Should().Be(IdAustriaVerificationProvider.RefusalCode);
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task The_sandbox_provider_answers_only_in_development()
    {
        var sandbox = new IdAustriaVerificationProvider(
            new IdAustriaOptions { Enabled = true, SharedSecret = "sandbox-secret" },
            isDevelopment: true);

        var result = await sandbox.VerifyIdentityAsync(Request());

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsSuccess.Should().BeTrue();
        result.Value!.ProviderName.Should().Contain("sandbox",
            "a simulated result must never be presentable as a real one");
    }

    [Fact]
    public async Task An_empty_user_id_is_still_a_validation_error()
    {
        var sandbox = new IdAustriaVerificationProvider(
            new IdAustriaOptions { Enabled = true, SharedSecret = "sandbox-secret" },
            isDevelopment: true);

        var result = await sandbox.VerifyIdentityAsync(Request(Guid.Empty));

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.Validation);
    }

    // --- configuration safety ------------------------------------------------

    [Fact]
    public void Enabling_the_provider_without_a_shared_secret_is_refused()
    {
        var options = new IdAustriaOptions { Enabled = true, SharedSecret = null };

        var act = () => options.EnsureSafeFor(isDevelopment: true);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_short_shared_secret_is_refused_outside_development()
    {
        var options = new IdAustriaOptions { Enabled = true, SharedSecret = "too-short" };

        var act = () => options.EnsureSafeFor(isDevelopment: false);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_disabled_provider_never_needs_a_secret()
    {
        var options = new IdAustriaOptions { Enabled = false, SharedSecret = null };

        var act = () => options.EnsureSafeFor(isDevelopment: false);

        act.Should().NotThrow("a provider that refuses everything is always safe to start");
    }

    [Fact]
    public void Configuration_defaults_to_disabled_when_absent()
    {
        var resolved = IdAustriaOptions.Resolve(new StubConfiguration(null));

        resolved.Enabled.Should().BeFalse(
            "the provider must be opt-in: a platform must never start with a rubber-stamp verifier");
    }

    private sealed class StubConfiguration(string? enabled) : Microsoft.Extensions.Configuration.IConfiguration
    {
        public string? this[string key]
        {
            get => key == "IdAustria:Enabled" ? enabled : null;
            set { }
        }

        public Microsoft.Extensions.Configuration.IConfigurationSection GetSection(string key) => null!;

        public IEnumerable<Microsoft.Extensions.Configuration.IConfigurationSection> GetChildren() => [];

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
}

public sealed class VerificationProviderTests
{
    [Fact]
    public async Task Manual_verification_provider_supports_expected_types_and_generates_reference()
    {
        var provider = new ManualVerificationProvider();
        var userId = Guid.NewGuid();

        provider.Supports(VerificationType.Identity).Should().BeTrue();
        provider.Supports(VerificationType.Address).Should().BeTrue();
        provider.Supports(VerificationType.BackgroundCheck).Should().BeTrue();
        provider.Supports(VerificationType.Organization).Should().BeFalse();

        var startResult = await provider.StartAsync(userId, VerificationType.Identity, new VerificationContext());
        startResult.ExternalReference.Should().StartWith("MAN-");
        startResult.RequiresExternalRedirect.Should().BeFalse();

        var outcome = await provider.GetOutcomeAsync(startResult.ExternalReference);
        outcome.IsVerified.Should().BeTrue();
        outcome.ValidUntilUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Organization_verification_provider_includes_org_reference()
    {
        var provider = new OrganizationVerificationProvider();
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        provider.Supports(VerificationType.Organization).Should().BeTrue();
        provider.Supports(VerificationType.Training).Should().BeTrue();
        provider.Supports(VerificationType.Identity).Should().BeFalse();

        var startResult = await provider.StartAsync(userId, VerificationType.Organization, new VerificationContext(OrganizationId: orgId));
        startResult.ExternalReference.Should().Contain(orgId.ToString("N"));

        var outcome = await provider.GetOutcomeAsync(startResult.ExternalReference);
        outcome.IsVerified.Should().BeTrue();
    }
}
