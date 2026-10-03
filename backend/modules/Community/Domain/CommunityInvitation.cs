using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Community.Domain;

public enum CommunityInvitationStatus
{
    Pending,
    Accepted,
    Declined,
    Expired,
    Revoked
}

public sealed class CommunityInvitation : Entity, IAuditable
{
    private CommunityInvitation() { }

    [DataClass(DataClass.Operational)]
    public Guid GroupId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid InvitedByUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? InvitedUserId { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? Email { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? Phone { get; private set; }

    [DataClass(DataClass.SensitiveData)]
    public string Token { get; private set; } = string.Empty;

    [DataClass(DataClass.Operational)]
    public CommunityInvitationStatus Status { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset ExpiresAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? AcceptedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static Result<CommunityInvitation> Create(
        Guid groupId,
        Guid invitedByUserId,
        Guid? invitedUserId = null,
        string? email = null,
        string? phone = null,
        TimeSpan? validity = null)
    {
        if (invitedUserId is null && string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
        {
            return Error.Validation("An invitation must target a user, email, or phone.");
        }

        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        var now = DateTimeOffset.UtcNow;
        return Result<CommunityInvitation>.Success(new CommunityInvitation
        {
            Id = Guid.CreateVersion7(),
            GroupId = groupId,
            InvitedByUserId = invitedByUserId,
            InvitedUserId = invitedUserId,
            Email = email?.Trim().ToLowerInvariant(),
            Phone = phone?.Trim(),
            Token = token,
            Status = CommunityInvitationStatus.Pending,
            ExpiresAtUtc = now.Add(validity ?? TimeSpan.FromDays(7)),
            CreatedAtUtc = now,
            CreatedBy = invitedByUserId,
            UpdatedAtUtc = now,
            UpdatedBy = invitedByUserId
        });
    }

    public Result Accept(Guid acceptingUserId)
    {
        if (Status != CommunityInvitationStatus.Pending)
        {
            return Error.Conflict("Invitation is not pending.");
        }

        if (DateTimeOffset.UtcNow > ExpiresAtUtc)
        {
            Status = CommunityInvitationStatus.Expired;
            UpdatedAtUtc = DateTimeOffset.UtcNow;
            UpdatedBy = acceptingUserId;
            return Error.Conflict("Invitation has expired.");
        }

        Status = CommunityInvitationStatus.Accepted;
        AcceptedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = acceptingUserId;
        return Result.Success();
    }

    public void Decline(Guid decliningUserId)
    {
        Status = CommunityInvitationStatus.Declined;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = decliningUserId;
    }

    public void Revoke(Guid revokingUserId)
    {
        Status = CommunityInvitationStatus.Revoked;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = revokingUserId;
    }
}
