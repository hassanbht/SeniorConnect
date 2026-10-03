using FluentAssertions;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Domain;
using SeniorConnect.Modules.Identity.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.TrustSafety.Tests;

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
