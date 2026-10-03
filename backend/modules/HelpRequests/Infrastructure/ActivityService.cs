using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.HelpRequests.Application;
using SeniorConnect.Modules.HelpRequests.Domain;
using SeniorConnect.Modules.Organizations.Contracts;

namespace SeniorConnect.Modules.HelpRequests.Infrastructure;

public sealed class ActivityService : IActivityService
{
    private readonly IHelpRequestsDbContext _db;
    private readonly IMemoryCache _cache;

    // Fail closed: without these, org-scoped logging and staff review are refused.
    private readonly IOrganizationAccessGuard? _accessGuard;
    private readonly IOrganizationDirectory? _directory;

    private static readonly MemoryCacheEntryOptions CategoryCacheOptions = new MemoryCacheEntryOptions()
        .SetAbsoluteExpiration(TimeSpan.FromMinutes(30))
        .SetSlidingExpiration(TimeSpan.FromMinutes(10));

    public ActivityService(
        IHelpRequestsDbContext db,
        IMemoryCache cache,
        IOrganizationAccessGuard? accessGuard = null,
        IOrganizationDirectory? directory = null)
    {
        _db = db;
        _cache = cache;
        _accessGuard = accessGuard;
        _directory = directory;
    }

    public async Task<Result<ActivityDto>> LogActivityAsync(
        Guid volunteerUserId,
        LogActivityRequest request,
        ActivitySource source = ActivitySource.SelfLogged,
        Guid? loggedByUserId = null,
        CancellationToken cancellationToken = default)
    {
        // P2-15: a coordinator's bulk entry records the staff member as logger.
        var loggerUserId = loggedByUserId ?? volunteerUserId;

        var scopeError = await AuthorizeLoggingAsync(volunteerUserId, loggerUserId, request.OrganizationId, cancellationToken);
        if (scopeError is not null)
        {
            return scopeError;
        }

        var category = await _db.ActivityCategories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.IsActive, cancellationToken);

        if (category is null)
        {
            return Error.NotFound("ActivityCategory");
        }

        // P2-14: "today" is the organization's (Austrian) calendar day, not UTC's.
        var today = RosterStatusPolicy.TodayInVienna();

        var logResult = Activity.Log(
            organizationId: request.OrganizationId,
            volunteerUserId: volunteerUserId,
            subjectUserId: request.SubjectUserId,
            category: category,
            occurredOn: request.OccurredOn,
            durationMinutes: request.DurationMinutes,
            locationType: request.LocationType,
            transportMode: request.TransportMode,
            insuranceContext: request.InsuranceContext,
            source: source,
            loggedByUserId: loggerUserId,
            today: today,
            notes: request.Notes);

        if (logResult.IsFailure)
        {
            return logResult.Error!;
        }

        var activity = logResult.Value!;
        _db.Activities.Add(activity);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<ActivityDto>.Success(MapActivity(activity));
    }

    public async Task<Result<ActivityDto>> ConfirmActivityAsync(
        Guid activityId,
        Guid confirmingUserId,
        CancellationToken cancellationToken = default)
    {
        // P2-12: tracked load — an AsNoTracking entity is never saved.
        var activity = await _db.Activities
            .FirstOrDefaultAsync(a => a.Id == activityId && !a.IsDeleted, cancellationToken);

        if (activity is null)
        {
            return Error.NotFound("Activity");
        }

        var reviewError = await AuthorizeReviewerAsync(activity, confirmingUserId, cancellationToken);
        if (reviewError is not null)
        {
            return reviewError;
        }

        var confirmResult = activity.Confirm(confirmingUserId);
        if (confirmResult.IsFailure)
        {
            return confirmResult.Error!;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result<ActivityDto>.Success(MapActivity(activity));
    }

    public async Task<Result<ActivityDto>> DisputeActivityAsync(
        Guid activityId,
        Guid disputingUserId,
        DisputeActivityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Error.Validation("Dispute reason is required.");
        }

        var activity = await _db.Activities
            .FirstOrDefaultAsync(a => a.Id == activityId && !a.IsDeleted, cancellationToken);

        if (activity is null)
        {
            return Error.NotFound("Activity");
        }

        var reviewError = await AuthorizeReviewerAsync(activity, disputingUserId, cancellationToken);
        if (reviewError is not null)
        {
            return reviewError;
        }

        var disputeResult = activity.Dispute(disputingUserId, request.Reason);
        if (disputeResult.IsFailure)
        {
            return disputeResult.Error!;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result<ActivityDto>.Success(MapActivity(activity));
    }

    public async Task<Result<ActivityDto>> GetActivityByIdAsync(
        Guid activityId,
        Guid requestingUserId,
        CancellationToken cancellationToken = default)
    {
        var activity = await _db.Activities
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == activityId && !a.IsDeleted, cancellationToken);

        if (activity is null || !await CanReadAsync(activity, requestingUserId, cancellationToken))
        {
            return Error.NotFound("Activity");
        }

        return Result<ActivityDto>.Success(MapActivity(activity));
    }

    public async Task<Result<IReadOnlyList<ActivityDto>>> GetVolunteerActivitiesAsync(
        Guid volunteerUserId,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Activities
            .AsNoTracking()
            .Where(a => a.VolunteerUserId == volunteerUserId && !a.IsDeleted);

        if (from.HasValue) query = query.Where(a => a.OccurredOn >= from.Value);
        if (to.HasValue) query = query.Where(a => a.OccurredOn <= to.Value);

        var activities = await query
            .OrderByDescending(a => a.OccurredOn)
            .ThenByDescending(a => a.LoggedAtUtc)
            .ToListAsync(cancellationToken);

        var dtos = activities.Select(MapActivity).ToList();
        return Result<IReadOnlyList<ActivityDto>>.Success(dtos);
    }

    public async Task<Result<IReadOnlyList<ActivityDto>>> GetOrganizationActivitiesAsync(
        Guid organizationId,
        Guid requestingUserId,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        if (_accessGuard is null)
        {
            return Error.NotFound("Organization");
        }

        var staff = await _accessGuard.EnsureStaffAsync(requestingUserId, organizationId, cancellationToken);
        if (staff.IsFailure)
        {
            return staff.Error!;
        }

        var query = _db.Activities
            .AsNoTracking()
            .Where(a => a.OrganizationId == organizationId && !a.IsDeleted);

        if (from.HasValue) query = query.Where(a => a.OccurredOn >= from.Value);
        if (to.HasValue) query = query.Where(a => a.OccurredOn <= to.Value);

        var activities = await query
            .OrderByDescending(a => a.OccurredOn)
            .ThenByDescending(a => a.LoggedAtUtc)
            .ToListAsync(cancellationToken);

        var dtos = activities.Select(MapActivity).ToList();
        return Result<IReadOnlyList<ActivityDto>>.Success(dtos);
    }

    public async Task<Result<IReadOnlyList<ActivityCategoryDto>>> GetCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        const string cacheKey = "ref_activity_categories";

        if (!_cache.TryGetValue(cacheKey, out IReadOnlyList<ActivityCategoryDto>? dtos) || dtos is null)
        {
            var categories = await _db.ActivityCategories
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.Code)
                .ToListAsync(cancellationToken);

            dtos = categories.Select(c => new ActivityCategoryDto(
                c.Id,
                c.Code,
                c.NameKey,
                c.DefaultSafetyLevel,
                c.IsBlocked,
                c.ReferralGroup,
                c.IsActive)).ToList();

            _cache.Set(cacheKey, dtos, CategoryCacheOptions);
        }

        return Result<IReadOnlyList<ActivityCategoryDto>>.Success(dtos);
    }

    public async Task<Result<IReadOnlyList<ReferralProviderDto>>> GetReferralProvidersAsync(
        string? referralGroup = null,
        string? regionCode = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.ReferralDirectories
            .AsNoTracking()
            .Where(r => r.IsActive);

        if (!string.IsNullOrWhiteSpace(referralGroup))
        {
            query = query.Where(r => r.ReferralGroup == referralGroup);
        }

        if (!string.IsNullOrWhiteSpace(regionCode))
        {
            query = query.Where(r => r.RegionCode == regionCode);
        }

        var providers = await query
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        var dtos = providers.Select(p => new ReferralProviderDto(
            p.Id,
            p.ReferralGroup,
            p.RegionCode,
            p.Name,
            p.Phone,
            p.Website,
            p.Address,
            p.NoteKey)).ToList();

        return Result<IReadOnlyList<ReferralProviderDto>>.Success(dtos);
    }

    private async Task<bool> CanReadAsync(Activity activity, Guid userId, CancellationToken cancellationToken)
    {
        if (userId == activity.VolunteerUserId || userId == activity.SubjectUserId || userId == activity.LoggedByUserId)
        {
            return true;
        }

        return activity.OrganizationId is { } organizationId
            && _accessGuard is not null
            && (await _accessGuard.EnsureStaffAsync(userId, organizationId, cancellationToken)).IsSuccess;
    }

    /// <summary>
    /// BR-HELP-07: hours are confirmed (or disputed) by the counterpart — the
    /// activity's subject — or by staff of the activity's organization, never
    /// by the volunteer. Anyone else: the activity does not exist for them.
    /// </summary>
    private async Task<Error?> AuthorizeReviewerAsync(Activity activity, Guid userId, CancellationToken cancellationToken)
    {
        if (userId == activity.VolunteerUserId)
        {
            return new Error("SELF_CONFIRMATION_FORBIDDEN", "A volunteer cannot confirm or dispute their own hours.", ErrorKind.Forbidden);
        }

        if (activity.SubjectUserId == userId)
        {
            return null;
        }

        if (activity.OrganizationId is not { } organizationId || _accessGuard is null)
        {
            return Error.NotFound("Activity");
        }

        var staff = await _accessGuard.EnsureStaffAsync(userId, organizationId, cancellationToken);
        if (staff.IsSuccess)
        {
            return null;
        }

        return staff.Error!.Kind == ErrorKind.NotFound ? Error.NotFound("Activity") : staff.Error;
    }

    /// <summary>
    /// P2-10: org-scoped hours only by the org's own people. Self-logging needs
    /// an active membership (volunteer or staff); logging for someone else
    /// needs a staff role and the volunteer on the org's active roster.
    /// </summary>
    private async Task<Error?> AuthorizeLoggingAsync(Guid volunteerUserId, Guid loggerUserId, Guid? organizationId, CancellationToken cancellationToken)
    {
        if (organizationId is not { } orgId)
        {
            return loggerUserId == volunteerUserId
                ? null
                : Error.Validation("Logging hours for someone else requires an organization.");
        }

        if (_accessGuard is null || _directory is null)
        {
            return Error.NotFound("Organization");
        }

        var isRosterVolunteer = (await _directory.GetActiveVolunteerUserIdsAsync(orgId, cancellationToken)).Contains(volunteerUserId);

        if (loggerUserId == volunteerUserId)
        {
            return isRosterVolunteer || (await _accessGuard.EnsureStaffAsync(loggerUserId, orgId, cancellationToken)).IsSuccess
                ? null
                : Error.NotFound("Organization");
        }

        var staff = await _accessGuard.EnsureStaffAsync(loggerUserId, orgId, cancellationToken);
        if (staff.IsFailure)
        {
            return staff.Error;
        }

        return isRosterVolunteer
            ? null
            : new Error("VOLUNTEER_NOT_IN_ORGANIZATION", "The volunteer is not an active member of this organization.", ErrorKind.Validation);
    }

    private static ActivityDto MapActivity(Activity a) => new(
        Id: a.Id,
        OrganizationId: a.OrganizationId,
        BranchId: a.BranchId,
        VolunteerUserId: a.VolunteerUserId,
        SubjectUserId: a.SubjectUserId,
        CategoryId: a.CategoryId,
        OccurredOn: a.OccurredOn,
        DurationMinutes: a.DurationMinutes,
        LocationType: a.LocationType,
        Notes: a.Notes,
        InsuranceContext: a.InsuranceContext,
        TransportMode: a.TransportMode,
        Source: a.Source,
        LoggedByUserId: a.LoggedByUserId,
        LoggedAtUtc: a.LoggedAtUtc,
        ConfirmedByUserId: a.ConfirmedByUserId,
        ConfirmedAtUtc: a.ConfirmedAtUtc,
        Status: a.Status);
}

