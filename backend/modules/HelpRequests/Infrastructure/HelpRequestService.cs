using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.HelpRequests.Application;
using SeniorConnect.Modules.HelpRequests.Domain;
using SeniorConnect.Modules.Family.Contracts;
using SeniorConnect.Modules.Identity.Contracts;
using SeniorConnect.Modules.Profiles.Contracts;
using SeniorConnect.Modules.TrustSafety.Contracts;

namespace SeniorConnect.Modules.HelpRequests.Infrastructure;

public sealed class HelpRequestService : IHelpRequestService
{
    private readonly IHelpRequestsDbContext _db;
    private readonly IActivitySafetyPolicy _safetyPolicy;
    private readonly ITrustLevelReader _trustLevelReader;
    private readonly ISafetyBoundaryReader _safetyBoundaryReader;
    private readonly IUserContactReader _userContactReader;
    private readonly IVolunteerReliabilityUpdater _reliabilityUpdater;
    private readonly IFamilyPermissionReader _familyPermissionReader;

    public HelpRequestService(
        IHelpRequestsDbContext db,
        IActivitySafetyPolicy safetyPolicy,
        ITrustLevelReader trustLevelReader,
        ISafetyBoundaryReader safetyBoundaryReader,
        IUserContactReader userContactReader,
        IVolunteerReliabilityUpdater reliabilityUpdater,
        IFamilyPermissionReader? familyPermissionReader = null)
    {
        _db = db;
        _safetyPolicy = safetyPolicy;
        _trustLevelReader = trustLevelReader;
        _safetyBoundaryReader = safetyBoundaryReader;
        _userContactReader = userContactReader;
        _reliabilityUpdater = reliabilityUpdater;
        _familyPermissionReader = familyPermissionReader ?? new NoopFamilyPermissionReader();
    }

    public async Task<Result<HelpRequestDto>> CreateHelpRequestAsync(
        Guid createdByUserId,
        Guid seniorUserId,
        CreateHelpRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        // 0. Acting on behalf of senior permission check (BR-HELP-04)
        if (seniorUserId != createdByUserId)
        {
            var hasPermission = await _familyPermissionReader.HasPermissionAsync(
                caregiverUserId: createdByUserId,
                seniorUserId: seniorUserId,
                permissionType: "CreateHelpRequestsOnBehalf",
                ct: cancellationToken);

            if (!hasPermission)
            {
                return Error.Forbidden("You do not have permission to create help requests on behalf of this senior.");
            }
        }

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

        // BR-COMM-04: contact details only ever computed for the counterpart
        // authorized to see them (senior/creator/assigned volunteer) —
        // MapRequest re-checks this itself, so passing null when
        // unauthorized costs nothing extra here.
        var contact = await _userContactReader.GetContactAsync(helpRequest.SeniorUserId, cancellationToken);
        var volunteerContact = helpRequest.AssignedVolunteerUserId is { } assignedVolunteerId
            ? await _userContactReader.GetContactAsync(assignedVolunteerId, cancellationToken)
            : null;

        return Result<HelpRequestDto>.Success(MapRequest(helpRequest, requestingUserId, contact, volunteerContact));
    }

    public async Task<Result<IReadOnlyList<HelpRequestDto>>> GetSeniorHelpRequestsAsync(
        Guid seniorUserId,
        CancellationToken cancellationToken = default)
    {
        var requests = await _db.HelpRequests
            .Where(r => r.SeniorUserId == seniorUserId && !r.IsDeleted)
            .OrderByDescending(r => r.ScheduledStartUtc)
            .ToListAsync(cancellationToken);

        var dtos = new List<HelpRequestDto>(requests.Count);
        foreach (var r in requests)
        {
            var volunteerContact = r.AssignedVolunteerUserId is { } assignedVolunteerId
                ? await _userContactReader.GetContactAsync(assignedVolunteerId, cancellationToken)
                : null;
            dtos.Add(MapRequest(r, seniorUserId, volunteerContact: volunteerContact));
        }
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

        if (helpRequest.SeniorUserId == volunteerUserId)
        {
            return Error.Forbidden("Beneficiary cannot accept their own help request.");
        }

        // P4-16: Blocked user check
        var isBlocked = await _safetyBoundaryReader.IsBlockedAsync(helpRequest.SeniorUserId, volunteerUserId, cancellationToken);
        if (isBlocked)
        {
            return Error.Forbidden("Cannot accept request from a blocked user.");
        }

        // P4-06: Trust Level Invariant enforcement
        var volunteerTrustLevel = await _trustLevelReader.GetEffectiveTrustLevelAsync(volunteerUserId, cancellationToken);
        if (volunteerTrustLevel < helpRequest.RequiredTrustLevel)
        {
            return new Error(
                "TRUST_LEVEL_INSUFFICIENT",
                $"Your verified Trust Level ({volunteerTrustLevel}) is below the required Trust Level ({helpRequest.RequiredTrustLevel}) for this activity.",
                ErrorKind.Forbidden);
        }

        // P4-07: Buddy System Check for Safety Level 3+ (first 3 visits)
        if (helpRequest.RequiredSafetyLevel >= 3)
        {
            var isBuddyRequired = await _safetyBoundaryReader.IsBuddyRequiredForLevel3Async(volunteerUserId, cancellationToken);
            if (isBuddyRequired)
            {
                return new Error(
                    "BUDDY_REQUIRED",
                    "The first three Safety Level 3+ visits require an assigned experienced buddy volunteer or coordinator waiver.",
                    ErrorKind.Forbidden);
            }
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

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // BR-HELP-03 / Gate 3 item 2: two truly-simultaneous accepts can
            // both pass the in-memory RowVersion check above and only race
            // at the database's own concurrency token (xmin). The loser
            // must still get the same clean, non-blaming 409 the sequential
            // case returns — never an unhandled 500.
            return new Error(
                "CONCURRENCY_CONFLICT",
                "The request was modified or claimed by another user.",
                ErrorKind.Conflict);
        }

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

        // P3-20: a completed assignment nudges reliability toward reliable.
        await _reliabilityUpdater.RecordOutcomeAsync(volunteerUserId, wasReliable: true, cancellationToken);

        // P3-20: a completed assignment nudges reliability toward reliable.
        await _reliabilityUpdater.RecordOutcomeAsync(volunteerUserId, wasReliable: true, cancellationToken);

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
        var cancelResult = helpRequest.Cancel(cancelledByUserId, request.Reason, request.ReasonCode);
        if (cancelResult.IsFailure)
        {
            return cancelResult.Error!;
        }

        var history = HelpRequestStatusHistory.Create(
            helpRequestId: helpRequest.Id,
            fromStatus: fromStatus,
            toStatus: HelpRequestStatus.Cancelled,
            changedByUserId: cancelledByUserId,
            reason: $"{request.ReasonCode}: {request.Reason}");

        _db.HelpRequestStatusHistories.Add(history);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<HelpRequestDto>.Success(MapRequest(helpRequest, requestingUserId: cancelledByUserId));
    }

    public async Task<Result<HelpRequestDto>> MarkNoShowAsync(
        Guid helpRequestId,
        Guid reportedByUserId,
        NoShowHelpRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var helpRequest = await _db.HelpRequests
            .FirstOrDefaultAsync(r => r.Id == helpRequestId && !r.IsDeleted, cancellationToken);

        if (helpRequest is null)
        {
            return Error.NotFound("HelpRequest");
        }

        var fromStatus = helpRequest.Status;
        var noShowResult = helpRequest.MarkNoShow(reportedByUserId, request.Notes);
        if (noShowResult.IsFailure)
        {
            return noShowResult.Error!;
        }

        // P3-20/P3-19: nudge reliability down, but snapshot the prior value
        // first so a later successful dispute can restore it exactly.
        if (helpRequest.AssignedVolunteerUserId is { } volunteerUserId)
        {
            var previousScore = await _reliabilityUpdater.RecordOutcomeAsync(volunteerUserId, wasReliable: false, cancellationToken);
            helpRequest.RecordPreNoShowReliabilityScore(previousScore);
        }

        var history = HelpRequestStatusHistory.Create(
            helpRequestId: helpRequest.Id,
            fromStatus: fromStatus,
            toStatus: HelpRequestStatus.NoShow,
            changedByUserId: reportedByUserId,
            reason: request.Notes ?? "No-show recorded");

        _db.HelpRequestStatusHistories.Add(history);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<HelpRequestDto>.Success(MapRequest(helpRequest, requestingUserId: reportedByUserId));
    }

    public async Task<Result<HelpRequestDto>> DisputeNoShowAsync(
        Guid helpRequestId,
        Guid disputedByUserId,
        DisputeNoShowRequest request,
        CancellationToken cancellationToken = default)
    {
        var helpRequest = await _db.HelpRequests
            .FirstOrDefaultAsync(r => r.Id == helpRequestId && !r.IsDeleted, cancellationToken);

        if (helpRequest is null)
        {
            return Error.NotFound("HelpRequest");
        }

        var fromStatus = helpRequest.Status;
        var disputeResult = helpRequest.DisputeNoShow(disputedByUserId, request.Reason);
        if (disputeResult.IsFailure)
        {
            return disputeResult.Error!;
        }

        // BR-HELP-06: a successful dispute REVERTS the score, not just
        // nudges it back — restore the exact pre-no-show snapshot.
        if (helpRequest.AssignedVolunteerUserId is { } volunteerUserId)
        {
            await _reliabilityUpdater.RestoreScoreAsync(volunteerUserId, helpRequest.PreNoShowReliabilityScore, cancellationToken);
        }

        var history = HelpRequestStatusHistory.Create(
            helpRequestId: helpRequest.Id,
            fromStatus: fromStatus,
            toStatus: fromStatus, // dispute doesn't change the status, only the record
            changedByUserId: disputedByUserId,
            reason: $"No-show disputed: {request.Reason}");

        _db.HelpRequestStatusHistories.Add(history);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<HelpRequestDto>.Success(MapRequest(helpRequest, requestingUserId: disputedByUserId));
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

    private static HelpRequestDto MapRequest(
        HelpRequest r,
        Guid requestingUserId,
        UserContact? seniorContact = null,
        UserContact? volunteerContact = null)
    {
        // Contact details mask rule (BR-COMM-04): Address and precise contact info are only revealed
        // once assigned to the volunteer, or for the senior/creator themselves.
        var isAuthorizedToSeeAddress = requestingUserId == r.SeniorUserId
            || requestingUserId == r.CreatedByUserId
            || (r.AssignedVolunteerUserId.HasValue && r.AssignedVolunteerUserId.Value == requestingUserId);

        // Symmetric to seniorContact: once assigned, the senior/creator sees
        // WHO is coming — same BR-COMM-04 gate, the other direction.
        var isSeniorOrCreator = requestingUserId == r.SeniorUserId || requestingUserId == r.CreatedByUserId;

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
            RowVersion: r.RowVersion,
            SeniorDisplayName: isAuthorizedToSeeAddress ? seniorContact?.DisplayName : null,
            SeniorPhone: isAuthorizedToSeeAddress ? seniorContact?.Phone : null,
            VolunteerDisplayName: isSeniorOrCreator ? volunteerContact?.DisplayName : null,
            VolunteerPhone: isSeniorOrCreator ? volunteerContact?.Phone : null);
    }

    private sealed class NoopFamilyPermissionReader : IFamilyPermissionReader
    {
        public Task<bool> HasPermissionAsync(Guid caregiverUserId, Guid seniorUserId, string permissionType, CancellationToken ct = default)
            => Task.FromResult(true);
    }
}
