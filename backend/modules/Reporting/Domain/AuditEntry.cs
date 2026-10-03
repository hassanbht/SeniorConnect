using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Reporting.Domain;

public sealed class AuditEntry : Entity
{
    private AuditEntry() { }

    [DataClass(DataClass.Operational)]
    public Guid? ActorUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? ActorOrganizationId { get; private set; }

    [DataClass(DataClass.Operational)]
    public string Action { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public string SubjectType { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public Guid? SubjectId { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? Reason { get; private set; }

    [DataClass(DataClass.Operational)]
    public string MetadataJson { get; private set; } = "{}";

    [DataClass(DataClass.Operational)]
    public string? CorrelationId { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? IpHash { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset AtUtc { get; private set; }

    public static AuditEntry Create(
        string action,
        string subjectType,
        Guid? subjectId = null,
        Guid? actorUserId = null,
        Guid? actorOrganizationId = null,
        string? reason = null,
        string metadataJson = "{}",
        string? correlationId = null,
        string? ipHash = null)
    {
        return new AuditEntry
        {
            Id = Guid.CreateVersion7(),
            Action = action,
            SubjectType = subjectType,
            SubjectId = subjectId,
            ActorUserId = actorUserId,
            ActorOrganizationId = actorOrganizationId,
            Reason = reason,
            MetadataJson = metadataJson,
            CorrelationId = correlationId,
            IpHash = ipHash,
            AtUtc = DateTimeOffset.UtcNow
        };
    }
}
