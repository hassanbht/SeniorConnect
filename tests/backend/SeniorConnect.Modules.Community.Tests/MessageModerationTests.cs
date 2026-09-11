using FluentAssertions;
using SeniorConnect.Modules.Community.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Community.Tests;

/// <summary>BR-COMM-05 / ADR-020 — the local message-moderation classifier.</summary>
public sealed class MessageModerationTests
{
    private readonly LocalMessageModerationService _sut = new();

    [Fact]
    public void Benign_message_is_not_flagged()
    {
        var verdict = _sut.Screen("Hallo zusammen, wer bringt am Samstag die Karten mit?");

        verdict.IsFlagged.Should().BeFalse();
        verdict.Reason.Should().BeNull();
    }

    [Theory]
    [InlineData("Bitte überweise das Geld auf mein Konto, IBAN AT611904300234573201")]
    [InlineData("Kannst du mir kurz deine Kontodaten schicken?")]
    public void Bank_or_money_request_is_flagged(string content)
    {
        var verdict = _sut.Screen(content);

        verdict.IsFlagged.Should().BeTrue();
        verdict.Reason.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("Sie haben gewonnen! Klicken Sie hier um Ihren Preis zu erhalten.")]
    [InlineData("Verifizieren Sie ihr Konto sofort, dringend Geld überweisen.")]
    public void Scam_pattern_is_flagged(string content)
    {
        var verdict = _sut.Screen(content);

        verdict.IsFlagged.Should().BeTrue();
    }

    [Fact]
    public void Harassment_language_is_flagged()
    {
        var verdict = _sut.Screen("Halt die Klappe, du Idiot.");

        verdict.IsFlagged.Should().BeTrue();
    }

    [Fact]
    public void Pasted_phone_number_is_flagged()
    {
        var verdict = _sut.Screen("Ruf mich an unter 0664 1234567, dann klären wir das.");

        verdict.IsFlagged.Should().BeTrue();
    }

    [Fact]
    public void Empty_content_is_never_flagged()
    {
        var verdict = _sut.Screen("   ");

        verdict.IsFlagged.Should().BeFalse();
    }
}
