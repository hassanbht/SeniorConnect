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
        // Versioned layout: pbkdf2-sha256$iterations$salt$hash
        Assert.Equal(4, hash.Split('$').Length);
        var salt = Convert.FromBase64String(hash.Split('$')[2]);
        var derived = Convert.FromBase64String(hash.Split('$')[3]);
        Assert.Equal(16, salt.Length);
        Assert.Equal(32, derived.Length);
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

    // --- wave-2: work factor and transparent upgrade ------------------------

    [Fact]
    public void HashPassword_UsesTheOwaspWorkFactor()
    {
        var hash = _hasher.HashPassword("SuperSecret123!");

        Assert.StartsWith($"pbkdf2-sha256${PasswordHasher.CurrentIterations}$", hash);
        Assert.True(PasswordHasher.CurrentIterations >= 600_000,
            "OWASP's recommended value for PBKDF2-HMAC-SHA256; any change must be benchmarked on production-grade hardware");
    }

    [Fact]
    public void TheStoredFormatRecordsItsOwnCost()
    {
        var hash = _hasher.HashPassword("SuperSecret123!");

        // Without the version/cost prefix the cost could never be raised again
        // without invalidating every existing password.
        Assert.Equal(4, hash.Split('$').Length);
        Assert.False(_hasher.NeedsRehash(hash));
    }

    [Fact]
    public void ALegacyHashStillVerifies_SoNobodyIsLockedOut()
    {
        const string password = "LegacyUserPassword2026!";
        var salt = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
        var legacyHash = Convert.ToBase64String(System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
            password,
            Convert.FromBase64String(salt),
            PasswordHasher.LegacyIterations,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            32));
        var legacyRecord = $"{salt}.{legacyHash}";

        Assert.True(_hasher.VerifyPassword(password, legacyRecord),
            "a hash written before the upgrade must still authenticate its owner");
        Assert.False(_hasher.VerifyPassword("WrongPassword123!", legacyRecord));
    }

    [Fact]
    public void ALegacyHashIsFlaggedForUpgrade()
    {
        var salt = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
        var legacyHash = Convert.ToBase64String(new byte[32]);

        Assert.True(_hasher.NeedsRehash($"{salt}.{legacyHash}"),
            "an old-cost record should be re-derived on the next successful login");
    }

    [Fact]
    public void ATamperedRecordWithAnAbsurdCostIsRejected()
    {
        // A stored record must never be able to make the server do unbounded work.
        Assert.False(_hasher.VerifyPassword("pw", "pbkdf2-sha256$999999999$c2FsdA==$aGFzaA=="));
        Assert.False(_hasher.NeedsRehash("pbkdf2-sha256$999999999$c2FsdA==$aGFzaA=="));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-hash")]
    [InlineData("only.one.part")]
    [InlineData("pbkdf2-sha256$notanumber$c2FsdA==$aGFzaA==")]
    [InlineData("pbkdf2-sha256$1000$!!!$aGFzaA==")]
    public void MalformedRecordsNeverAuthenticate(string stored)
    {
        Assert.False(_hasher.VerifyPassword("any-password", stored));
    }
}
