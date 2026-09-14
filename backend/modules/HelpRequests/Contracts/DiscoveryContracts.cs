namespace SeniorConnect.Modules.HelpRequests.Contracts;

// Other modules may reference ONLY this namespace. Never Domain, never
// Application, never Infrastructure. An architecture test enforces it.

/// <summary>
/// BR-GEO-05: lets the Geography module discover open help requests for
/// proximity search without depending on HelpRequests' Domain/Application/
/// Infrastructure. Only the minimal fields needed for distance filtering and
/// identification are exposed — never SeniorUserId or other PII (BR-GEO-06).
/// </summary>
public interface IHelpRequestDiscoveryReader
{
    Task<IReadOnlyList<NearbyHelpRequestRow>> FindOpenRequestsWithLocationAsync(CancellationToken ct);
}

public sealed record NearbyHelpRequestRow(
    Guid Id, Guid CategoryId, double Latitude, double Longitude);
