using SeniorConnect.Modules.Identity.Domain;

namespace SeniorConnect.Modules.Identity.Application;

public interface ICapabilityService
{
    Task<IReadOnlyList<string>> ResolveCapabilitiesAsync(
        User user,
        short currentTrustLevel,
        CancellationToken cancellationToken = default);
}
