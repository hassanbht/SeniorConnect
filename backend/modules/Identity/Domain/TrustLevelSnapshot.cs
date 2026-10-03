using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Identity.Domain;

/// <summary>
/// Snapshot of every computed trust level so a past decision can be explained.
/// Trust levels 0–5, deterministic, side-effect free.
/// </summary>
public sealed class TrustLevelSnapshot : Entity
{
    private TrustLevelSnapshot() { }

    [DataClass(DataClass.Operational)]
    public Guid UserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public short Level { get; private set; }

    /// <summary>JSON explaining how the level was computed.</summary>
    [DataClass(DataClass.Operational)]
    public string ReasonJson { get; private set; } = "{}";

    [DataClass(DataClass.Operational)]
    public DateTimeOffset ComputedAtUtc { get; private set; }

    public static TrustLevelSnapshot Create(
        Guid userId,
        short level,
        string reasonJson)
    {
        return new TrustLevelSnapshot
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Level = level,
            ReasonJson = reasonJson,
            ComputedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
