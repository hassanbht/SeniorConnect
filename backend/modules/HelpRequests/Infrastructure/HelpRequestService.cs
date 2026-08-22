using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.HelpRequests.Application;
using SeniorConnect.Modules.HelpRequests.Domain;

namespace SeniorConnect.Modules.HelpRequests.Infrastructure;

public sealed class HelpRequestService : IHelpRequestService
{
    private readonly IHelpRequestsDbContext _db;
    private readonly IActivitySafetyPolicy _safetyPolicy;

    public HelpRequestService(IHelpRequestsDbContext db, IActivitySafetyPolicy safetyPolicy)
    {
        _db = db;
        _safetyPolicy = safetyPolicy;
    }

    public async Task<Result<HelpRequestDto>> CreateHelpRequestAsync(
        Guid createdByUserId,
        Guid seniorUserId,
        CreateHelpRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Emergency Detection (BR-SCOPE-04)
        if (EmergencyDetector.IsEmergency(request.Notes))
        {
            return new Error(
                "EMERGENCY_DETECTED",
                "This request describes an acute medical emergency. Please call 144 / 112 immediately.",
                ErrorKind.Validation);
        }

        // 2. Category & Blocked Category Rule (BR-SCOPE-02/03)
        var category = await _db.ActivityCategories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.IsActive, cancellationToken);

        if (category is null)
        {
            return Error.NotFound("ActivityCategory");
        }

        if (category.IsBlocked)
        {
            return Error.CategoryBlocked(category.ReferralGroup ?? "general_support");
        }

        // 3. Compute Safety & Trust Level Server-Side (BR-SAFETY-01/02)
        var safetyEvaluation = _safetyPolicy.Evaluate(
            category: category,
            locationType: request.LocationType,
            transportMode: request.TransportMode,
            isSubjectVulnerable: false);

        var createResult = HelpRequest.Create(
            organizationId: request.OrganizationId,
            seniorUserId: seniorUserId,
            createdByUserId: createdByUserId,
            categoryId: request.CategoryId,
            safetyLevel: safetyEvaluation.RequiredSafetyLevel,
            trustLevel: safetyEvaluation.RequiredTrustLevel,
            scheduledStartUtc: request.ScheduledStartUtc,
            scheduledEndUtc: request.ScheduledEndUtc,
            durationMinutes: request.DurationMinutes,
            locationType: request.LocationType,
            notes: request.Notes,
            address: request.Address,
            postalCode: request.PostalCode,
            city: request.City,
            latitude: request.Latitude,
            longitude: request.Longitude,
            transportMode: request.TransportMode,
            insuranceContext: request.InsuranceContext);

        if (createResult.IsFailure)
        {
            return createResult.Error!;
        }

        var helpRequest = createResult.Value!;
        _db.HelpRequests.Add(helpRequest);

        var history = HelpRequestStatusHistory.Create(
            helpRequestId: helpRequest.Id,
            fromStatus: HelpRequestStatus.Draft,
            toStatus: HelpRequestStatus.Open,
            changedByUserId: createdByUserId,
            reason: "Request created");

        _db.HelpRequestStatusHistories.Add(history);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<HelpRequestDto>.Success(MapRequest(helpRequest, requestingUserId: createdByUserId));
    }

    public async Task<Result<HelpRequestDto>> GetHelpRequestByIdAsync(
        Guid helpRequestId,
        Guid requestingUserId,
        CancellationToken cancellationToken = default)
    {
        var helpRequest = await _db.HelpRequests
            .FirstOrDefaultAsync(r => r.Id == helpRequestId && !r.IsDeleted, cancellationToken);

        if (helpRequest is null)
        {
            return Error.NotFound("HelpRequest");
        }

        return Result<HelpRequestDto>.Success(MapRequest(helpRequest, requestingUserId));
    }

    public async Task<Result<IReadOnlyList<HelpRequestDto>>> GetSeniorHelpRequestsAsync(
        Guid seniorUserId,
        CancellationToken cancellationToken = default)
    {
        var requests = await _db.HelpRequests
            .Where(r => r.SeniorUserId == seniorUserId && !r.IsDeleted)
            .OrderByDescending(r => r.ScheduledStartUtc)
            .ToListAsync(cancellationToken);

        var dtos = requests.Select(r => MapRequest(r, seniorUserId)).ToList();
        return Result<IReadOnlyList<HelpRequestDto>>.Success(dtos);
    }

    public async Task<Result<IReadOnlyList<HelpRequestDto>>> GetOpenHelpRequestsAsync(
        Guid? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.HelpRequests
            .Where(r => (r.Status == HelpRequestStatus.Open || r.Status == HelpRequestStatus.Offered || r.Status == HelpRequestStatus.Matching) && !r.IsDeleted);

        if (organizationId.HasValue)
        {
            query = query.Where(r => r.OrganizationId == organizationId);
        }

        var requests = await query
            .OrderBy(r => r.ScheduledStartUtc)
            .ToListAsync(cancellationToken);

        var dtos = requests.Select(r => MapRequest(r, requestingUserId: Guid.Empty)).ToList();
        return Result<IReadOnlyList<HelpRequestDto>>.Success(dtos);
    }

    public async Task<Result<HelpRequestDto>> AcceptHelpRequestAsync(
        Guid helpRequestId,
        Guid volunteerUserId,
        AcceptHelpRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var helpRequest = await _db.HelpRequests
            .FirstOrDefaultAsync(r => r.Id == helpRequestId && !r.IsDeleted, cancellationToken);

        if (helpRequest is null)
        {
            return Error.NotFound("HelpRequest");
        }

        var fromStatus = helpRequest.Status;
        var assignResult = helpRequest.Assign(volunteerUserId, request.ExpectedRowVersion);
        if (assignResult.IsFailure)
        {
            return assignResult.Error!;
        }

        var history = HelpRequestStatusHistory.Create(
            helpRequestId: helpRequest.Id,
            fromStatus: fromStatus,
            toStatus: HelpRequestStatus.Assigned,
            changedByUserId: volunteerUserId,
            reason: "Accepted by volunteer");

        _db.HelpRequestStatusHistories.Add(history);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<HelpRequestDto>.Success(MapRequest(helpRequest, requestingUserId: volunteerUserId));
    }

    public async Task<Result<HelpRequestDto>> CheckInHelpRequestAsync(
        Guid helpRequestId,
        Guid volunteerUserId,
        CancellationToken cancellationToken = default)
    {
        var helpRequest = await _db.HelpRequests
            .FirstOrDefaultAsync(r => r.Id == helpRequestId && !r.IsDeleted, cancellationToken);

        if (helpRequest is null)
        {
            return Error.NotFound("HelpRequest");
        }

        var fromStatus = helpRequest.Status;
        var checkInResult = helpRequest.CheckIn(volunteerUserId);
        if (checkInResult.IsFailure)
        {
            return checkInResult.Error!;
        }

        var history = HelpRequestStatusHistory.Create(
            helpRequestId: helpRequest.Id,
            fromStatus: fromStatus,
            toStatus: HelpRequestStatus.InProgress,
            changedByUserId: volunteerUserId,
            reason: "Volunteer checked in");

        _db.HelpRequestStatusHistories.Add(history);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<HelpRequestDto>.Success(MapRequest(helpRequest, requestingUserId: volunteerUserId));
    }

    public async Task<Result<HelpRequestDto>> CompleteHelpRequestAsync(
        Guid helpRequestId,
        Guid volunteerUserId,
        CompleteHelpRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var helpRequest = await _db.HelpRequests
            .FirstOrDefaultAsync(r => r.Id == helpRequestId && !r.IsDeleted, cancellationToken);

        if (helpRequest is null)
        {
            return Error.NotFound("HelpRequest");
        }

        var category = await _db.ActivityCategories
            .FirstOrDefaultAsync(c => c.Id == helpRequest.CategoryId, cancellationToken);

        if (category is null)
        {
            return Error.NotFound("ActivityCategory");
        }

        var fromStatus = helpRequest.Status;
        var completeResult = helpRequest.Complete(volunteerUserId, request.ActualDurationMinutes);
        if (completeResult.IsFailure)
        {
            return completeResult.Error!;
        }

        // Bridge to Phase 2 Activity (P3-17)
        var today = DateOnly.FromDateTime(helpRequest.ScheduledStartUtc.UtcDateTime);
        var activityLogResult = Activity.Log(
            organizationId: helpRequest.OrganizationId,
            volunteerUserId: volunteerUserId,
            subjectUserId: helpRequest.SeniorUserId,
            category: category,
            occurredOn: today,
            durationMinutes: helpRequest.DurationMinutes,
            locationType: helpRequest.LocationType,
            transportMode: helpRequest.TransportMode,
            insuranceContext: helpRequest.InsuranceContext,
            source: ActivitySource.FromHelpRequest,
            loggedByUserId: volunteerUserId,
            today: DateOnly.FromDateTime(DateTime.UtcNow),
            notes: helpRequest.Notes);

        if (activityLogResult.IsSuccess && activityLogResult.Value is not null)
        {
            var activity = activityLogResult.Value;
            _db.Activities.Add(activity);
        }

        var history = HelpRequestStatusHistory.Create(
            helpRequestId: helpRequest.Id,
            fromStatus: fromStatus,
            toStatus: HelpRequestStatus.Completed,
            changedByUserId: volunteerUserId,
            reason: "Visit completed");

        _db.HelpRequestStatusHistories.Add(history);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<HelpRequestDto>.Success(MapRequest(helpRequest, requestingUserId: volunteerUserId));
    }

    public async Task<Result<HelpRequestDto>> CancelHelpRequestAsync(
        Guid helpRequestId,
        Guid cancelledByUserId,
        CancelHelpRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var helpRequest = await _db.HelpRequests
            .FirstOrDefaultAsync(r => r.Id == helpRequestId && !r.IsDeleted, cancellationToken);

        if (helpRequest is null)
        {
            return Error.NotFound("HelpRequest");
        }

        var fromStatus = helpRequest.Status;
        var cancelResult = helpRequest.Cancel(cancelledByUserId, request.Reason);
        if (cancelResult.IsFailure)
        {
            return cancelResult.Error!;
        }

        var history = HelpRequestStatusHistory.Create(
            helpRequestId: helpRequest.Id,
            fromStatus: fromStatus,
            toStatus: HelpRequestStatus.Cancelled,
            changedByUserId: cancelledByUserId,
            reason: request.Reason);

        _db.HelpRequestStatusHistories.Add(history);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<HelpRequestDto>.Success(MapRequest(helpRequest, requestingUserId: cancelledByUserId));
    }

    public async Task<Result<IReadOnlyList<HelpRequestStatusHistoryDto>>> GetStatusHistoryAsync(
        Guid helpRequestId,
        CancellationToken cancellationToken = default)
    {
        var history = await _db.HelpRequestStatusHistories
            .Where(h => h.HelpRequestId == helpRequestId)
            .OrderBy(h => h.ChangedAtUtc)
            .ToListAsync(cancellationToken);

        var dtos = history.Select(h => new HelpRequestStatusHistoryDto(
            Id: h.Id,
            HelpRequestId: h.HelpRequestId,
            FromStatus: h.FromStatus,
            ToStatus: h.ToStatus,
            ChangedByUserId: h.ChangedByUserId,
            ChangedAtUtc: h.ChangedAtUtc,
            Reason: h.Reason)).ToList();

        return Result<IReadOnlyList<HelpRequestStatusHistoryDto>>.Success(dtos);
    }

    private static HelpRequestDto MapRequest(HelpRequest r, Guid requestingUserId)
    {
        // Contact details mask rule (BR-COMM-04): Address and precise contact info are only revealed
        // once assigned to the volunteer, or for the senior/creator themselves.
        var isAuthorizedToSeeAddress = requestingUserId == r.SeniorUserId 
            || requestingUserId == r.CreatedByUserId 
            || (r.AssignedVolunteerUserId.HasValue && r.AssignedVolunteerUserId.Value == requestingUserId);

        return new HelpRequestDto(
            Id: r.Id,
            OrganizationId: r.OrganizationId,
            BranchId: r.BranchId,
            SeniorUserId: r.SeniorUserId,
            CreatedByUserId: r.CreatedByUserId,
            CategoryId: r.CategoryId,
            RequiredSafetyLevel: r.RequiredSafetyLevel,
            RequiredTrustLevel: r.RequiredTrustLevel,
            ScheduledStartUtc: r.ScheduledStartUtc,
            ScheduledEndUtc: r.ScheduledEndUtc,
            DurationMinutes: r.DurationMinutes,
            LocationType: r.LocationType,
            LocationAddress: isAuthorizedToSeeAddress ? r.LocationAddress : null,
            LocationPostalCode: r.LocationPostalCode,
            LocationCity: r.LocationCity,
            Latitude: r.Latitude,
            Longitude: r.Longitude,
            Notes: r.Notes,
            TransportMode: r.TransportMode,
            InsuranceContext: r.InsuranceContext,
            Status: r.Status,
            AssignedVolunteerUserId: r.AssignedVolunteerUserId,
            AssignedAtUtc: r.AssignedAtUtc,
            CheckedInAtUtc: r.CheckedInAtUtc,
            CompletedAtUtc: r.CompletedAtUtc,
            CancellationReason: r.CancellationReason,
            RowVersion: r.RowVersion);
    }
}
