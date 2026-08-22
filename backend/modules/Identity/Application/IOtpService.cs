using SeniorConnect.Modules.Identity.Domain;
using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Identity.Application;

public interface IOtpService
{
    Task<Result<string>> GenerateAndSendPhoneOtpAsync(
        string phone,
        OtpPurpose purpose,
        string? ipAddress,
        string preferredLocale,
        CancellationToken cancellationToken = default);

    Task<Result<string>> GenerateAndSendEmailOtpAsync(
        string email,
        OtpPurpose purpose,
        string? ipAddress,
        string preferredLocale,
        CancellationToken cancellationToken = default);

    Task<Result> ValidateAndConsumeOtpAsync(
        string destination,
        string code,
        OtpChannel channel,
        OtpPurpose purpose,
        CancellationToken cancellationToken = default);
}
