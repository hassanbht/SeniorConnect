using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.TrustSafety.Application;
using SeniorConnect.Modules.TrustSafety.Domain;

namespace SeniorConnect.Modules.TrustSafety.Infrastructure;

public sealed class SafeguardingService : ISafeguardingService
{
    private readonly ISafeguardingDbContext _db;

    public SafeguardingService(ISafeguardingDbContext db)
    {
        _db = db;
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
        var sCase = await _db.SafeguardingCases
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
        var query = _db.SafeguardingCases.AsQueryable();

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
        var sCase = await _db.SafeguardingCases
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
        var sCase = await _db.SafeguardingCases
            .FirstOrDefaultAsync(c => c.Id == caseId, cancellationToken);

        if (sCase is null)
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
        var sCase = await _db.SafeguardingCases
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
