using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Organizations.Domain;

public sealed class OrganizationPolicy : Entity, IOrganizationScoped
{
    private OrganizationPolicy() { }

    [DataClass(DataClass.Operational)]
    public Guid? OrganizationId { get; private set; }

    [DataClass(DataClass.Operational)]
    public string PolicyKey { get; private set; } = null!;

    [DataClass(DataClass.Operational)]
    public string PolicyValueJson { get; private set; } = "{}";

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static OrganizationPolicy Create(
        Guid organizationId,
        string policyKey,
        string policyValueJson,
        Guid? updatedBy = null)
    {
        return new OrganizationPolicy
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = organizationId,
            PolicyKey = policyKey,
            PolicyValueJson = policyValueJson,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedBy = updatedBy
        };
    }

    public void UpdateValue(string policyValueJson, Guid? updatedBy = null)
    {
        PolicyValueJson = policyValueJson;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }
}
