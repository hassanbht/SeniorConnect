using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SeniorConnect.Modules.Identity.Application;

namespace SeniorConnect.Modules.Identity.Infrastructure;

/// <summary>
/// RFC 6238 TOTP implementation using only BCL crypto (HMACSHA1), 30s time
/// step, 6-digit codes, ±1 step clock drift tolerance on validation.
/// </summary>
public sealed class TotpService : ITotpService
{
    private const int SecretByteLength = 20;
    private const int TimeStepSeconds = 30;
    private const int Digits = 6;
    private const int DriftSteps = 1;
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(SecretByteLength);
        return Base32Encode(bytes);
    }

    public string BuildProvisioningUri(string secretBase32, string accountLabel, string issuer = "SeniorConnect")
    {
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedLabel = Uri.EscapeDataString(accountLabel);
        return $"otpauth://totp/{encodedIssuer}:{encodedLabel}?secret={secretBase32}&issuer={encodedIssuer}&digits={Digits}&period={TimeStepSeconds}";
    }

    public bool ValidateCode(string secretBase32, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var trimmedCode = code.Trim();
        var secretBytes = Base32Decode(secretBase32);
        var currentStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / TimeStepSeconds;

        for (var drift = -DriftSteps; drift <= DriftSteps; drift++)
        {
            var candidate = ComputeCode(secretBytes, currentStep + drift);
            if (candidate == trimmedCode)
            {
                return true;
            }
        }

        return false;
    }

    private static string ComputeCode(byte[] secretBytes, long timeStep)
    {
        var counter = BitConverter.GetBytes(timeStep);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counter);
        }

        // RFC 6238 mandates HMAC-SHA1 as the TOTP algorithm (interoperability with
        // standard authenticator apps) — not a general-purpose hash choice.
#pragma warning disable CA5350
        using var hmac = new HMACSHA1(secretBytes);
#pragma warning restore CA5350
        var hash = hmac.ComputeHash(counter);

        var offset = hash[^1] & 0x0F;
        var binaryCode =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        var truncated = binaryCode % (int)Math.Pow(10, Digits);
        return truncated.ToString(CultureInfo.InvariantCulture).PadLeft(Digits, '0');
    }

    private static string Base32Encode(byte[] data)
    {
        var result = new StringBuilder((data.Length * 8 + 4) / 5);
        var bitBuffer = 0;
        var bitsInBuffer = 0;

        foreach (var b in data)
        {
            bitBuffer = (bitBuffer << 8) | b;
            bitsInBuffer += 8;

            while (bitsInBuffer >= 5)
            {
                bitsInBuffer -= 5;
                var index = (bitBuffer >> bitsInBuffer) & 0x1F;
                result.Append(Base32Alphabet[index]);
            }
        }

        if (bitsInBuffer > 0)
        {
            var index = (bitBuffer << (5 - bitsInBuffer)) & 0x1F;
            result.Append(Base32Alphabet[index]);
        }

        return result.ToString();
    }

    private static byte[] Base32Decode(string base32)
    {
        var cleaned = base32.Trim().TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>((cleaned.Length * 5) / 8);
        var bitBuffer = 0;
        var bitsInBuffer = 0;

        foreach (var c in cleaned)
        {
            var index = Base32Alphabet.IndexOf(c);
            if (index < 0)
            {
                continue;
            }

            bitBuffer = (bitBuffer << 5) | index;
            bitsInBuffer += 5;

            if (bitsInBuffer >= 8)
            {
                bitsInBuffer -= 8;
                bytes.Add((byte)((bitBuffer >> bitsInBuffer) & 0xFF));
            }
        }

        return bytes.ToArray();
    }
}
