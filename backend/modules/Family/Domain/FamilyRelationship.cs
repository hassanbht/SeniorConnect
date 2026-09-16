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
    public short FailedAttempts { get; private set; }
    public short MaxAttempts { get; private set; } = 5;

    public bool IsExhausted => FailedAttempts >= MaxAttempts;

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
            ConfirmedAtUtc = DateTimeOffset.UtcNow,
            MaxAttempts = 5,
            FailedAttempts = 0
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
            CreatedAtUtc = DateTimeOffset.UtcNow,
            MaxAttempts = 5,
            FailedAttempts = 0
        };

        foreach (PermissionType p in Enum.GetValues<PermissionType>())
        {
            var isDefault = p == PermissionType.ViewActivities || p == PermissionType.ReceiveSafetyAlerts;
            rel._permissions.Add(new FamilyPermission(rel.Id, p, isDefault));
        }

        return rel;
    }

    public void RecordFailedAttempt()
    {
        FailedAttempts++;
    }

    public Result Accept(Guid caregiverUserId)
    {
        if (Status != RelationshipStatus.Invited && Status != RelationshipStatus.PendingApproval)
        {
            return Error.Conflict("INVALID_STATUS", $"Cannot accept a relationship in status {Status}.");
        }

        if (IsExhausted)
        {
            return Error.Conflict("INVITATION_EXHAUSTED", "This invitation code has been locked due to too many failed attempts.");
        }

        if (caregiverUserId == SeniorUserId)
        {
            RecordFailedAttempt();
            return Error.Validation("A senior cannot become their own caregiver.");
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
        foreach (var perm in _permissions)
        {
            perm.Revoke();
        }

        return Result.Success();
    }

    public void UpdatePermission(PermissionType type, bool isGranted)
    {
        var existing = _permissions.FirstOrDefault(p => p.PermissionType == type);
        if (existing != null)
        {
            if (isGranted) existing.Grant();
            else existing.Revoke();
        }
        else
        {
            _permissions.Add(new FamilyPermission(Id, type, isGranted));
        }
    }

    public bool HasPermission(PermissionType type)
    {
        if (Status != RelationshipStatus.Active) return false;
        var perm = _permissions.FirstOrDefault(p => p.PermissionType == type);
        return perm != null && perm.IsGranted;
    }
}
