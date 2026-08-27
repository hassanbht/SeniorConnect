using FluentAssertions;
using SeniorConnect.Modules.TrustSafety.Domain;
using Xunit;

namespace SeniorConnect.Modules.TrustSafety.Tests;

public sealed class UserBlockTests
{
    [Fact]
    public void Block_record_creates_with_reason()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var block = UserBlock.Create(userA, userB, "Uncomfortable communication");

        block.BlockingUserId.Should().Be(userA);
        block.BlockedUserId.Should().Be(userB);
        block.Reason.Should().Be("Uncomfortable communication");
        block.CreatedAtUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }
}
