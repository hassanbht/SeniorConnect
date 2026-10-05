using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Organizations.Contracts;
using SeniorConnect.Modules.TrustSafety.Application;
using SeniorConnect.Modules.TrustSafety.Contracts;
using SeniorConnect.Modules.TrustSafety.Domain;

namespace SeniorConnect.Modules.TrustSafety.Infrastructure;

public sealed class SafeguardingService : ISafeguardingService, ISafeguardingCaseRaiser
{
    private readonly ISafeguardingDbContext _db;
    private readonly IOrganizationCoordinatorReader _orgReader;

    public SafeguardingService(ISafeguardingDbContext db, IOrganizationCoordinatorReader orgReader)
    {
        _db = db;
        _orgReader = orgReader;
    }

    /// <summary>
    /// B-2 boundary adapter (ISafeguardingCaseRaiser): the Community module
    /// converts an escalated unsafe-behavior report through this port — a
    /// neutral string severity crosses the boundary, never the Domain enum.
    /// Unknown severities fail closed with a validation error.
    /// </summary>
    public async Task<Result<Guid>> RaiseCaseAsync(
        Guid reporterUserId,
        Guid subjectUserId,
        string summary,
        string severity,
        string category,
        Guid? organizationId,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<SafeguardingSeverity>(severity, ignoreCase: false, out var parsed)
            || !Enum.IsDefined(parsed))
        {
            return Error.Validation(
                "Severity must be one of: Low, Medium, High, Critical.");
        }

        var raised = await RaiseConcernAsync(reporterUserId,
            new RaiseConcernRequest(subjectUserId, summary, parsed, category, organizationId), ct);

        return raised.IsSuccess
            ? Result<Guid>.Success(raised.Value!.Id)
            : Result<Guid>.Failure(raised.Error!);
    }

    public async Task<Result<SafeguardingCaseDto>> RaiseConcernAsync(
        Guid reporterUserId,
        RaiseConcernRequest request,
        CancellationToken cancellationToken = default)
    {
        var createResult = SafeguardingCase.Raise(
            reporterUserId: reporterUserId,
            subjectUserId: request.SubjectUserId,
            summary: request.Summary,
            severity: request.Severity,
            category: request.Category,
            organizationId: request.OrganizationId);

        if (createResult.IsFailure)
        {
            return createResult.Error!;
        }

        var sCase = createResult.Value!;
        _db.SafeguardingCases.Add(sCase);

        var log = SafeguardingAccessLog.Create(sCase.Id, reporterUserId, "RaiseConcern");
        _db.SafeguardingAccessLogs.Add(log);

        await _db.SaveChangesAsync(cancellationToken);

        return Result<SafeguardingCaseDto>.Success(MapCase(sCase));
    }

    public async Task<Result<SafeguardingCaseDto>> GetCaseByIdAsync(
        Guid caseId,
        Guid officerUserId,
        CancellationToken cancellationToken = default)
    {
        var sCase = await (await VisibleCasesAsync(officerUserId, cancellationToken))
            .FirstOrDefaultAsync(c => c.Id == caseId, cancellationToken);

        if (sCase is null)
        {
            return Error.NotFound("SafeguardingCase");
        }

        // Write audit access log (BR-SG-06)
        var log = SafeguardingAccessLog.Create(caseId, officerUserId, "ViewCase");
        _db.SafeguardingAccessLogs.Add(log);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<SafeguardingCaseDto>.Success(MapCase(sCase));
    }

    public async Task<Result<IReadOnlyList<SafeguardingCaseDto>>> GetCasesAsync(
        Guid? organizationId,
        Guid officerUserId,
        CancellationToken cancellationToken = default)
    {
        var query = await VisibleCasesAsync(officerUserId, cancellationToken);

        if (organizationId.HasValue)
        {
            query = query.Where(c => c.OrganizationId == organizationId);
        }

        var cases = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        // P4-12: Write audit access log for case list access (BR-SG-06)
        var log = SafeguardingAccessLog.Create(Guid.Empty, officerUserId, "ListCases");
        _db.SafeguardingAccessLogs.Add(log);
        await _db.SaveChangesAsync(cancellationToken);

        var dtos = cases.Select(MapCase).ToList();
        return Result<IReadOnlyList<SafeguardingCaseDto>>.Success(dtos);
    }

    public async Task<Result> AddCaseNoteAsync(
        Guid caseId,
        Guid officerUserId,
        AddCaseNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var sCase = await (await VisibleCasesAsync(officerUserId, cancellationToken))
            .FirstOrDefaultAsync(c => c.Id == caseId, cancellationToken);

        if (sCase is null)
        {
            return Error.NotFound("SafeguardingCase");
        }

        var noteResult = SafeguardingCaseNote.Create(caseId, officerUserId, request.NoteText);
        if (noteResult.IsFailure)
        {
            return noteResult.Error!;
        }

        _db.SafeguardingCaseNotes.Add(noteResult.Value!);

        var log = SafeguardingAccessLog.Create(caseId, officerUserId, "AddNote");
        _db.SafeguardingAccessLogs.Add(log);

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<SafeguardingCaseDto>> AssignCaseAsync(
        Guid caseId,
        Guid officerUserId,
        AssignCaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var sCase = await (await VisibleCasesAsync(officerUserId, cancellationToken))
            .FirstOrDefaultAsync(c => c.Id == caseId, cancellationToken);

        if (sCase is null)
        {
            return Error.NotFound("SafeguardingCase");
        }

        // BR-SG-02: the assignee must actually be a safeguarding officer WITHIN
        // the case's own scope. Without this check a case could be handed to an
        // officer of another organization (or to a non-officer at all), and
        // VisibleCasesAsync would then grant them the case — reporter and
        // subject included. A miss is 404: the foreign assignee learns nothing.
        var assigneeOrgIds = await _orgReader
            .GetSafeguardingOfficerOrganizationIdsAsync(request.AssigneeOfficerUserId, cancellationToken);

        if (!IsAssigneeInCaseScope(sCase.OrganizationId, assigneeOrgIds))
        {
            return Error.NotFound("SafeguardingCase");
        }

        var assignResult = sCase.AssignToOfficer(request.AssigneeOfficerUserId);
        if (assignResult.IsFailure)
        {
            return assignResult.Error!;
        }

        var log = SafeguardingAccessLog.Create(caseId, officerUserId, $"AssignCase:{request.AssigneeOfficerUserId}");
        _db.SafeguardingAccessLogs.Add(log);

        await _db.SaveChangesAsync(cancellationToken);
        return Result<SafeguardingCaseDto>.Success(MapCase(sCase));
    }

    public async Task<Result<SafeguardingCaseDto>> CloseCaseAsync(
        Guid caseId,
        Guid officerUserId,
        CloseCaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var sCase = await (await VisibleCasesAsync(officerUserId, cancellationToken))
            .FirstOrDefaultAsync(c => c.Id == caseId, cancellationToken);

        if (sCase is null)
        {
            return Error.NotFound("SafeguardingCase");
        }

        var closeResult = sCase.Close(officerUserId, request.ResolutionNotes);
        if (closeResult.IsFailure)
        {
            return closeResult.Error!;
        }

        var log = SafeguardingAccessLog.Create(caseId, officerUserId, "CloseCase");
        _db.SafeguardingAccessLogs.Add(log);

        await _db.SaveChangesAsync(cancellationToken);
        return Result<SafeguardingCaseDto>.Success(MapCase(sCase));
    }

    /// <summary>
    /// Phase 4: an officer restricts the subject of a case they may see. The
    /// tenant comes from the case row, never from the request body, so an
    /// officer of another organization gets 404.
    /// </summary>
    public async Task<Result<SafeguardingRestrictionDto>> ImposeRestrictionAsync(
        Guid caseId,
        Guid officerUserId,
        ImposeRestrictionRequest request,
        CancellationToken cancellationToken = default)
    {
        var sCase = await (await VisibleCasesAsync(officerUserId, cancellationToken))
            .FirstOrDefaultAsync(c => c.Id == caseId, cancellationToken);
        if (sCase is null)
        {
            return Error.NotFound("SafeguardingCase");
        }

        var created = SafeguardingRestriction.Impose(
            sCase, officerUserId, request.CounterpartUserId,
            request.BlocksDiscovery, request.BlocksLocation, request.BlocksContact, request.BlocksAssignment);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        _db.SafeguardingRestrictions.Add(created.Value!);
        _db.SafeguardingAccessLogs.Add(SafeguardingAccessLog.Create(caseId, officerUserId, "ImposeRestriction"));

        // S-2 (Option B, 2026-09-30): a content-free operational task for the
        // case's organization. Org-less cases (platform scope) get NO task -
        // platform officers work their own queue. The task carries no
        // restriction kind, reason or case reference (BR-SG-05).
        if (sCase.OrganizationId is { } taskOrgId)
        {
            var openTask = await _db.OperationalTasks.FirstOrDefaultAsync(t =>
                t.OrganizationId == taskOrgId && t.SubjectUserId == created.Value!.SubjectUserId
                && t.CompletedAtUtc == null && t.CancelledAtUtc == null, cancellationToken);
            if (openTask is null)
            {
                _db.OperationalTasks.Add(OperationalTask.Create(
                    taskOrgId, created.Value!.SubjectUserId, officerUserId));
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<SafeguardingRestrictionDto>.Success(MapRestriction(created.Value!));
    }

    public async Task<Result<SafeguardingRestrictionDto>> LiftRestrictionAsync(
        Guid restrictionId,
        Guid officerUserId,
        LiftRestrictionRequest request,
        CancellationToken cancellationToken = default)
    {
        var restriction = await _db.SafeguardingRestrictions
            .FirstOrDefaultAsync(r => r.Id == restrictionId, cancellationToken);
        var visible = restriction is not null
            && await (await VisibleCasesAsync(officerUserId, cancellationToken))
                .AnyAsync(c => c.Id == restriction.CaseId, cancellationToken);
        if (!visible)
        {
            return Error.NotFound("SafeguardingRestriction");
        }

        var lifted = restriction!.Lift(officerUserId, request.Reason);
        if (lifted.IsFailure)
        {
            return lifted.Error!;
        }
        _db.SafeguardingAccessLogs.Add(SafeguardingAccessLog.Create(restriction.CaseId, officerUserId, "LiftRestriction"));

        // S-2: lifting the restriction closes the content-free operational task.
        var openTasks = await _db.OperationalTasks
            .Where(t => t.SubjectUserId == restriction.SubjectUserId
                        && t.CompletedAtUtc == null && t.CancelledAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var task in openTasks)
        {
            task.Cancel();
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<SafeguardingRestrictionDto>.Success(MapRestriction(restriction));
    }

    public async Task<Result<IReadOnlyList<SafeguardingRestrictionDto>>> GetRestrictionsAsync(
        Guid caseId,
        Guid officerUserId,
        CancellationToken cancellationToken = default)
    {
        var visible = await (await VisibleCasesAsync(officerUserId, cancellationToken))
            .AnyAsync(c => c.Id == caseId, cancellationToken);
        if (!visible)
        {
            return Error.NotFound("SafeguardingCase");
        }

        var list = await _db.SafeguardingRestrictions
            .AsNoTracking()
            .Where(r => r.CaseId == caseId)
            .OrderBy(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        _db.SafeguardingAccessLogs.Add(SafeguardingAccessLog.Create(caseId, officerUserId, "ListRestrictions"));
        await _db.SaveChangesAsync(cancellationToken);
        return Result<IReadOnlyList<SafeguardingRestrictionDto>>.Success(list.Select(MapRestriction).ToList());
    }

    private static SafeguardingRestrictionDto MapRestriction(SafeguardingRestriction r) => new(
        r.Id, r.CaseId, r.SubjectUserId, r.CounterpartUserId,
        r.BlocksDiscovery, r.BlocksLocation, r.BlocksContact, r.BlocksAssignment,
        r.CreatedAtUtc, r.LiftedAtUtc);

    /// <summary>
    /// Cases an officer may see (S-4, platform officer decision 2026-09-30):
    /// organization-attached cases of their organizations, **organization-less
    /// cases only for platform safeguarding officers**, and cases assigned to
    /// them. An ordinary organization officer no longer sees org-less cases.
    /// Anything else is a 404.
    /// </summary>
    /// <summary>
    /// S-4 mirror of the visibility rule: an org-scoped case may only be
    /// assigned to an officer of that same organization. An org-less case is
    /// reserved for platform safeguarding officers, so it has no ordinary
    /// assignee at all.
    /// </summary>
    private static bool IsAssigneeInCaseScope(Guid? caseOrganizationId, IReadOnlyCollection<Guid> assigneeOrgIds)
    {
        if (caseOrganizationId is not { } orgId)
        {
            return false;
        }

        return assigneeOrgIds.Contains(orgId);
    }

    private async Task<IQueryable<SafeguardingCase>> VisibleCasesAsync(Guid officerUserId, CancellationToken ct)
    {
        // BR-SG-02: the org scope is the organizations where the caller is an
        // active SafeguardingOfficer member. Coordinator/Admin roles never
        // count; the SafeguardingOfficer capability gate applies on top.
        var officerOrgs = await _orgReader.GetSafeguardingOfficerOrganizationIdsAsync(officerUserId, ct);
        var orgIds = officerOrgs.Select(id => (Guid?)id).ToList();

        // S-4: PLATFORM_LEVEL ownership is an explicit role now, never implied
        // by an organization officer membership or by OrganizationAdmin.
        var platformOfficerIds = await _orgReader.GetPlatformSafeguardingOfficerUserIdsAsync(ct);
        var isPlatformOfficer = platformOfficerIds.Contains(officerUserId);

        var scope = _db.SafeguardingCases.Where(c =>
            orgIds.Contains(c.OrganizationId)
            || c.AssignedOfficerUserId == officerUserId);

        if (isPlatformOfficer)
        {
            scope = _db.SafeguardingCases.Where(c =>
                c.OrganizationId == null
                || orgIds.Contains(c.OrganizationId)
                || c.AssignedOfficerUserId == officerUserId);
        }

        return scope;
    }

    private static SafeguardingCaseDto MapCase(SafeguardingCase c) => new(
        Id: c.Id,
        OrganizationId: c.OrganizationId,
        SubjectUserId: c.SubjectUserId,
        ReporterUserId: c.ReporterUserId,
        Severity: c.Severity,
        Category: c.Category,
        Summary: c.Summary,
        Status: c.Status,
        AssignedOfficerUserId: c.AssignedOfficerUserId,
        ResolutionNotes: c.ResolutionNotes,
        ResolvedAtUtc: c.ResolvedAtUtc,
        CreatedAtUtc: c.CreatedAtUtc);
}
