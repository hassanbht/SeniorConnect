using Microsoft.Extensions.Logging;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Identity.Infrastructure;

public sealed class EmailSender : IEmailSender
{
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(ILogger<EmailSender> logger)
    {
        _logger = logger;
    }

    public Task<Result> SendEmailAsync(string recipientEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        var maskedEmail = MaskEmail(recipientEmail);
        _logger.LogInformation("[Email Adapter] Sending Email to {MaskedEmail} | Subject: {Subject}", maskedEmail, subject);

        return Task.FromResult(Result.Success());
    }

    private static string MaskEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 1) return "***@***";
        return $"{email[0]}***{email[(atIndex - 1)..]}";
    }
}
