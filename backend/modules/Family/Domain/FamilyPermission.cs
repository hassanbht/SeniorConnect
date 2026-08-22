namespace SeniorConnect.Modules.Family.Domain;

public enum PermissionType
{
    ViewActivities = 0,
    CreateHelpRequestsOnBehalf = 1,
    ViewEmergencyContacts = 2,
    ManageSettings = 3,
    ReceiveSafetyAlerts = 4
}

public sealed class FamilyPermission
{
    public Guid Id { get; private set; }
    public Guid FamilyRelationshipId { get; private set; }
    public PermissionType PermissionType { get; private set; }
    public bool IsGranted { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private FamilyPermission() { }

    public FamilyPermission(Guid familyRelationshipId, PermissionType permissionType, bool isGranted)
    {
        Id = Guid.NewGuid();
        FamilyRelationshipId = familyRelationshipId;
        PermissionType = permissionType;
        IsGranted = isGranted;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Grant()
    {
        IsGranted = true;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Revoke()
    {
        IsGranted = false;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
