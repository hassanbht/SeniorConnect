using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.HelpRequests.Application;
using SeniorConnect.Modules.HelpRequests.Domain;

namespace SeniorConnect.Modules.HelpRequests.Infrastructure;

public sealed class ActivityService : IActivityService
{
    private readonly IHelpRequestsDbContext _db;

    public ActivityService(IHelpRequestsDbContext db)
    {
        _db = db;
    }

    public async Task<Result<ActivityDto>> LogActivityAsync(
        Guid volunteerUserId,
        LogActivityRequest request,
        ActivitySource source = ActivitySource.SelfLogged,
        CancellationToken cancellationToken = default)
    {
        var category = await _db.ActivityCategories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.IsActive, cancellationToken);

        if (category is null)
        {
            return Error.NotFound("ActivityCategory");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

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
            loggedByUserId: volunteerUserId,
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
        var activity = await _db.Activities
            .FirstOrDefaultAsync(a => a.Id == activityId && !a.IsDeleted, cancellationToken);

        if (activity is null)
        {
            return Error.NotFound("Activity");
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
        CancellationToken cancellationToken = default)
    {
        var activity = await _db.Activities
            .FirstOrDefaultAsync(a => a.Id == activityId && !a.IsDeleted, cancellationToken);

        if (activity is null)
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
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Activities
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
        var categories = await _db.ActivityCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Code)
            .ToListAsync(cancellationToken);

        var dtos = categories.Select(c => new ActivityCategoryDto(
            c.Id,
            c.Code,
            c.NameKey,
            c.DefaultSafetyLevel,
            c.IsBlocked,
            c.ReferralGroup,
            c.IsActive)).ToList();

        return Result<IReadOnlyList<ActivityCategoryDto>>.Success(dtos);
    }

    public async Task<Result<IReadOnlyList<ReferralProviderDto>>> GetReferralProvidersAsync(
        string? referralGroup = null,
        string? regionCode = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.ReferralDirectories
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
