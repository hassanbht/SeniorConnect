using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Domain;
using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Identity.Infrastructure;

public sealed class JwtTokenService : ITokenService
{
    private readonly IIdentityDbContext _db;
    private readonly IIdentityHashingService _hashing;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly byte[] _signingKey;

    public JwtTokenService(
        IIdentityDbContext db,
        IIdentityHashingService hashing,
        IConfiguration configuration)
    {
        _db = db;
        _hashing = hashing;
        _issuer = configuration["Jwt:Issuer"] ?? "SeniorConnect.Api";
        _audience = configuration["Jwt:Audience"] ?? "SeniorConnect.Client";

        var secret = configuration["Jwt:SecretKey"] ?? "SeniorConnect_jwt_super_secret_signing_key_2026_default_secure_key_123456";
        _signingKey = Encoding.UTF8.GetBytes(secret);
    }

    public string GenerateAccessToken(
        User user,
        IReadOnlyList<string> capabilities,
        short trustLevel)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new("preferred_locale", user.PreferredLocale),
            new("senior_mode", user.SeniorModeDefault ? "true" : "false"),
            new("trust_level", trustLevel.ToString(System.Globalization.CultureInfo.InvariantCulture))
        };

        if (!string.IsNullOrWhiteSpace(user.Phone))
        {
            claims.Add(new(ClaimTypes.MobilePhone, user.Phone));
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new(ClaimTypes.Email, user.Email));
        }

        foreach (var capability in capabilities)
        {
            claims.Add(new("capability", capability));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _issuer,
            Audience = _audience,
            Expires = DateTime.UtcNow.AddMinutes(15),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(_signingKey),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public async Task<Result<(string RawToken, RefreshToken Entity)>> CreateRefreshTokenAsync(
        Guid userId,
        string? deviceLabel,
        bool isPersonalDevice,
        CancellationToken cancellationToken = default)
    {
        var rawBytes = RandomNumberGenerator.GetBytes(64);
        var rawToken = Convert.ToBase64String(rawBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        var tokenHash = _hashing.HashToken(rawToken);

        var lifetimeDays = isPersonalDevice ? 90 : 1; // 90 days for personal, 1 day for kiosk/shared

        var entity = RefreshToken.Create(
            userId: userId,
            tokenHash: tokenHash,
            deviceLabel: deviceLabel,
            isPersonalDevice: isPersonalDevice,
            lifetimeDays: lifetimeDays);

        _db.RefreshTokens.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<(string RawToken, RefreshToken Entity)>.Success((rawToken, entity));
    }

    public async Task<Result<(string NewRawToken, RefreshToken NewEntity, User User)>> RotateRefreshTokenAsync(
        string rawRefreshToken,
        string? newDeviceLabel,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = _hashing.HashToken(rawRefreshToken);

        var existingToken = await _db.RefreshTokens
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, cancellationToken);

        if (existingToken is null)
        {
            return new Error("TOKEN_NOT_FOUND", "Refresh token was not found.", ErrorKind.NotFound);
        }

        // Reuse detection: if token is already revoked, revoke all tokens for this user as a security safeguard
        if (!existingToken.IsActive)
        {
            var userTokens = await _db.RefreshTokens
                .Where(r => r.UserId == existingToken.UserId && r.RevokedAtUtc == null)
                .ToListAsync(cancellationToken);

            foreach (var token in userTokens)
            {
                token.Revoke();
            }

            await _db.SaveChangesAsync(cancellationToken);

            return new Error(
                "TOKEN_COMPROMISED",
                "Refresh token has already been used or revoked. All active sessions have been terminated.",
                ErrorKind.Unauthenticated);
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == existingToken.UserId && !u.IsDeleted, cancellationToken);

        if (user is null || user.Status != UserStatus.Active)
        {
            return new Error("USER_INACTIVE", "Account is not active.", ErrorKind.Forbidden);
        }

        // Generate new token
        var newBytes = RandomNumberGenerator.GetBytes(64);
        var newRawToken = Convert.ToBase64String(newBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        var newTokenHash = _hashing.HashToken(newRawToken);

        var lifetimeDays = existingToken.IsPersonalDevice ? 90 : 1;
        var newEntity = RefreshToken.Create(
            userId: user.Id,
            tokenHash: newTokenHash,
            deviceLabel: newDeviceLabel ?? existingToken.DeviceLabel,
            isPersonalDevice: existingToken.IsPersonalDevice,
            lifetimeDays: lifetimeDays);

        _db.RefreshTokens.Add(newEntity);

        // Revoke the old token and link to the replacement
        existingToken.Revoke(newEntity.Id);

        await _db.SaveChangesAsync(cancellationToken);

        return Result<(string NewRawToken, RefreshToken NewEntity, User User)>.Success((newRawToken, newEntity, user));
    }

    public async Task<Result> RevokeRefreshTokenAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = _hashing.HashToken(rawRefreshToken);

        var token = await _db.RefreshTokens
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash && r.RevokedAtUtc == null, cancellationToken);

        if (token is not null)
        {
            token.Revoke();
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result> RevokeDeviceSessionAsync(
        Guid userId,
        Guid refreshTokenId,
        CancellationToken cancellationToken = default)
    {
        var token = await _db.RefreshTokens
            .FirstOrDefaultAsync(r => r.Id == refreshTokenId && r.UserId == userId && r.RevokedAtUtc == null, cancellationToken);

        if (token is null)
        {
            return new Error("DEVICE_NOT_FOUND", "Device session was not found or already revoked.", ErrorKind.NotFound);
        }

        token.Revoke();
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> RevokeAllSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var tokens = await _db.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.Revoke();
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<IReadOnlyList<DeviceSessionDto>> GetActiveDevicesAsync(
        Guid userId,
        string? currentRawRefreshToken,
        CancellationToken cancellationToken = default)
    {
        var currentTokenHash = currentRawRefreshToken is not null
            ? _hashing.HashToken(currentRawRefreshToken)
            : null;

        var tokens = await _db.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAtUtc == null && r.ExpiresAtUtc > DateTimeOffset.UtcNow)
            .OrderByDescending(r => r.IssuedAtUtc)
            .ToListAsync(cancellationToken);

        return tokens.Select(t => new DeviceSessionDto(
            Id: t.Id,
            DeviceLabel: t.DeviceLabel,
            IsPersonalDevice: t.IsPersonalDevice,
            IssuedAtUtc: t.IssuedAtUtc,
            ExpiresAtUtc: t.ExpiresAtUtc,
            IsActive: t.IsActive,
            IsCurrent: t.TokenHash == currentTokenHash
        )).ToList();
    }
}
