using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Identity.Application;

public interface ISmsSender
{
    Task<Result> SendSmsAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
}
