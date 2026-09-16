using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.TrustSafety.Application;
using SeniorConnect.Modules.TrustSafety.Domain;

namespace SeniorConnect.Modules.TrustSafety.Infrastructure;

public sealed class OnboardingService : IOnboardingService
{
    private readonly ITrustSafetyDbContext _db;

    public OnboardingService(ITrustSafetyDbContext db)
    {
        _db = db;
    }

    public async Task<Result<VolunteerApplicationDto>> ApplyAsync(
        Guid userId,
        ApplyVolunteerRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.VolunteerApplications
            .FirstOrDefaultAsync(a => a.OrganizationId == request.OrganizationId && a.UserId == userId && a.Status == ApplicationStatus.Open, cancellationToken);

        if (existing is not null)
        {
            return new Error("APPLICATION_EXISTS", "You already have an open application for this organization.", ErrorKind.Conflict);
        }

        var application = VolunteerApplication.Apply(
            organizationId: request.OrganizationId,
            userId: userId,
            motivation: request.Motivation);

        _db.VolunteerApplications.Add(application);

        // Seed default 5-step pipeline per Phase 2 spec (P2-23)
        var steps = new List<VolunteerApplicationStep>
        {
            VolunteerApplicationStep.Create(application.Id, ApplicationStepType.Interview, 1, slaDays: 7),
            VolunteerApplicationStep.Create(application.Id, ApplicationStepType.BackgroundCheck, 2, slaDays: 14),
            VolunteerApplicationStep.Create(application.Id, ApplicationStepType.ConfidentialityAgreement, 3, slaDays: 7),
            VolunteerApplicationStep.Create(application.Id, ApplicationStepType.Briefing, 4, slaDays: 14),
            VolunteerApplicationStep.Create(application.Id, ApplicationStepType.Approval, 5, slaDays: 7)
        };

        // Open the first step automatically
        steps[0].Start();

        _db.VolunteerApplicationSteps.AddRange(steps);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<VolunteerApplicationDto>.Success(MapApplication(application, steps));
    }

    public async Task<Result<VolunteerApplicationDto>> GetApplicationByIdAsync(
        Guid applicationId,
        CancellationToken cancellationToken = default)
    {
        var application = await _db.VolunteerApplications
            .FirstOrDefaultAsync(a => a.Id == applicationId, cancellationToken);

        if (application is null)
        {
            return Error.NotFound("VolunteerApplication");
        }

        var steps = await _db.VolunteerApplicationSteps
            .Where(s => s.ApplicationId == applicationId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(cancellationToken);

        return Result<VolunteerApplicationDto>.Success(MapApplication(application, steps));
    }

    public async Task<Result<IReadOnlyList<VolunteerApplicationDto>>> GetOrganizationApplicationsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var applications = await _db.VolunteerApplications
            .Where(a => a.OrganizationId == organizationId)
            .OrderByDescending(a => a.AppliedAtUtc)
            .ToListAsync(cancellationToken);

        var appIds = applications.Select(a => a.Id).ToList();
        var allSteps = await _db.VolunteerApplicationSteps
            .Where(s => appIds.Contains(s.ApplicationId))
            .OrderBy(s => s.SortOrder)
            .ToListAsync(cancellationToken);

        var dtos = applications.Select(a => MapApplication(a, allSteps.Where(s => s.ApplicationId == a.Id).ToList())).ToList();
        return Result<IReadOnlyList<VolunteerApplicationDto>>.Success(dtos);
    }

    public async Task<Result<VolunteerApplicationDto>> UpdateStepAsync(
        Guid applicationId,
        Guid stepId,
        UpdateStepRequest request,
        Guid staffUserId,
        CancellationToken cancellationToken = default)
    {
        var application = await _db.VolunteerApplications
            .FirstOrDefaultAsync(a => a.Id == applicationId, cancellationToken);

        if (application is null)
        {
            return Error.NotFound("VolunteerApplication");
        }

        var step = await _db.VolunteerApplicationSteps
            .FirstOrDefaultAsync(s => s.Id == stepId && s.ApplicationId == applicationId, cancellationToken);

        if (step is null)
        {
            return Error.NotFound("ApplicationStep");
        }

        switch (request.Status)
        {
            case StepStatus.InProgress:
                step.Start();
                break;
            case StepStatus.Completed:
                step.Complete(staffUserId, request.Note);
                break;
            case StepStatus.Blocked:
                step.Block(request.Note);
                break;
            case StepStatus.Skipped:
                step.Skip(request.Note);
                break;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var steps = await _db.VolunteerApplicationSteps
            .Where(s => s.ApplicationId == applicationId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(cancellationToken);

        return Result<VolunteerApplicationDto>.Success(MapApplication(application, steps));
    }

    public async Task<Result<VolunteerApplicationDto>> DecideApplicationAsync(
        Guid applicationId,
        DecideApplicationRequest request,
        Guid staffUserId,
        CancellationToken cancellationToken = default)
    {
        var application = await _db.VolunteerApplications
            .FirstOrDefaultAsync(a => a.Id == applicationId, cancellationToken);

        if (application is null)
        {
            return Error.NotFound("VolunteerApplication");
        }

        if (request.Approved)
        {
            application.Approve(staffUserId);
        }
        else
        {
            application.Decline(staffUserId, request.Reason);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var steps = await _db.VolunteerApplicationSteps
            .Where(s => s.ApplicationId == applicationId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(cancellationToken);

        return Result<VolunteerApplicationDto>.Success(MapApplication(application, steps));
    }

    private static VolunteerApplicationDto MapApplication(VolunteerApplication a, IReadOnlyList<VolunteerApplicationStep> steps) => new(
        Id: a.Id,
        OrganizationId: a.OrganizationId,
        UserId: a.UserId,
        Status: a.Status,
        Motivation: a.Motivation,
        AppliedAtUtc: a.AppliedAtUtc,
        DecidedAtUtc: a.DecidedAtUtc,
        DecidedByUserId: a.DecidedByUserId,
        DeclineReason: a.DeclineReason,
        Steps: steps.Select(s => new ApplicationStepDto(
            s.Id,
            s.ApplicationId,
            s.Step,
            s.SortOrder,
            s.Status,
            s.SlaDays,
            s.OpenedAtUtc,
            s.CompletedAtUtc,
            s.CompletedByUserId,
            s.Note,
            s.DaysOpen,
            s.IsOverdue)).ToList());
}
