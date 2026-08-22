using SeniorConnect.Modules.HelpRequests.Domain;
using SeniorConnect.Domain;

namespace SeniorConnect.Modules.HelpRequests.Application;

public sealed record ConfirmActivityCommand(Guid ActivityId, string? ETag);

/// <summary>
/// The authorization pipeline runs in the order defined in
/// docs/architecture/authorization.md §2. Each step returns the error kind that
/// section prescribes — in particular, a cross-tenant miss returns NotFound,
/// never Forbidden, because a 403 confirms the resource exists.
/// </summary>
public sealed class ConfirmActivityHandler(
    IActivityRepository repository,
    ICapabilityService capabilities,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<Guid>> HandleAsync(
        ConfirmActivityCommand command, CancellationToken ct)
    {
        // 3. Tenant scope. The repository applies the global query filter, so a
        //    row belonging to another organization is simply not found.
        var activity = await repository.FindAsync(command.ActivityId, ct);
        if (activity is null)
        {
            return Error.NotFound("Activity");
        }

        // 4. Capability.
        if (!await capabilities.HasAsync(
                currentUser.UserId, Capability.ConfirmActivityHours,
                activity.OrganizationId, ct))
        {
            return Error.CapabilityMissing(nameof(Capability.ConfirmActivityHours));
        }

        // 5. Relationship. A volunteer may not confirm their own hours — that
        //    would make the number the product is sold on self-reported.
        if (activity.VolunteerUserId == currentUser.UserId)
        {
            return new Error(
                "SELF_CONFIRMATION_NOT_ALLOWED",
                "Hours must be confirmed by someone other than the volunteer.",
                ErrorKind.Forbidden);
        }

        // 8. Domain invariants, including BR-TRANSPORT-04.
        var result = activity.Confirm(currentUser.UserId);
        if (result.IsFailure)
        {
            return result.Error!;
        }

        await audit.WriteAsync(new AuditEntry(
            ActorUserId: currentUser.UserId,
            ActorOrganizationId: activity.OrganizationId,
            Action: "activity.confirmed",
            SubjectType: nameof(Activity),
            SubjectId: activity.Id,
            Reason: null), ct);

        await unitOfWork.SaveChangesAsync(ct);
        return activity.Id;
    }
}

// --- Ports. Implementations live in Infrastructure. --------------------------

public interface IActivityRepository
{
    Task<Activity?> FindAsync(Guid id, CancellationToken ct);
    Task<ActivityCategory?> FindCategoryAsync(Guid id, CancellationToken ct);
    void Add(Activity activity);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}

public interface ICurrentUser
{
    Guid UserId { get; }
    Guid? OrganizationId { get; }
}

public interface ICapabilityService
{
    Task<bool> HasAsync(Guid userId, Capability capability, Guid? organizationId,
        CancellationToken ct);
}

public interface IAuditWriter
{
    Task WriteAsync(AuditEntry entry, CancellationToken ct);
}

public sealed record AuditEntry(
    Guid ActorUserId,
    Guid? ActorOrganizationId,
    string Action,
    string SubjectType,
    Guid SubjectId,
    string? Reason);

public enum Capability
{
    LogOwnActivity,
    LogActivityForOthers,
    ConfirmActivityHours,
    ViewOrganizationDashboard,
    ManageOrganizationVolunteers,
    ApproveVerification,

    // Restricted. Never implied by an admin role. BR-SG-02.
    AccessSafeguarding,

    // Funder. Reaches only the aggregate namespace. BR-FUNDER-02.
    ViewFunderReports,
}
