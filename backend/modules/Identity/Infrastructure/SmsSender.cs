using Microsoft.Extensions.Logging;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Identity.Infrastructure;

public sealed class SmsSender : ISmsSender
{
    private readonly ILogger<SmsSender> _logger;

    public SmsSender(ILogger<SmsSender> logger)
    {
        _logger = logger;
    }

    public Task<Result> SendSmsAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        // Safe logging - mask the phone number per privacy guidelines
        var maskedPhone = phoneNumber.Length > 6
            ? $"{phoneNumber[..3]}***{phoneNumber[^3..]}"
            : "***";

        _logger.LogInformation("[SMS Adapter] Sending SMS to {MaskedPhone}: {Message}", maskedPhone, message);

        return Task.FromResult(Result.Success());
    }
}
