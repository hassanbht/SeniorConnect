namespace SeniorConnect.Modules.Family.Domain;

public sealed class SeniorAccessLog
{
    public Guid Id { get; private set; }
    public Guid SeniorUserId { get; private set; }
    public Guid AccessedByUserId { get; private set; }
    public string AccessedByUserName { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string ResourceAccessed { get; private set; } = string.Empty;
    public string PlainLanguageDescription { get; private set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; private set; }
    public string? IpAddressHash { get; private set; }

    private SeniorAccessLog() { }

    public static SeniorAccessLog Create(
        Guid seniorUserId,
        Guid accessedByUserId,
        string accessedByUserName,
        string action,
        string resourceAccessed,
        string plainLanguageDescription,
        string? ipAddressHash = null)
    {
        return new SeniorAccessLog
        {
            Id = Guid.NewGuid(),
            SeniorUserId = seniorUserId,
            AccessedByUserId = accessedByUserId,
            AccessedByUserName = accessedByUserName,
            Action = action,
            ResourceAccessed = resourceAccessed,
            PlainLanguageDescription = plainLanguageDescription,
            TimestampUtc = DateTimeOffset.UtcNow,
            IpAddressHash = ipAddressHash
        };
    }
}
