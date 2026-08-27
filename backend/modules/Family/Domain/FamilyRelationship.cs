using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Family.Domain;

public enum RelationshipType
{
    Child = 0,
    Spouse = 1,
    Sibling = 2,
    Neighbor = 3,
    LegalGuardian = 4,
    Other = 5
}

public enum RelationshipStatus
{
    PendingApproval = 0,
    Active = 1,
    Invited = 2,
    Revoked = 3
}

public sealed class FamilyRelationship
{
    public Guid Id { get; private set; }
    public Guid SeniorUserId { get; private set; }
    public Guid CaregiverUserId { get; private set; }
    public RelationshipType RelationshipType { get; private set; }
    public RelationshipStatus Status { get; private set; }
    public string? InvitationCode { get; private set; }
    public DateTimeOffset? InvitationExpiresAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ConfirmedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public Guid? RevokedByUserId { get; private set; }

    private readonly List<FamilyPermission> _permissions = [];
    public IReadOnlyCollection<FamilyPermission> Permissions => _permissions.AsReadOnly();

    private FamilyRelationship() { }

    public static FamilyRelationship CreateActive(
        Guid seniorUserId,
        Guid caregiverUserId,
        RelationshipType relationshipType)
    {
        var rel = new FamilyRelationship
        {
            Id = Guid.NewGuid(),
            SeniorUserId = seniorUserId,
            CaregiverUserId = caregiverUserId,
            RelationshipType = relationshipType,
            Status = RelationshipStatus.Active,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ConfirmedAtUtc = DateTimeOffset.UtcNow
        };

        // Initialize all permissions with defaults
        foreach (PermissionType p in Enum.GetValues<PermissionType>())
        {
            var isDefault = p == PermissionType.ViewActivities || p == PermissionType.ReceiveSafetyAlerts;
            rel._permissions.Add(new FamilyPermission(rel.Id, p, isDefault));
        }

        return rel;
    }

    public static FamilyRelationship CreateInvitation(
        Guid seniorUserId,
        RelationshipType relationshipType,
        string invitationCode,
        DateTimeOffset expiresAtUtc)
    {
        var rel = new FamilyRelationship
        {
            Id = Guid.NewGuid(),
            SeniorUserId = seniorUserId,
            CaregiverUserId = Guid.Empty, // Assigned upon claim
            RelationshipType = relationshipType,
            Status = RelationshipStatus.Invited,
            InvitationCode = invitationCode,
            InvitationExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        foreach (PermissionType p in Enum.GetValues<PermissionType>())
        {
            var isDefault = p == PermissionType.ViewActivities || p == PermissionType.ReceiveSafetyAlerts;
            rel._permissions.Add(new FamilyPermission(rel.Id, p, isDefault));
        }

        return rel;
    }

    public Result Accept(Guid caregiverUserId)
    {
        if (Status != RelationshipStatus.Invited && Status != RelationshipStatus.PendingApproval)
        {
            return Error.Conflict("INVALID_STATUS", $"Cannot accept a relationship in status {Status}.");
        }

        if (InvitationExpiresAtUtc.HasValue && InvitationExpiresAtUtc.Value < DateTimeOffset.UtcNow)
        {
            return Error.Conflict("INVITATION_EXPIRED", "This family invitation has expired.");
        }

        CaregiverUserId = caregiverUserId;
        Status = RelationshipStatus.Active;
        ConfirmedAtUtc = DateTimeOffset.UtcNow;
        InvitationCode = null;

        if (_permissions.Count == 0)
        {
            foreach (PermissionType p in Enum.GetValues<PermissionType>())
            {
                var isDefault = p == PermissionType.ViewActivities || p == PermissionType.ReceiveSafetyAlerts;
                _permissions.Add(new FamilyPermission(Id, p, isDefault));
            }
        }

        return Result.Success();
    }

    public Result Revoke(Guid revokedByUserId)
    {
        if (Status == RelationshipStatus.Revoked)
        {
            return Error.Conflict("ALREADY_REVOKED", "Relationship is already revoked.");
        }

        Status = RelationshipStatus.Revoked;
        RevokedAtUtc = DateTimeOffset.UtcNow;
        RevokedByUserId = revokedByUserId;

        // Revoke all granted permissions
        foreach (var p in _permissions)
        {
            p.Revoke();
        }

        return Result.Success();
    }

    public bool HasPermission(PermissionType type)
    {
        if (Status != RelationshipStatus.Active) return false;
        var perm = _permissions.FirstOrDefault(p => p.PermissionType == type);
        return perm != null && perm.IsGranted;
    }

    public void UpdatePermission(PermissionType type, bool isGranted)
    {
        var perm = _permissions.FirstOrDefault(p => p.PermissionType == type);
        if (perm == null)
        {
            _permissions.Add(new FamilyPermission(Id, type, isGranted));
        }
        else
        {
            if (isGranted) perm.Grant();
            else perm.Revoke();
        }
    }
}
