using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Identity.Application;

public interface IEmailSender
{
    Task<Result> SendEmailAsync(string recipientEmail, string subject, string body, CancellationToken cancellationToken = default);
}
