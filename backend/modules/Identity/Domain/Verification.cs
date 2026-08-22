using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Identity.Domain;

/// <summary>
/// Verification outcomes ONLY — never the document (BR-TRUST-05).
/// Supports manual, organization, ID Austria and KYC providers via IIdentityVerificationProvider.
/// </summary>
public sealed class Verification : Entity, IAuditable
{
    private Verification() { }

    [DataClass(DataClass.Operational)]
    public Guid UserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public VerificationType Type { get; private set; }

    [DataClass(DataClass.Operational)]
    public VerificationStatus Status { get; private set; }

    [DataClass(DataClass.Operational)]
    public VerificationProvider Provider { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? VerifiedByUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? VerifiedByOrganizationId { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? VerifiedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? ValidUntilUtc { get; private set; }

    /// <summary>The provider's ID, NOT the document.</summary>
    [DataClass(DataClass.Operational)]
    public string? ExternalReference { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? RejectionReason { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsExpired => ValidUntilUtc.HasValue && DateTimeOffset.UtcNow > ValidUntilUtc.Value;

    public static Verification Create(
        Guid userId,
        VerificationType type,
        VerificationProvider? provider = null,
        Guid? createdBy = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new Verification
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Type = type,
            Status = VerificationStatus.NotStarted,
            Provider = provider ?? VerificationProvider.Manual,
            CreatedAtUtc = now,
            CreatedBy = createdBy,
            UpdatedAtUtc = now,
            UpdatedBy = createdBy
        };
    }

    /// <summary>
    /// Convenience method used by tests and simple workflows to set status directly.
    /// For production use, prefer <see cref="Verify"/> or <see cref="Reject"/>.
    /// </summary>
    public void RecordOutcome(VerificationStatus status, string? rejectionReason = null)
    {
        Status = status;
        if (status == VerificationStatus.Verified)
        {
            VerifiedAtUtc = DateTimeOffset.UtcNow;
        }
        else if (status == VerificationStatus.Rejected && rejectionReason is not null)
        {
            RejectionReason = rejectionReason;
        }
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Result Verify(Guid verifiedByUserId, DateTimeOffset? validUntilUtc = null,
        Guid? verifiedByOrganizationId = null, string? externalReference = null)
    {
        Status = VerificationStatus.Verified;
        VerifiedByUserId = verifiedByUserId;
        VerifiedByOrganizationId = verifiedByOrganizationId;
        VerifiedAtUtc = DateTimeOffset.UtcNow;
        ValidUntilUtc = validUntilUtc;
        ExternalReference = externalReference;
        return Result.Success();
    }

    public Result Reject(string reason)
    {
        Status = VerificationStatus.Rejected;
        RejectionReason = reason;
        return Result.Success();
    }

    public void Expire()
    {
        Status = VerificationStatus.Expired;
    }
}

public enum VerificationType
{
    Email,
    Phone,
    Identity,
    Address,
    Organization,
    Training,
    BackgroundCheck
}

public enum VerificationStatus
{
    NotStarted,
    Pending,
    Submitted,
    Verified,
    Rejected,
    Expired
}

public enum VerificationProvider
{
    Manual,
    Organization,
    IdAustria,
    Kyc
}
