using SeniorConnect.Modules.Identity.Domain;
using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Identity.Application;

public interface ITokenService
{
    string GenerateAccessToken(
        User user,
        IReadOnlyList<string> capabilities,
        short trustLevel);

    Task<Result<(string RawToken, RefreshToken Entity)>> CreateRefreshTokenAsync(
        Guid userId,
        string? deviceLabel,
        bool isPersonalDevice,
        CancellationToken cancellationToken = default);

    Task<Result<(string NewRawToken, RefreshToken NewEntity, User User)>> RotateRefreshTokenAsync(
        string rawRefreshToken,
        string? newDeviceLabel,
        CancellationToken cancellationToken = default);

    Task<Result> RevokeRefreshTokenAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken = default);

    Task<Result> RevokeDeviceSessionAsync(
        Guid userId,
        Guid refreshTokenId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes every active refresh token for the user. Used for SIM-swap
    /// mitigation on phone-number change (BR-AUTH-06).
    /// </summary>
    Task<Result> RevokeAllSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeviceSessionDto>> GetActiveDevicesAsync(
        Guid userId,
        string? currentRawRefreshToken,
        CancellationToken cancellationToken = default);
}
