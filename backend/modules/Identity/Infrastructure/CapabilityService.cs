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

            // Derived safety capabilities from trust level
            if (currentTrustLevel >= 1) capabilities.Add("PerformSafetyLevel1");
            if (currentTrustLevel >= 2) capabilities.Add("PerformSafetyLevel2");
            if (currentTrustLevel >= 3) capabilities.Add("PerformSafetyLevel3");
            if (currentTrustLevel >= 4) capabilities.Add("PerformSafetyLevel4");
            if (currentTrustLevel >= 5) capabilities.Add("PerformSafetyLevel5");
        }

        // Fetch unexpired granted capabilities
        var now = DateTimeOffset.UtcNow;
        var grantedCapabilities = await _db.UserCapabilities
            .Where(c => c.UserId == user.Id && (c.ExpiresAtUtc == null || c.ExpiresAtUtc > now))
            .Select(c => c.Capability)
            .ToListAsync(cancellationToken);

        foreach (var cap in grantedCapabilities)
        {
            capabilities.Add(cap);
        }

        return capabilities.ToList();
    }
}
