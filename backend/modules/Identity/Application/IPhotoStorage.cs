using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Identity.Application;

public interface IPhotoStorage
{
    Task<Result<string>> SaveProfilePhotoAsync(Guid userId, Stream content, string contentType, CancellationToken cancellationToken = default);
}
