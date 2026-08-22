namespace SeniorConnect.Infrastructure;

public sealed class DefaultTenantContext : ITenantContext
{
    public Guid? OrganizationId { get; set; }
    public bool IsPlatformScope { get; set; }
}
