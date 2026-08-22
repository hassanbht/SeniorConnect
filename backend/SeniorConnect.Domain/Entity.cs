namespace SeniorConnect.Domain;

public abstract class Entity
{
    public Guid Id { get; protected set; }

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

public interface IDomainEvent
{
    DateTimeOffset OccurredAtUtc { get; }
}

/// <summary>
/// Marks an entity that belongs to an organization.
///
/// OrganizationId is deliberately NULLABLE (ADR-007): a null organization means
/// "independent / community", which is a first-class case, not a missing value.
///
/// Every implementor MUST have an EF global query filter. An architecture test
/// enforces this — do not delete that test.
/// </summary>
public interface IOrganizationScoped
{
    Guid? OrganizationId { get; }
}

public interface IAuditable
{
    DateTimeOffset CreatedAtUtc { get; }
    Guid? CreatedBy { get; }
    DateTimeOffset UpdatedAtUtc { get; }
    Guid? UpdatedBy { get; }
}

public interface ISoftDeletable
{
    bool IsDeleted { get; }
}
