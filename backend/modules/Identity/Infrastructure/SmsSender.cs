using Microsoft.Extensions.Logging;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Identity.Infrastructure;

/// <summary>
/// P1-07: SMS adapter. No gateway is contracted yet, so:
/// - Development: the message (including OTP codes) is written to the log so
///   developers can sign in. Development never holds real personal data.
/// - Everywhere else: the message is never logged, and the send fails with
///   SMS_PROVIDER_NOT_CONFIGURED instead of pretending it was delivered.
/// </summary>
public sealed class SmsSender : ISmsSender
{
    private readonly ILogger<SmsSender> _logger;
    private readonly bool _logMessagesForDevelopment;

    public SmsSender(ILogger<SmsSender> logger, bool logMessagesForDevelopment)
    {
        _logger = logger;
        _logMessagesForDevelopment = logMessagesForDevelopment;
    }

    public Task<Result> SendSmsAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        // Safe logging - mask the phone number per privacy guidelines
        var maskedPhone = phoneNumber.Length > 6
            ? $"{phoneNumber[..3]}***{phoneNumber[^3..]}"
            : "***";

        if (!_logMessagesForDevelopment)
        {
            _logger.LogError("[SMS Adapter] No SMS gateway configured; message to {MaskedPhone} not sent.", maskedPhone);
            return Task.FromResult(Result.Failure(Error.ProviderUnavailable(
                "SMS_PROVIDER_NOT_CONFIGURED", "Text messages cannot be sent right now.")));
        }

        _logger.LogInformation("[SMS Adapter][Development] SMS to {MaskedPhone}: {Message}", maskedPhone, message);
        return Task.FromResult(Result.Success());
    }
}
