namespace SeniorConnect.Modules.Identity.Application;

/// <summary>
/// RFC 6238 TOTP (time-based one-time password) generation and validation for
/// organization staff two-factor auth. Hand-rolled on BCL crypto only — no
/// external package (30s step, 6 digits, HMAC-SHA1).
/// </summary>
public interface ITotpService
{
    /// <summary>Generates a new random Base32-encoded shared secret.</summary>
    string GenerateSecret();

    /// <summary>Builds the standard otpauth:// URI for authenticator apps / QR rendering.</summary>
    string BuildProvisioningUri(string secretBase32, string accountLabel, string issuer = "SeniorConnect");

    /// <summary>Validates a 6-digit code against the secret, allowing ±1 step of clock drift.</summary>
    bool ValidateCode(string secretBase32, string code);
}
