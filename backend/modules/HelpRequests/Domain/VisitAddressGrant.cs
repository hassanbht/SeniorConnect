using SeniorConnect.Domain;

namespace SeniorConnect.Modules.HelpRequests.Domain;

/// <summary>
/// ADR-026 Option C (approved 2026-09-30): a purpose-bound, assignment-bound,
/// volunteer-bound, revocable grant that releases the visit address to the
/// assigned volunteer while the assignment is live.
///
/// PRODUCT POLICY IMPLEMENTED — LEGAL VALIDATION STILL REQUIRED BEFORE
/// PRODUCTION ACTIVATION. The feature is disabled by default
/// (`VisitAddress:RequireGrant` in appsettings; see HelpRequestService).
///
/// No address data is stored here — only the consent decision. The grant is
/// not an expiry date: it ends when the assignment ends (existing gate).
/// </summary>
public sealed class VisitAddressGrant : Entity
{
    private VisitAddressGrant() { }

    [DataClass(DataClass.Operational)]
    public Guid HelpRequestId { get; private set; }

    /// <summary>The single recipient — the assigned volunteer.</summary>
    [DataClass(DataClass.PersonalData)]
    public Guid VolunteerUserId { get; private set; }

    /// <summary>Senior, or an authorized representative (BR-HELP-04), or — for org requests — the coordinator acting for the senior under the audited path.</summary>
    [DataClass(DataClass.PersonalData)]
    public Guid GrantedByUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public VisitAddressGrantIssuer GrantedByRole { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset GrantedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? RevokedByUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? RevokeReason { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsActive => RevokedAtUtc is null;

    public static Result<VisitAddressGrant> Create(
        Guid helpRequestId,
        Guid volunteerUserId,
        Guid grantedByUserId,
        VisitAddressGrantIssuer grantedByRole)
    {
        if (helpRequestId == Guid.Empty || volunteerUserId == Guid.Empty || grantedByUserId == Guid.Empty)
        {
            return Error.Validation("Grant identifiers are required.");
        }

        return Result<VisitAddressGrant>.Success(new VisitAddressGrant
        {
            Id = Guid.CreateVersion7(),
            HelpRequestId = helpRequestId,
            VolunteerUserId = volunteerUserId,
            GrantedByUserId = grantedByUserId,
            GrantedByRole = grantedByRole,
            GrantedAtUtc = DateTimeOffset.UtcNow
        });
    }

    /// <summary>Revocation is possible until check-in; after check-in the visit happened (ADR-026 plan §3).</summary>
    public Result Revoke(Guid revokedByUserId, string? reason)
    {
        if (!IsActive)
        {
            return Result.Success(); // idempotent
        }

        RevokedAtUtc = DateTimeOffset.UtcNow;
        RevokedByUserId = revokedByUserId;
        RevokeReason = reason;
        return Result.Success();
    }
}

public enum VisitAddressGrantIssuer
{
    Senior,
    AuthorizedRepresentative,
    CoordinatorOnBehalf
}
