using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Reporting.Domain;

public sealed class FundingRelationship : Entity
{
    private FundingRelationship() { }

    [DataClass(DataClass.Operational)]
    public Guid FunderId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid OrganizationId { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateOnly ValidFrom { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateOnly? ValidUntil { get; private set; }

    [DataClass(DataClass.Operational)]
    public string ReportingScopeJson { get; private set; } = "{}";

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    public static FundingRelationship Create(
        Guid funderId,
        Guid organizationId,
        DateOnly validFrom,
        DateOnly? validUntil = null,
        string reportingScopeJson = "{}",
        Guid? createdBy = null)
    {
        return new FundingRelationship
        {
            Id = Guid.CreateVersion7(),
            FunderId = funderId,
            OrganizationId = organizationId,
            ValidFrom = validFrom,
            ValidUntil = validUntil,
            ReportingScopeJson = reportingScopeJson,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = createdBy
        };
    }
}
