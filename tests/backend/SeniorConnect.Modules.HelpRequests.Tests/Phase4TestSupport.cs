using Microsoft.EntityFrameworkCore;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Profiles.Infrastructure;
using SeniorConnect.Modules.TrustSafety.Infrastructure;

namespace SeniorConnect.Modules.HelpRequests.Tests;

/// <summary>Phase 4 test wiring: the real readers over InMemory databases.</summary>
internal static class Phase4TestSupport
{
    /// <summary>Safeguarding keeps its own DbContext (ADR-004), so tests get a separate InMemory store.</summary>
    public static SafeguardingDbContext NewSafeguardingDb() => new(
        new DbContextOptionsBuilder<SafeguardingDbContext>()
            .UseInMemoryDatabase("safeguarding-" + Guid.NewGuid())
            .Options);

    public static LocationVisibilityService LocationVisibility(SeniorConnectDbContext db, SafeguardingDbContext safeguarding) =>
        new(db, new SafetyBoundaryReader(db), new SafeguardingRestrictionReader(safeguarding));
}
