using SeniorConnect.Modules.Identity.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Identity.Tests;

public sealed class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_ProducesNonEmptySaltAndHash()
    {
        var hash = _hasher.HashPassword("SuperSecret123!");

        Assert.NotNull(hash);
        Assert.Contains(".", hash);
        Assert.True(hash.Length > 20);
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        const string password = "StaffCoordinator2026!";
        var hash = _hasher.HashPassword(password);

        var isValid = _hasher.VerifyPassword(password, hash);

        Assert.True(isValid);
    }

    [Fact]
    public void VerifyPassword_WithIncorrectPassword_ReturnsFalse()
    {
        var hash = _hasher.HashPassword("CorrectPassword123!");

        var isValid = _hasher.VerifyPassword("WrongPassword123!", hash);

        Assert.False(isValid);
    }
}
