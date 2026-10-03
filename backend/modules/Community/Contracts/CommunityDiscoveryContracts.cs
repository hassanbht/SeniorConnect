namespace SeniorConnect.Modules.Community.Contracts;

// Other modules may reference ONLY this namespace. Never Domain, never
// Application, never Infrastructure. An architecture test enforces it.

/// <summary>
/// P5-06 (BR-GEO-05 style): lets the Geography module discover upcoming
/// community events for proximity/interest search without depending on
/// Community's Domain/Application/Infrastructure. Only the minimal fields
/// needed for distance filtering and interest matching are exposed — never
/// the host, description, or exact address.
/// </summary>
public interface ICommunityDiscoveryReader
{
    Task<IReadOnlyList<CommunityEventDiscoveryRow>> FindUpcomingEventsForDiscoveryAsync(CancellationToken ct);
}

public sealed record CommunityEventDiscoveryRow(
    Guid EventId, string Title, string Category, string? PostalCode, DateTimeOffset StartsAtUtc);
