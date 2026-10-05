using System.Security.Cryptography;
using System.Globalization;
using SeniorConnect.Modules.Identity.Application;

namespace SeniorConnect.Modules.Identity.Infrastructure;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hashing with a self-describing stored format:
///
///     pbkdf2-sha256$&lt;iterations&gt;$&lt;salt-b64&gt;$&lt;hash-b64&gt;
///
/// <para><b>Why the version prefix matters.</b> The stored hash records the
/// work factor it was created with, so the cost can be raised later without
/// invalidating existing passwords: <see cref="VerifyPassword"/> reads the
/// iteration count from the record and reports an old one as needing an upgrade,
/// and the caller re-hashes on the next successful login. Without it, raising
/// the cost would lock every existing user out.</para>
///
/// <para>Legacy records (the old bare <c>salt.hash</c> pair) are still verified
/// at their original cost so nobody is locked out by this change; they simply get
/// upgraded on next login.</para>
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    /// <summary>OWASP's recommended value for PBKDF2-HMAC-SHA256 (Cheat Sheet Series).
/// A work factor is a trade-off against the real environment: raise it only
/// after benchmarking login latency on production-grade hardware, and lower it
/// only through a new ADR. The stored format records the cost, so any future
/// change upgrades hashes transparently instead of locking users out.</summary>
    public const int CurrentIterations = 600_000;

    /// <summary>The cost the previous, versionless format used.</summary>
    public const int LegacyIterations = 100_000;

    /// <summary>Reject absurd iteration counts from a tampered stored record.</summary>
    private const int MaxIterations = 5_000_000;

    private const string Prefix = "pbkdf2-sha256";
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            CurrentIterations,
            Algorithm,
            KeySize);

        return string.Join('$', Prefix, CurrentIterations, Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    /// <summary>
    /// True when the record was produced with a weaker work factor than the
    /// current policy and should be re-hashed after a successful login.
    ///
    /// A legacy (versionless) record always qualifies: it carries no cost at
    /// all, so it is upgraded on the next login regardless.
    /// </summary>
    public bool NeedsRehash(string passwordHash)
    {
        if (!TryRead(passwordHash, out var iterations, out _, out _))
        {
            return false;
        }

        return iterations < CurrentIterations;
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (!TryRead(passwordHash, out var iterations, out var salt, out var expectedHash))
        {
            return false;
        }

        try
        {
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                Algorithm,
                expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryRead(string stored, out int iterations, out byte[] salt, out byte[] hash)
    {
        iterations = 0;
        salt = [];
        hash = [];

        if (string.IsNullOrWhiteSpace(stored))
        {
            return false;
        }

        var parts = stored.Split('$');

        // Current format: pbkdf2-sha256$iterations$salt$hash
        if (parts.Length == 4
            && string.Equals(parts[0], Prefix, StringComparison.Ordinal)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out iterations))
        {
            if (iterations <= 0 || iterations > MaxIterations)
            {
                iterations = 0;
                return false;
            }

            return TryDecode(parts[2], out salt) && TryDecode(parts[3], out hash);
        }

        // Legacy format: salt.hash at the cost it was written with.
        var legacy = stored.Split('.');
        if (legacy.Length == 2)
        {
            iterations = LegacyIterations;
            return TryDecode(legacy[0], out salt) && TryDecode(legacy[1], out hash);
        }

        return false;
    }

    private static bool TryDecode(string value, out byte[] decoded)
    {
        decoded = [];

        try
        {
            decoded = Convert.FromBase64String(value);
            return decoded.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
