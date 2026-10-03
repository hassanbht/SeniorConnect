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

    /// <summary>
    /// Phase 4: how a manually recorded fact was established (e.g. a staff
    /// member saw a Meldebestätigung). Null for facts recorded by the system
    /// (email link, SMS code, ID Austria).
    /// </summary>
    [DataClass(DataClass.Operational)]
    public VerificationMethod? Method { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? RevokedByUserId { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? RevocationReason { get; private set; }

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

    /// <summary>The only state in which a fact counts: verified, not expired, not revoked.</summary>
    [DataClass(DataClass.Operational)]
    public bool IsCurrentlyValid => Status == VerificationStatus.Verified && !IsExpired;

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

    /// <summary>
    /// Phase 4: a staff-recorded fact (address or organization). The verifier,
    /// organization and method are mandatory; the fact never comes from the
    /// person it describes.
    /// </summary>
    public static Result<Verification> RecordManual(
        Guid userId,
        VerificationType type,
        VerificationMethod method,
        Guid verifiedByUserId,
        Guid verifiedByOrganizationId,
        DateTimeOffset? validUntilUtc = null)
    {
        if (type is not (VerificationType.Address or VerificationType.Organization))
        {
            return Error.Validation("Only address and organization facts can be recorded manually.");
        }
        if (verifiedByUserId == userId)
        {
            return Error.Forbidden("A person cannot verify themselves.");
        }
        var validMethod = type == VerificationType.Address
            ? method is VerificationMethod.DocumentSighted or VerificationMethod.OrganizationRecord
            : method == VerificationMethod.OrganizationMembership;
        if (!validMethod)
        {
            return Error.Validation($"Method {method} does not establish a {type} fact.");
        }
        if (validUntilUtc.HasValue && validUntilUtc.Value <= DateTimeOffset.UtcNow)
        {
            return Error.Validation("validUntilUtc must be in the future.");
        }

        var verification = Create(userId, type, VerificationProvider.Organization, verifiedByUserId);
        verification.Method = method;
        verification.Verify(verifiedByUserId, validUntilUtc, verifiedByOrganizationId);
        return Result<Verification>.Success(verification);
    }

    /// <summary>
    /// Phase 4: a revoked fact stops counting immediately. The row stays as
    /// history (BR-AUDIT-03); it is never deleted.
    /// </summary>
    public Result Revoke(Guid revokedByUserId, string reason)
    {
        if (Status != VerificationStatus.Verified)
        {
            return Error.Conflict("Only a verified fact can be revoked.");
        }
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("A revocation reason is required.");
        }

        var now = DateTimeOffset.UtcNow;
        Status = VerificationStatus.Revoked;
        RevokedAtUtc = now;
        RevokedByUserId = revokedByUserId;
        RevocationReason = reason.Trim();
        UpdatedAtUtc = now;
        UpdatedBy = revokedByUserId;
        return Result.Success();
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
    Expired,
    Revoked
}

/// <summary>Phase 4: how a manually recorded fact was established.</summary>
public enum VerificationMethod
{
    /// <summary>Staff saw a residence document (e.g. Meldebestätigung) in person. Nothing is stored (BR-TRUST-05).</summary>
    DocumentSighted,
    /// <summary>The organization already holds a confirmed address from its own intake.</summary>
    OrganizationRecord,
    /// <summary>The organization vouches for an active member.</summary>
    OrganizationMembership
}

public enum VerificationProvider
{
    Manual,
    Organization,
    IdAustria,
    Kyc
}
