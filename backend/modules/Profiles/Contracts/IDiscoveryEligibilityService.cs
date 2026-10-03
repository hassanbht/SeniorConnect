namespace SeniorConnect.Modules.Profiles.Contracts;

/// <summary>Phase 4: why a list of people is being filtered.</summary>
public enum DiscoveryPurpose
{
    /// <summary>Browsing people nearby (e.g. nearby volunteers).</summary>
    OpenDiscovery,

    /// <summary>Candidates for a help request. Organization rules apply when the request has an organization.</summary>
    Matching,

    /// <summary>
    /// Showing items owned by these people (e.g. open help requests by
    /// seniors). Only blocks and discovery restrictions apply: publishing a
    /// request is itself the owner's choice to be seen.
    /// </summary>
    OwnedContent
}

public sealed record DiscoveryContext(DiscoveryPurpose Purpose, Guid? OrganizationId = null);

/// <summary>
/// Phase 4 (docs/security/verification-and-discovery.md §5): the one answer to
/// "may this person appear here?". It never decides how precisely their
/// location is shown; that stays with <see cref="ILocationVisibilityService"/>.
/// </summary>
public interface IDiscoveryEligibilityService
{
    /// <param name="counterpartUserId">
    /// The person on the other side: the viewer when browsing, the senior when
    /// matching. Blocks and pair restrictions are checked against them.
    /// </param>
    Task<IReadOnlySet<Guid>> FilterEligibleAsync(
        Guid counterpartUserId,
        IReadOnlyCollection<Guid> candidateUserIds,
        DiscoveryContext context,
        CancellationToken ct = default);
}
