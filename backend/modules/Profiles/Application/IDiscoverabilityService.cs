using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Profiles.Application;

/// <summary>Phase 4: the caller's own "people can find my profile" setting.</summary>
public interface IDiscoverabilityService
{
    Task<Result<DiscoverabilityDto>> GetAsync(Guid userId, CancellationToken ct = default);
    Task<Result<DiscoverabilityDto>> SetAsync(Guid userId, UpdateDiscoverabilityRequest request, CancellationToken ct = default);
}

public sealed record DiscoverabilityDto(bool IsDiscoverable, DateTimeOffset? UpdatedAtUtc);

public sealed record UpdateDiscoverabilityRequest(bool IsDiscoverable);
