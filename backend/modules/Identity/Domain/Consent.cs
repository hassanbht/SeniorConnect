using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Identity.Domain;

public sealed class Consent : Entity
{
    private Consent() { }

    [DataClass(DataClass.Operational)]
    public Guid UserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public ConsentType ConsentType { get; private set; }

    [DataClass(DataClass.Operational)]
    public string DocumentVersion { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public bool Granted { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset GrantedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? WithdrawnAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? IpHash { get; private set; }

    public static Consent Create(
        Guid userId,
        ConsentType consentType,
        string documentVersion,
        bool granted,
        string? ipHash = null)
    {
        return new Consent
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            ConsentType = consentType,
            DocumentVersion = documentVersion,
            Granted = granted,
            GrantedAtUtc = DateTimeOffset.UtcNow,
            IpHash = ipHash
        };
    }

    public void Withdraw()
    {
        WithdrawnAtUtc = DateTimeOffset.UtcNow;
        Granted = false;
    }
}

public enum ConsentType
{
    Terms,
    Privacy,
    NotificationsPush,
    NotificationsSms,
    NotificationsEmail,
    ResearchStatistics
}
