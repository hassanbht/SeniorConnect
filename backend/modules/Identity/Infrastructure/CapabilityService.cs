using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Identity.Application;
using SeniorConnect.Modules.Identity.Domain;

namespace SeniorConnect.Modules.Identity.Infrastructure;

public sealed class CapabilityService : ICapabilityService
{
    private readonly IIdentityDbContext _db;

    public CapabilityService(IIdentityDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<string>> ResolveCapabilitiesAsync(
        User user,
        short currentTrustLevel,
        CancellationToken cancellationToken = default)
    {
        var capabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Basic user capabilities for all active accounts
        if (user.Status == UserStatus.Active)
        {
            capabilities.Add("CreateHelpRequest");
            capabilities.Add("JoinPublicEvent");
            capabilities.Add("CreateCommunityGroup");
            capabilities.Add("ManageSupportProfile");
            capabilities.Add("ViewSupportedPersonActivities");
            capabilities.Add("ViewSupportedPersonHelpRequests");

            // Levels 1 and 2 restate the trust ladder: reachable contact, then
            // verified contact (authorization.md §3). They are pure trust gates.
            if (currentTrustLevel >= 1) capabilities.Add("PerformSafetyLevel1");
            if (currentTrustLevel >= 2) capabilities.Add("PerformSafetyLevel2");

            // Levels 3–5 are NOT a trust-level ladder (authorization.md §3).
            // Trust 4 means "official identity verified" and trust 5 means
            // "background check verified"; deriving the capability from the
            // level alone would hand a home-visit capability to someone whose
            // only extra fact is a passport check. Each of them additionally
            // requires its own recorded facts, read fresh here.
            var facts = await LoadSafetyFactsAsync(user.Id, cancellationToken);
            AddDerivedSafetyLevels(capabilities, currentTrustLevel, facts);
        }

        // Fetch unexpired granted capabilities (P4-04: PerformSafetyLevel* can NEVER be hand-granted)
        var now = DateTimeOffset.UtcNow;
        var grantedCapabilities = await _db.UserCapabilities
            .Where(c => c.UserId == user.Id && (c.ExpiresAtUtc == null || c.ExpiresAtUtc > now))
            .Select(c => c.Capability)
            .ToListAsync(cancellationToken);

        foreach (var cap in grantedCapabilities)
        {
            if (!cap.StartsWith("PerformSafetyLevel", StringComparison.OrdinalIgnoreCase))
            {
                capabilities.Add(cap);
            }
        }

        return capabilities.ToList();
    }

    /// <summary>
    /// The persisted facts level 3–5 additionally require. Read per call so a
    /// revoked or expired fact stops counting immediately.
    /// </summary>
    private async Task<SafetyFacts> LoadSafetyFactsAsync(Guid userId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // The expiry test is written against ValidUntilUtc rather than the
        // computed IsExpired property so it translates to SQL in every provider.
        var verifications = await _db.Verifications
            .AsNoTracking()
            .Where(v => v.UserId == userId
                        && v.Status == VerificationStatus.Verified
                        && (!v.ValidUntilUtc.HasValue || v.ValidUntilUtc > now))
            .Select(v => v.Type)
            .ToListAsync(ct);

        return new SafetyFacts(
            HasTraining: verifications.Contains(VerificationType.Training),
            HasBackgroundCheck: verifications.Contains(VerificationType.BackgroundCheck),
            HasOrganizationApproval: verifications.Contains(VerificationType.Organization));
    }

    /// <summary>
    /// authorization.md §3 — PerformSafetyLevel3 ← trust&gt;=3 AND training AND
    /// (org approval OR platform review); Level4 ← trust&gt;=5 AND background
    /// check AND org approval; Level5 ← Level4 AND named coordinator. The
    /// coordinator assignment is a request-level decision enforced by the
    /// assignment path, never derivable from a user-level fact, so it is
    /// deliberately not granted here — it would otherwise be permanently
    /// true for anyone who ever reached level 4.
    /// </summary>
    private static void AddDerivedSafetyLevels(
        HashSet<string> capabilities,
        short currentTrustLevel,
        SafetyFacts facts)
    {
        if (currentTrustLevel >= 3 && facts.HasTraining && facts.HasOrganizationApproval)
        {
            capabilities.Add("PerformSafetyLevel3");
        }

        // Home visits: the highest-risk gate. Trust level 5 alone is the
        // background check; the organization approval is a separate fact and
        // must be present too.
        if (currentTrustLevel >= 5 && facts.HasBackgroundCheck && facts.HasOrganizationApproval)
        {
            capabilities.Add("PerformSafetyLevel4");
        }

        // PerformSafetyLevel5 additionally requires a named coordinator to be
        // assigned to the specific activity, which is checked at assignment
        // time (ActivitySafetyPolicy.RequiredNamedCoordinator), not here.
    }

    private sealed record SafetyFacts(
        bool HasTraining,
        bool HasBackgroundCheck,
        bool HasOrganizationApproval);
}
