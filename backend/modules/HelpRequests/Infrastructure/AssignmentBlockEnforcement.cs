using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.HelpRequests.Application;
using SeniorConnect.Modules.HelpRequests.Domain;
using SeniorConnect.Modules.TrustSafety.Contracts;

namespace SeniorConnect.Modules.HelpRequests.Infrastructure;

/// <summary>
/// B-1 (Option B, approved 2026-09-30): releases every live assignment between
/// a newly blocked pair back to the open pool. Deterministic and idempotent:
/// re-running after the same block (or concurrent duplicate blocks) finds no
/// further live assignments and returns 0. Runs in the caller's transaction
/// scope (SaveChanges is called by the port itself so TrustSafety sees a
/// consistent state afterwards).
/// </summary>
public sealed class AssignmentBlockEnforcement(IHelpRequestsDbContext db) : IAssignmentBlockEnforcementPort
{
    public async Task<int> UnassignLiveAssignmentsBetweenAsync(Guid userA, Guid userB, CancellationToken ct = default)
    {
        var affected = await db.HelpRequests
            .Where(r =>
                (r.SeniorUserId == userA && r.AssignedVolunteerUserId == userB)
                || (r.SeniorUserId == userB && r.AssignedVolunteerUserId == userA))
            .Where(r => r.Status == HelpRequestStatus.Assigned || r.Status == HelpRequestStatus.InProgress)
            .ToListAsync(ct);

        var released = 0;
        foreach (var request in affected)
        {
            var volunteerId = request.AssignedVolunteerUserId!.Value;
            if (request.ReleaseAssignmentForSafety(volunteerId))
            {
                released++;

                var history = HelpRequestStatusHistory.Create(
                    helpRequestId: request.Id,
                    fromStatus: HelpRequestStatus.Assigned,
                    toStatus: HelpRequestStatus.Open,
                    changedByUserId: volunteerId,
                    reason: "Assignment released after a block between the participants (B-1).");

                db.HelpRequestStatusHistories.Add(history);
                // Audit: the status history row above IS the durable audit trail
                // (BR-AUDIT-01 pattern used by every status transition); its reason
                // field records the release cause without naming the block counterpart.
            }
        }

        if (released > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return released;
    }
}
