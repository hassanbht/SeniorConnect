using FluentAssertions;
using SeniorConnect.Modules.Identity.Domain;
using SeniorConnect.Modules.Identity.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.TrustSafety.Tests;

public sealed class TrustLevelComputationTests
{
    private readonly TrustLevelCalculator _calculator = new();

    [Fact]
    public void Unverified_user_has_trust_level_0()
    {
        var user = User.CreateWithPhone("+436601234567", "Test User");
        var verifications = Array.Empty<Verification>();

        var eval = _calculator.Evaluate(user, verifications);

        eval.Level.Should().Be(0);
        eval.MissingForNextLevel.Should().Contain("verification.phone_or_email");
    }

    [Fact]
    public void Verified_phone_only_has_trust_level_1()
    {
        var user = User.CreateWithPhone("+436601234567", "Test User");
        user.VerifyPhone();

        var eval = _calculator.Evaluate(user, Array.Empty<Verification>());

        eval.Level.Should().Be(1);
        eval.MissingForNextLevel.Should().Contain("verification.email");
    }

    [Fact]
    public void Verified_phone_and_email_has_trust_level_2()
    {
        var user = User.CreateWithPhone("+436601234567", "Test User");
        user.VerifyPhone();
        user.VerifyEmail();

        var eval = _calculator.Evaluate(user, Array.Empty<Verification>());

        eval.Level.Should().Be(2);
        eval.MissingForNextLevel.Should().Contain("verification.address");
    }

    [Fact]
    public void Verified_address_has_trust_level_3()
    {
        var user = User.CreateWithPhone("+436601234567", "Test User");
        user.VerifyPhone();
        user.VerifyEmail();

        var addressV = Verification.Create(user.Id, VerificationType.Address);
        addressV.RecordOutcome(VerificationStatus.Verified);

        var eval = _calculator.Evaluate(user, new[] { addressV });

        eval.Level.Should().Be(3);
        eval.MissingForNextLevel.Should().Contain("verification.identity");
    }

    [Fact]
    public void Verified_official_id_has_trust_level_4()
    {
        var user = User.CreateWithPhone("+436601234567", "Test User");
        user.VerifyPhone();
        user.VerifyEmail();

        var addressV = Verification.Create(user.Id, VerificationType.Address);
        addressV.RecordOutcome(VerificationStatus.Verified);

        var idV = Verification.Create(user.Id, VerificationType.Identity);
        idV.RecordOutcome(VerificationStatus.Verified);

        var eval = _calculator.Evaluate(user, new[] { addressV, idV });

        eval.Level.Should().Be(4);
        eval.MissingForNextLevel.Should().Contain("verification.background_check");
    }

    [Fact]
    public void Verified_criminal_background_check_has_trust_level_5()
    {
        var user = User.CreateWithPhone("+436601234567", "Test User");
        user.VerifyPhone();
        user.VerifyEmail();

        var addressV = Verification.Create(user.Id, VerificationType.Address);
        addressV.RecordOutcome(VerificationStatus.Verified);

        var idV = Verification.Create(user.Id, VerificationType.Identity);
        idV.RecordOutcome(VerificationStatus.Verified);

        var bgV = Verification.Create(user.Id, VerificationType.BackgroundCheck);
        bgV.RecordOutcome(VerificationStatus.Verified);

        var eval = _calculator.Evaluate(user, new[] { addressV, idV, bgV });

        eval.Level.Should().Be(5);
        eval.MissingForNextLevel.Should().BeEmpty();
    }

    [Fact]
    public void Expired_verification_drops_trust_level_purely_and_deterministically()
    {
        var user = User.CreateWithPhone("+436601234567", "Test User");
        user.VerifyPhone();
        user.VerifyEmail();

        var addressV = Verification.Create(user.Id, VerificationType.Address);
        addressV.Verify(Guid.NewGuid(), validUntilUtc: DateTimeOffset.UtcNow.AddDays(-1)); // Expired

        var eval = _calculator.Evaluate(user, new[] { addressV });

        // Address expired -> drops back to level 2
        eval.Level.Should().Be(2);
        eval.MissingForNextLevel.Should().Contain("verification.address");
    }
}
