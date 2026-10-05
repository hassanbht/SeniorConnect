using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.HelpRequests.Application;
using SeniorConnect.Modules.HelpRequests.Domain;
using SeniorConnect.Modules.Family.Contracts;
using SeniorConnect.Modules.Identity.Contracts;
using SeniorConnect.Modules.Organizations.Contracts;
using SeniorConnect.Modules.Profiles.Contracts;
using SeniorConnect.Modules.TrustSafety.Contracts;

namespace SeniorConnect.Modules.HelpRequests.Infrastructure;

public sealed class HelpRequestService : IHelpRequestService
{
    private readonly IHelpRequestsDbContext _db;
    private static readonly string[] DefaultMissingVerifications = ["Profil-Bestätigung (z. B. Identität, Telefon oder Organisation)"];

    private readonly IActivitySafetyPolicy _safetyPolicy;
    private readonly ITrustLevelReader _trustLevelReader;
    private readonly ISafetyBoundaryReader _safetyBoundaryReader;
    private readonly IUserContactReader _userContactReader;
    private readonly IVolunteerReliabilityUpdater _reliabilityUpdater;
    private readonly IFamilyPermissionReader _familyPermissionReader;
    private readonly IOrganizationAccessGuard? _accessGuard;
    private readonly ILocationVisibilityService? _locationVisibility;
    private readonly ISafeguardingRestrictionReader? _restrictions;
    private readonly bool _visitAddressGrantRequired;

    public HelpRequestService(
        IHelpRequestsDbContext db,
        IActivitySafetyPolicy safetyPolicy,
        ITrustLevelReader trustLevelReader,
        ISafetyBoundaryReader safetyBoundaryReader,
        IUserContactReader userContactReader,
        IVolunteerReliabilityUpdater reliabilityUpdater,
        IFamilyPermissionReader? familyPermissionReader = null,
        IOrganizationAccessGuard? accessGuard = null,
        ILocationVisibilityService? locationVisibility = null,
        ISafeguardingRestrictionReader? restrictions = null,
        Microsoft.Extensions.Options.IOptions<VisitAddressOptions>? visitAddressOptions = null)
    {
        _db = db;
        _visitAddressGrantRequired = visitAddressOptions?.Value.RequireGrant ?? false;
        _safetyPolicy = safetyPolicy;
        _trustLevelReader = trustLevelReader;
        _safetyBoundaryReader = safetyBoundaryReader;
        _userContactReader = userContactReader;
        _reliabilityUpdater = reliabilityUpdater;
        _familyPermissionReader = familyPermissionReader ?? new NoopFamilyPermissionReader();
        // Fail closed: without the guard, nobody is recognised as a coordinator.
        _accessGuard = accessGuard;
        // Fail closed: without it, every owner counts as not having consented.
        _locationVisibility = locationVisibility;
        // Phase 4: registered in production; without it only blocks apply.
        _restrictions = restrictions;
    }

    private async Task<IReadOnlyList<ActiveRestriction>> RestrictionsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct) =>
        _restrictions is null ? [] : await _restrictions.GetActiveAsync(userIds, ct);

    /// <summary>Phase 4: a block or a discovery restriction hides the owner's requests from the viewer.</summary>
    private async Task<bool> IsHiddenFromAsync(Guid ownerUserId, Guid viewerUserId, CancellationToken ct) =>
        await _safetyBoundaryReader.IsBlockedAsync(ownerUserId, viewerUserId, ct)
        || (await RestrictionsAsync([ownerUserId, viewerUserId], ct)).Restricts(RestrictionKind.Discovery, ownerUserId, viewerUserId);

    /// <summary>
    /// Phase 4 (ADR-026 hardening): BR-COMM-04 contact and address are withheld
    /// between senior and volunteer while they are blocked or a safeguarding
    /// contact restriction applies to them.
    /// </summary>
    private async Task<bool> IsContactWithheldAsync(HelpRequest r, CancellationToken ct)
    {
        if (r.AssignedVolunteerUserId is not { } volunteer)
        {
            return false;
        }
        return await _safetyBoundaryReader.IsBlockedAsync(r.SeniorUserId, volunteer, ct)
            || (await RestrictionsAsync([r.SeniorUserId, volunteer], ct)).Restricts(RestrictionKind.Contact, r.SeniorUserId, volunteer);
    }

    /// <summary>
    /// ADR-026: "the assigned counterparty" is the volunteer of a live
    /// assignment only. A cancelled, completed or no-show request no longer
    /// has one, even though AssignedVolunteerUserId keeps the history.
    /// </summary>
    private static bool IsLiveAssignee(HelpRequest r, Guid userId) =>
        r.AssignedVolunteerUserId == userId
        && r.Status is HelpRequestStatus.Assigned or HelpRequestStatus.InProgress;

    public async Task<Result<HelpRequestDto>> CreateHelpRequestAsync(
        Guid createdByUserId,
        Guid seniorUserId,
        CreateHelpRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!GeoCoordinate.IsValidPair(request.Latitude, request.Longitude))
        {
            return GeoCoordinate.Invalid();
        }

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

        // 1b. Typed nursing/medical tasks are refused exactly like a blocked category (BR-SCOPE-02/03, P3-04)
        if (NursingTaskDetector.IsNursingTask(request.Notes))
        {
            var nursingReferralGroup = await _db.ActivityCategories
                .Where(c => c.Code == NursingTaskDetector.NursingCategoryCode)
                .Select(c => c.ReferralGroup)
                .FirstOrDefaultAsync(cancellationToken);

            return Error.CategoryBlocked(nursingReferralGroup ?? NursingTaskDetector.NursingCategoryCode);
        }

        // 1c. Schedule shape (P3-21): stable codes instead of a generic domain message
        if (request.ScheduledStartUtc == default)
        {
            return new Error("SCHEDULED_START_REQUIRED", "scheduledStartUtc is required.", ErrorKind.Validation);
        }

        if (request.ScheduledEndUtc == default)
        {
            return new Error("SCHEDULED_END_REQUIRED", "scheduledEndUtc is required.", ErrorKind.Validation);
        }

        if (request.ScheduledEndUtc <= request.ScheduledStartUtc)
        {
            return new Error("SCHEDULED_END_BEFORE_START", "scheduledEndUtc must be after scheduledStartUtc.", ErrorKind.Validation);
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
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == helpRequestId && !r.IsDeleted, cancellationToken);

        if (helpRequest is null)
        {
            return Error.NotFound("HelpRequest");
        }

        var isParty = await IsPartyAsync(helpRequest, requestingUserId, cancellationToken);

        // A non-party may look at a request only while it is still open for
        // offers (same data the open list shows, redacted). Once it is
        // assigned or closed it does not exist for them: 404, not 403.
        if (!isParty)
        {
            return IsOpenForOffers(helpRequest.Status)
                   && !await IsHiddenFromAsync(helpRequest.SeniorUserId, requestingUserId, cancellationToken)
                ? Result<HelpRequestDto>.Success(MapRequest(
                    helpRequest,
                    requestingUserId,
                    isParty: false,
                    discoveryDisclosure: await DecideLocationAsync(helpRequest.SeniorUserId, requestingUserId, LocationRelationship.DiscoveryViewer, cancellationToken)))
                : Error.NotFound("HelpRequest");
        }

        // BR-COMM-04: contact details only ever computed for the counterpart
        // authorized to see them (senior/creator/assigned volunteer);
        // MapRequest re-checks this itself.
        var contact = await _userContactReader.GetContactAsync(helpRequest.SeniorUserId, cancellationToken);
        var volunteerContact = helpRequest.AssignedVolunteerUserId is { } assignedVolunteerId
            ? await _userContactReader.GetContactAsync(assignedVolunteerId, cancellationToken)
            : null;

        return Result<HelpRequestDto>.Success(MapRequest(
            helpRequest, requestingUserId, contact, volunteerContact,
            withholdContact: await IsContactWithheldAsync(helpRequest, cancellationToken)));
    }

    public async Task<Result<IReadOnlyList<HelpRequestDto>>> GetSeniorHelpRequestsAsync(
        Guid seniorUserId,
        CancellationToken cancellationToken = default)
    {
        var requests = await _db.HelpRequests
            .AsNoTracking()
            .Where(r => r.SeniorUserId == seniorUserId && !r.IsDeleted)
            .OrderByDescending(r => r.ScheduledStartUtc)
            .ToListAsync(cancellationToken);

        var dtos = new List<HelpRequestDto>(requests.Count);
        foreach (var r in requests)
        {
            var volunteerContact = r.AssignedVolunteerUserId is { } assignedVolunteerId
                ? await _userContactReader.GetContactAsync(assignedVolunteerId, cancellationToken)
                : null;
            dtos.Add(MapRequest(r, seniorUserId, volunteerContact: volunteerContact,
                withholdContact: await IsContactWithheldAsync(r, cancellationToken)));
        }
        return Result<IReadOnlyList<HelpRequestDto>>.Success(dtos);
    }

    public async Task<Result<IReadOnlyList<HelpRequestDto>>> GetOpenHelpRequestsAsync(
        Guid? organizationId = null,
        Guid? viewerUserId = null,
        CancellationToken cancellationToken = default)
    {
        // BR-TENANT-03: an organizationId narrowing the list is a CLIENT-SUPPLIED
        // tenant selector, never an authorization. The guard decides membership;
        // a miss is 404 so the existence of the foreign org is never confirmed.
        // Fail closed: without the guard nobody is recognised as staff.
        if (organizationId is { } requestedOrgId)
        {
            if (_accessGuard is null)
            {
                return Error.NotFound("HelpRequest");
            }

            var staffCheck = await _accessGuard.EnsureStaffAsync(viewerUserId ?? Guid.Empty, requestedOrgId, cancellationToken);
            if (staffCheck.IsFailure)
            {
                return staffCheck.Error!;
            }
        }

        var query = _db.HelpRequests
            .AsNoTracking()
            .Where(r => (r.Status == HelpRequestStatus.Open || r.Status == HelpRequestStatus.Offered || r.Status == HelpRequestStatus.Matching) && !r.IsDeleted);

        if (organizationId.HasValue)
        {
            query = query.Where(r => r.OrganizationId == organizationId);
        }

        var requests = await query
            .OrderBy(r => r.ScheduledStartUtc)
            .ToListAsync(cancellationToken);

        var viewer = viewerUserId ?? Guid.Empty;
        if (viewerUserId is { } v && requests.Count > 0)
        {
            var owners = requests.Select(r => r.SeniorUserId).Distinct().ToList();
            var blocked = (await _safetyBoundaryReader.GetBlockedUserIdsAsync(v, owners, cancellationToken)).ToHashSet();
            var restricted = await RestrictionsAsync([v, .. owners], cancellationToken);
            requests = requests
                .Where(r => r.SeniorUserId != v)
                .Where(r => !blocked.Contains(r.SeniorUserId) && !restricted.Restricts(RestrictionKind.Discovery, r.SeniorUserId, v))
                .ToList();
        }

        var disclosures = await DecideLocationsAsync(
            requests.Select(r => r.SeniorUserId).ToList(), viewer, LocationRelationship.DiscoveryViewer, cancellationToken);
        var dtos = requests.Select(r => MapRequest(
            r,
            requestingUserId: Guid.Empty,
            isParty: false,
            discoveryDisclosure: disclosures[r.SeniorUserId])).ToList();
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

        // Phase 4: a safeguarding assignment restriction. The reason stays
        // inside safeguarding (BR-SG-05); the volunteer only learns "not possible".
        if ((await RestrictionsAsync([helpRequest.SeniorUserId, volunteerUserId], cancellationToken))
            .Restricts(RestrictionKind.Assignment, helpRequest.SeniorUserId, volunteerUserId))
        {
            return new Error("ASSIGNMENT_NOT_PERMITTED",
                "This assignment is not possible. Please contact your coordinator.", ErrorKind.Forbidden);
        }

        // P4-06: Trust Level Invariant enforcement
        var volunteerTrustLevel = await _trustLevelReader.GetEffectiveTrustLevelAsync(volunteerUserId, cancellationToken);
        if (volunteerTrustLevel < helpRequest.RequiredTrustLevel)
        {
            return Error.TrustLevelInsufficient(
                helpRequest.RequiredTrustLevel,
                volunteerTrustLevel,
                DefaultMissingVerifications);
        }

        // P4-07: Buddy System Check for Safety Level 3+ (first 3 visits)
        if (helpRequest.RequiredSafetyLevel >= 3)
        {
            var isBuddyRequired = await _safetyBoundaryReader.IsBuddyRequiredForLevel3Async(volunteerUserId, cancellationToken);
            if (isBuddyRequired)
            {
                return new Error(
                    "BUDDY_REQUIRED",
                    "Für die ersten drei Einsätze mit Sicherheitsstufe 3+ ist die Begleitung durch eine erfahrene Begleitperson (Buddy) oder eine Freigabe der Koordination erforderlich.",
                    ErrorKind.Forbidden,
                    new Dictionary<string, object>
                    {
                        ["actionKey"] = "trust.action.buddy_required"
                    });
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

        // Bridge to Phase 2 Activity (P3-17 / BR-HELP-07). Hours only exist
        // as an Activity, so completion and the activity are one unit: if
        // the activity cannot be logged we return before SaveChanges and the
        // completion is not persisted either. An early completion (before
        // the scheduled day) is logged on the day it actually happened.
        var today = RosterStatusPolicy.TodayInVienna();
        var scheduledOn = RosterStatusPolicy.TodayInVienna(helpRequest.ScheduledStartUtc);
        var activityLogResult = Activity.Log(
            organizationId: helpRequest.OrganizationId,
            volunteerUserId: volunteerUserId,
            subjectUserId: helpRequest.SeniorUserId,
            category: category,
            occurredOn: scheduledOn > today ? today : scheduledOn,
            durationMinutes: helpRequest.DurationMinutes,
            locationType: helpRequest.LocationType,
            transportMode: helpRequest.TransportMode,
            insuranceContext: helpRequest.InsuranceContext,
            source: ActivitySource.FromHelpRequest,
            loggedByUserId: volunteerUserId,
            today: today,
            notes: helpRequest.Notes);

        if (activityLogResult.IsFailure)
        {
            return activityLogResult.Error!;
        }

        _db.Activities.Add(activityLogResult.Value!);

        var history = HelpRequestStatusHistory.Create(
            helpRequestId: helpRequest.Id,
            fromStatus: fromStatus,
            toStatus: HelpRequestStatus.Completed,
            changedByUserId: volunteerUserId,
            reason: "Visit completed");

        _db.HelpRequestStatusHistories.Add(history);
        await _db.SaveChangesAsync(cancellationToken);

        // P3-20: a completed assignment nudges reliability toward reliable, exactly once.
        await _reliabilityUpdater.RecordOutcomeAsync(volunteerUserId, wasReliable: true, cancellationToken);

        return Result<HelpRequestDto>.Success(MapRequest(helpRequest, requestingUserId: volunteerUserId));
    }

    public async Task<Result<HelpRequestDto>> CancelHelpRequestAsync(
        Guid helpRequestId,
        Guid cancelledByUserId,
        CancelHelpRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var helpRequest = await FindForPartyAsync(helpRequestId, cancelledByUserId, cancellationToken);
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
        // BR-HELP-06: only the counterparty (senior, creator, assigned
        // volunteer) or a coordinator of the request's organization.
        var helpRequest = await FindForPartyAsync(helpRequestId, reportedByUserId, cancellationToken);
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

        // P3-19/P3-20: the volunteer is penalised only when THEY were the
        // no-show, i.e. someone other than the volunteer reported it. A
        // volunteer reporting the senior's absence costs them nothing.
        // ponytail: coordinator reports count as a volunteer no-show; add an
        // explicit absent-party field if coordinators must report senior absences.
        if (helpRequest.AssignedVolunteerUserId is { } volunteerUserId && volunteerUserId != reportedByUserId)
        {
            // Snapshot the prior value so an upheld dispute can restore it exactly.
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
        var helpRequest = await FindForPartyAsync(helpRequestId, disputedByUserId, cancellationToken);
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

        // BR-HELP-06: a disputed no-show does not count against reliability
        // until resolved, so the accused volunteer's score is restored now;
        // a coordinator's "upheld" resolution re-applies the penalty.
        if (helpRequest.IsReliabilityPenaltySuspended())
        {
            await _reliabilityUpdater.RestoreScoreAsync(
                helpRequest.AssignedVolunteerUserId!.Value, helpRequest.PreNoShowReliabilityScore, cancellationToken);
        }

        var history = HelpRequestStatusHistory.Create(
            helpRequestId: helpRequest.Id,
            fromStatus: fromStatus,
            toStatus: fromStatus, // dispute doesn't change the status, only the record
            changedByUserId: disputedByUserId,
            reason: $"No-show disputed (pending coordinator resolution): {request.Reason}");

        _db.HelpRequestStatusHistories.Add(history);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<HelpRequestDto>.Success(MapRequest(helpRequest, requestingUserId: disputedByUserId));
    }

    public async Task<Result<HelpRequestDto>> ResolveNoShowDisputeAsync(
        Guid helpRequestId,
        Guid resolvedByUserId,
        ResolveNoShowDisputeRequest request,
        CancellationToken cancellationToken = default)
    {
        var helpRequest = await _db.HelpRequests
            .FirstOrDefaultAsync(r => r.Id == helpRequestId && !r.IsDeleted, cancellationToken);

        // Coordinator-only: outside the request's organization it does not exist (404);
        // a member without a staff role gets the guard's 403.
        if (helpRequest?.OrganizationId is not { } organizationId || _accessGuard is null)
        {
            return Error.NotFound("HelpRequest");
        }

        var staff = await _accessGuard.EnsureStaffAsync(resolvedByUserId, organizationId, cancellationToken);
        if (staff.IsFailure)
        {
            return staff.Error!.Kind == ErrorKind.NotFound ? Error.NotFound("HelpRequest") : staff.Error!;
        }

        var resolveResult = helpRequest.ResolveNoShowDispute(resolvedByUserId, request.Upheld);
        if (resolveResult.IsFailure)
        {
            return resolveResult.Error!;
        }

        // Upheld: the no-show stands, so the suspended penalty is applied again.
        // Rejected: the restored score simply stays.
        if (request.Upheld && helpRequest.IsReliabilityPenaltySuspended())
        {
            await _reliabilityUpdater.RecordOutcomeAsync(
                helpRequest.AssignedVolunteerUserId!.Value, wasReliable: false, cancellationToken);
        }

        var outcome = request.Upheld ? "upheld" : "rejected";
        var note = string.IsNullOrWhiteSpace(request.Note) ? string.Empty : $": {request.Note.Trim()}";
        _db.HelpRequestStatusHistories.Add(HelpRequestStatusHistory.Create(
            helpRequestId: helpRequest.Id,
            fromStatus: helpRequest.Status,
            toStatus: helpRequest.Status,
            changedByUserId: resolvedByUserId,
            reason: $"No-show dispute resolved ({outcome}){note}"));

        await _db.SaveChangesAsync(cancellationToken);

        return Result<HelpRequestDto>.Success(MapRequest(helpRequest, requestingUserId: resolvedByUserId));
    }

    public async Task<Result<IReadOnlyList<HelpRequestStatusHistoryDto>>> GetStatusHistoryAsync(
        Guid helpRequestId,
        Guid requestingUserId,
        CancellationToken cancellationToken = default)
    {
        if (await FindForPartyAsync(helpRequestId, requestingUserId, cancellationToken) is null)
        {
            return Error.NotFound("HelpRequest");
        }

        var history = await _db.HelpRequestStatusHistories
            .AsNoTracking()
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

    /// <summary>Loads a request only if the caller is a party to it; otherwise behaves as not found.</summary>
    private async Task<HelpRequest?> FindForPartyAsync(Guid helpRequestId, Guid userId, CancellationToken cancellationToken)
    {
        var helpRequest = await _db.HelpRequests
            .FirstOrDefaultAsync(r => r.Id == helpRequestId && !r.IsDeleted, cancellationToken);

        return helpRequest is not null && await IsPartyAsync(helpRequest, userId, cancellationToken)
            ? helpRequest
            : null;
    }

    /// <summary>Senior, creator, assigned volunteer, or an active coordinator/admin of the request's organization.</summary>
    private async Task<bool> IsPartyAsync(HelpRequest helpRequest, Guid userId, CancellationToken cancellationToken)
    {
        if (helpRequest.IsDirectParty(userId))
        {
            return true;
        }

        if (helpRequest.OrganizationId is not { } organizationId || _accessGuard is null)
        {
            return false;
        }

        return (await _accessGuard.EnsureStaffAsync(userId, organizationId, cancellationToken)).IsSuccess;
    }

    /// <summary>
    /// GET /help-requests/{id}/location: the one place a request's exact
    /// coordinates can come from, decided by <see cref="ILocationVisibilityService"/>.
    /// Staff get the area only; the assigned volunteer gets exact coordinates
    /// only with the senior's precise consent.
    /// </summary>
    public async Task<Result<HelpRequestLocationDto>> GetHelpRequestLocationAsync(
        Guid helpRequestId,
        Guid requestingUserId,
        CancellationToken cancellationToken = default)
    {
        var helpRequest = await _db.HelpRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == helpRequestId && !r.IsDeleted, cancellationToken);
        if (helpRequest is null)
        {
            return Error.NotFound("HelpRequest");
        }

        var relationship = DirectRelationship(helpRequest, requestingUserId)
            ?? (await IsPartyAsync(helpRequest, requestingUserId, cancellationToken)
                ? LocationRelationship.OrganizationStaff
                : IsOpenForOffers(helpRequest.Status) ? LocationRelationship.DiscoveryViewer : LocationRelationship.None);
        if (relationship == LocationRelationship.None)
        {
            return Error.NotFound("HelpRequest");
        }

        var disclosure = await DecideLocationAsync(helpRequest.SeniorUserId, requestingUserId, relationship, cancellationToken);
        var (latitude, longitude) = LocationVisibilityPolicy.Shape(disclosure, helpRequest.Latitude, helpRequest.Longitude);
        return Result<HelpRequestLocationDto>.Success(new HelpRequestLocationDto(
            HelpRequestId: helpRequest.Id,
            Disclosure: disclosure.ToCode(),
            Latitude: latitude,
            Longitude: longitude,
            PostalCode: disclosure.ShowsArea() ? helpRequest.LocationPostalCode : null,
            City: disclosure.ShowsArea() ? helpRequest.LocationCity : null));
    }

    private static LocationRelationship? DirectRelationship(HelpRequest r, Guid userId)
    {
        if (userId == r.SeniorUserId) return LocationRelationship.Self;
        if (userId == r.CreatedByUserId) return LocationRelationship.RequestCreator;
        if (IsLiveAssignee(r, userId)) return LocationRelationship.AssignedVolunteer;
        return null;
    }

    private async Task<LocationDisclosure> DecideLocationAsync(
        Guid ownerUserId, Guid viewerUserId, LocationRelationship relationship, CancellationToken ct) =>
        _locationVisibility is null
            ? LocationVisibilityPolicy.Decide(relationship, LocationSharingLevel.None)
            : await _locationVisibility.DecideAsync(ownerUserId, viewerUserId, relationship, ct);

    private async Task<IReadOnlyDictionary<Guid, LocationDisclosure>> DecideLocationsAsync(
        IReadOnlyCollection<Guid> ownerUserIds, Guid viewerUserId, LocationRelationship relationship, CancellationToken ct) =>
        _locationVisibility is null
            ? ownerUserIds.Distinct().ToDictionary(id => id, _ => LocationVisibilityPolicy.Decide(relationship, LocationSharingLevel.None))
            : await _locationVisibility.DecideManyAsync(ownerUserIds, viewerUserId, relationship, ct);

    private static bool IsOpenForOffers(HelpRequestStatus status) =>
        status is HelpRequestStatus.Open or HelpRequestStatus.Matching or HelpRequestStatus.Offered;

    // --- ADR-026 Option C (approved 2026-09-30; legal validation pending) ----

    /// <summary>
    /// The senior (or an authorized representative per BR-HELP-04) grants the
    /// visit address to the CURRENT assignee of their live assignment. A
    /// coordinator may act on behalf of an app-less senior — recorded with the
    /// audited coordinator path (decision baseline §8.5). Idempotent: an
    /// existing live grant for the same pair returns success.
    /// </summary>
    public async Task<Result> GrantVisitAddressAsync(
        Guid helpRequestId,
        Guid grantedByUserId,
        GrantVisitAddressRequest request,
        CancellationToken cancellationToken = default)
    {
        var r = await _db.HelpRequests.FirstOrDefaultAsync(x => x.Id == helpRequestId && !x.IsDeleted, cancellationToken);
        if (r is null)
        {
            return Error.NotFound("HelpRequest");
        }

        if (r.AssignedVolunteerUserId is not { } volunteerId
            || r.Status is not (HelpRequestStatus.Assigned or HelpRequestStatus.InProgress))
        {
            return Error.Validation("A visit address can only be granted for a live assignment.");
        }

        VisitAddressGrantIssuer role;
        if (grantedByUserId == r.SeniorUserId || grantedByUserId == r.CreatedByUserId)
        {
            role = VisitAddressGrantIssuer.Senior;
        }
        else if (request.OnBehalfOfSeniorByCoordinatorId is { } coordinatorId && coordinatorId == grantedByUserId)
        {
            // Coordinator-on-behalf path: only for org requests, with a reason, audited.
            if (r.OrganizationId is null)
            {
                return Error.Forbidden("A coordinator may only grant on behalf for an organization request.");
            }

            if (string.IsNullOrWhiteSpace(request.CoordinatorReason))
            {
                return Error.Validation("A reason is required for the coordinator on-behalf path.");
            }

            role = VisitAddressGrantIssuer.CoordinatorOnBehalf;
        }
        else if (_familyPermissionReader is { } family
                 && await family.HasPermissionAsync(grantedByUserId, r.SeniorUserId, "CreateHelpRequestsOnBehalf", cancellationToken))
        {
            role = VisitAddressGrantIssuer.AuthorizedRepresentative;
        }
        else
        {
            return Error.Forbidden("Only the senior, an authorized representative, or the organization coordinator may grant the visit address.");
        }

        var existing = await _db.VisitAddressGrants.FirstOrDefaultAsync(g =>
            g.HelpRequestId == helpRequestId
            && g.VolunteerUserId == volunteerId
            && g.RevokedAtUtc == null, cancellationToken); // mapped column (IsActive is not translatable)
        if (existing is not null)
        {
            return Result.Success(); // idempotent
        }

        var grant = VisitAddressGrant.Create(helpRequestId, volunteerId, grantedByUserId, role);
        if (grant.IsFailure)
        {
            return grant.Error!;
        }

        _db.VisitAddressGrants.Add(grant.Value!);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// The grantor (senior / representative / granting coordinator) revokes the
    /// live grant. Possible until check-in (after that the visit happened).
    /// Idempotent.
    /// </summary>
    public async Task<Result> RevokeVisitAddressAsync(
        Guid helpRequestId,
        Guid revokedByUserId,
        RevokeVisitAddressRequest request,
        CancellationToken cancellationToken = default)
    {
        var grants = await _db.VisitAddressGrants
            .Where(g => g.HelpRequestId == helpRequestId && g.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var grant in grants)
        {
            var mayRevoke = grant.GrantedByUserId == revokedByUserId;
            if (!mayRevoke)
            {
                var r = await _db.HelpRequests.AsNoTracking().FirstAsync(x => x.Id == helpRequestId, cancellationToken);
                mayRevoke = revokedByUserId == r.SeniorUserId
                            || revokedByUserId == r.CreatedByUserId;
            }

            if (mayRevoke)
            {
                grant.Revoke(revokedByUserId, request.Reason);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private HelpRequestDto MapRequest(
        HelpRequest r,
        Guid requestingUserId,
        UserContact? seniorContact = null,
        UserContact? volunteerContact = null,
        bool isParty = true,
        LocationDisclosure discoveryDisclosure = LocationDisclosure.DistanceOnly,
        bool withholdContact = false)
    {
        // The generic DTO never carries exact coordinates, whoever asks
        // (staff included): at most the ~1 km grid cell. Exact coordinates
        // exist only behind GetHelpRequestLocationAsync. For parties, consent
        // could only lift the assigned volunteer above Approximate, which
        // this cap removes anyway, so it is not read here.
        var relationship = DirectRelationship(r, requestingUserId)
            ?? (isParty ? LocationRelationship.OrganizationStaff : LocationRelationship.DiscoveryViewer);
        var disclosure = (relationship == LocationRelationship.DiscoveryViewer
                ? discoveryDisclosure
                : LocationVisibilityPolicy.Decide(relationship, LocationSharingLevel.None))
            .AtMost(LocationDisclosure.Approximate);
        var (latitude, longitude) = LocationVisibilityPolicy.Shape(disclosure, r.Latitude, r.Longitude);

        // Contact details mask rule (BR-COMM-04): Address and precise contact info are only revealed
        // once assigned to the volunteer, or for the senior/creator themselves.
        // ADR-026: the volunteer only while the assignment is live, and never
        // while senior and volunteer are blocked or contact-restricted.
        // ADR-026 Option C (approved 2026-09-30): when the feature gate is on,
        // the live assignee additionally needs a LIVE GRANT. Blocks and
        // CONTACT/LOCATION restrictions still withhold first (can only subtract).
        var needsGrant = _visitAddressGrantRequired
                         && IsLiveAssignee(r, requestingUserId);
        var hasGrant = !needsGrant || _db.VisitAddressGrants.AsNoTracking().Any(g =>
            g.HelpRequestId == r.Id
            && g.VolunteerUserId == requestingUserId
            && g.RevokedAtUtc == null); // mapped column (IsActive is not translatable)
        var isAuthorizedToSeeAddress = requestingUserId == r.SeniorUserId
            || requestingUserId == r.CreatedByUserId
            || (IsLiveAssignee(r, requestingUserId) && !withholdContact && hasGrant);

        // ADR-026 (legal validation pending): parties of a live assignment see
        // whether a live grant exists, so the senior's app can offer the grant /
        // revoke action at the right moment. Content-free consent metadata —
        // never the address itself.
        var grantActive = IsLiveAssignee(r, requestingUserId) || requestingUserId == r.SeniorUserId
            ? _db.VisitAddressGrants.AsNoTracking().Any(g =>
                g.HelpRequestId == r.Id
                && g.RevokedAtUtc == null
                && (r.AssignedVolunteerUserId == null || g.VolunteerUserId == r.AssignedVolunteerUserId))
            : false;

        // Symmetric to seniorContact: once assigned, the senior/creator sees
        // WHO is coming (same BR-COMM-04 gate, the other direction).
        var isSeniorOrCreator = (requestingUserId == r.SeniorUserId || requestingUserId == r.CreatedByUserId) && !withholdContact;

        // Non-parties (open list, browsing volunteers) never get identities
        // or free-text notes.
        return new HelpRequestDto(
            Id: r.Id,
            OrganizationId: r.OrganizationId,
            BranchId: r.BranchId,
            SeniorUserId: isParty ? r.SeniorUserId : Guid.Empty,
            CreatedByUserId: isParty ? r.CreatedByUserId : Guid.Empty,
            CategoryId: r.CategoryId,
            RequiredSafetyLevel: r.RequiredSafetyLevel,
            RequiredTrustLevel: r.RequiredTrustLevel,
            ScheduledStartUtc: r.ScheduledStartUtc,
            ScheduledEndUtc: r.ScheduledEndUtc,
            DurationMinutes: r.DurationMinutes,
            LocationType: r.LocationType,
            LocationAddress: isAuthorizedToSeeAddress ? r.LocationAddress : null,
            LocationPostalCode: disclosure.ShowsArea() ? r.LocationPostalCode : null,
            LocationCity: disclosure.ShowsArea() ? r.LocationCity : null,
            Latitude: latitude,
            Longitude: longitude,
            Notes: isParty ? r.Notes : null,
            TransportMode: r.TransportMode,
            InsuranceContext: r.InsuranceContext,
            Status: r.Status,
            AssignedVolunteerUserId: isParty ? r.AssignedVolunteerUserId : null,
            AssignedAtUtc: r.AssignedAtUtc,
            CheckedInAtUtc: r.CheckedInAtUtc,
            CompletedAtUtc: r.CompletedAtUtc,
            CancellationReason: isParty ? r.CancellationReason : null,
            RowVersion: r.RowVersion,
            SeniorDisplayName: isAuthorizedToSeeAddress ? seniorContact?.DisplayName : null,
            SeniorPhone: isAuthorizedToSeeAddress ? seniorContact?.Phone : null,
            VolunteerDisplayName: isSeniorOrCreator ? volunteerContact?.DisplayName : null,
            VolunteerPhone: isSeniorOrCreator ? volunteerContact?.Phone : null,
            VisitAddressGrantActive: grantActive);
    }

    /// <summary>P6-05: fail closed. Without a real reader nobody may act on a senior's behalf.</summary>
    private sealed class NoopFamilyPermissionReader : IFamilyPermissionReader
    {
        public Task<bool> HasPermissionAsync(Guid caregiverUserId, Guid seniorUserId, string permissionType, CancellationToken ct = default)
            => Task.FromResult(false);
    }
}
