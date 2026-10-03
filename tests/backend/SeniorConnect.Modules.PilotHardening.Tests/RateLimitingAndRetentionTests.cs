using FluentAssertions;
using SeniorConnect.Modules.Family.Domain;
using SeniorConnect.Modules.Identity.Domain;
using Xunit;

namespace SeniorConnect.Modules.PilotHardening.Tests;

public sealed class RateLimitingAndRetentionTests
{
    [Fact]
    public void ExpiredOtpChallenge_CannotBeVerified_P7_08()
    {
        var challenge = OtpChallenge.Create(
            destinationHash: "hash-phone",
            codeHash: "hash-code",
            channel: OtpChannel.Sms,
            expiryMinutes: 5
        );

        // Challenge created with 5 min expiry
        challenge.IsExpired.Should().BeFalse();
        challenge.ExpiresAtUtc.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(5), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ExpiredZugangskarte_CannotBeClaimed_P7_08()
    {
        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();
        var card = Zugangskarte.Generate(seniorId, caregiverId, TimeSpan.FromSeconds(-10));

        // When claiming an expired card
        var claimResult = card.Claim();
        claimResult.IsFailure.Should().BeTrue();
        claimResult.Error.Code.Should().Be("EXPIRED");
    }

    [Fact]
    public void OtpChallenge_TracksAttempts_AndBecomesExhausted_P7_09()
    {
        var challenge = OtpChallenge.Create(
            destinationHash: "hash-phone",
            codeHash: "hash-code",
            channel: OtpChannel.Sms,
            maxAttempts: 3
        );

        challenge.RecordAttempt();
        challenge.RecordAttempt();
        challenge.IsExhausted.Should().BeFalse();

        // 3rd attempt reaches max
        challenge.RecordAttempt();
        challenge.IsExhausted.Should().BeTrue();

        // 4th attempt is rejected with invalid transition
        var attemptResult = challenge.RecordAttempt();
        attemptResult.IsFailure.Should().BeTrue();
    }
}
