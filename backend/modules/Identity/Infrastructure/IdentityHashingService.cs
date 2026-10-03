using System.Security.Cryptography;
using System.Text;
using SeniorConnect.Modules.Identity.Application;

namespace SeniorConnect.Modules.Identity.Infrastructure;

public sealed class IdentityHashingService : IIdentityHashingService
{
    private const string GlobalSalt = "SeniorConnect_identity_v1_salt_secret_key_fixed";

    public string HashDestination(string destination)
    {
        var normalized = destination.Trim().ToLowerInvariant();
        return ComputeSha256($"{normalized}:{GlobalSalt}:dest");
    }

    public string HashCode(string code)
    {
        var normalized = code.Trim();
        return ComputeSha256($"{normalized}:{GlobalSalt}:code");
    }

    public string? HashIp(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return null;
        }

        return ComputeSha256($"{ipAddress.Trim()}:{GlobalSalt}:ip");
    }

    public string HashToken(string token)
    {
        return ComputeSha256($"{token}:{GlobalSalt}:token");
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
