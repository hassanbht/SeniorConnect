using SeniorConnect.Domain;

namespace SeniorConnect.Modules.TrustSafety.Domain;

public enum SafeguardingSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum SafeguardingStatus
{
    Open,
    Assigned,
    UnderInvestigation,
    ActionTaken,
    Resolved,
    Closed
}

public sealed class SafeguardingCase : Entity, IAuditable
{
    private SafeguardingCase() { }

    [DataClass(DataClass.Operational)]
    public Guid? OrganizationId { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public Guid SubjectUserId { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public Guid ReporterUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public SafeguardingSeverity Severity { get; private set; }

    [DataClass(DataClass.Operational)]
    public string Category { get; private set; } = "general_concern";

    [DataClass(DataClass.PersonalData)]
    public string Summary { get; private set; } = string.Empty;

    [DataClass(DataClass.Operational)]
    public SafeguardingStatus Status { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? AssignedOfficerUserId { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? ResolutionNotes { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static Result<SafeguardingCase> Raise(
        Guid reporterUserId,
        Guid subjectUserId,
        string summary,
        SafeguardingSeverity severity = SafeguardingSeverity.Medium,
        string category = "general_concern",
        Guid? organizationId = null)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return Error.Validation("Concern summary cannot be empty.");
        }

        var now = DateTimeOffset.UtcNow;
        return Result<SafeguardingCase>.Success(new SafeguardingCase
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = organizationId,
            ReporterUserId = reporterUserId,
            SubjectUserId = subjectUserId,
            Summary = summary.Trim(),
            Severity = severity,
            Category = category,
            Status = SafeguardingStatus.Open,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
    }

    public Result AssignToOfficer(Guid officerUserId)
    {
        AssignedOfficerUserId = officerUserId;
        Status = SafeguardingStatus.Assigned;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result RecordAction(string notes)
    {
        Status = SafeguardingStatus.ActionTaken;
        ResolutionNotes = notes;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result Close(Guid closedByOfficerUserId, string resolutionNotes)
    {
        Status = SafeguardingStatus.Closed;
        ResolutionNotes = resolutionNotes;
        ResolvedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = closedByOfficerUserId;
        return Result.Success();
    }
}

public sealed class SafeguardingCaseNote : Entity
{
    private SafeguardingCaseNote() { }

    [DataClass(DataClass.Operational)]
    public Guid CaseId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid AuthorOfficerUserId { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string NoteText { get; private set; } = string.Empty;

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Result<SafeguardingCaseNote> Create(Guid caseId, Guid authorOfficerUserId, string noteText)
    {
        if (string.IsNullOrWhiteSpace(noteText))
        {
            return Error.Validation("Note text cannot be empty.");
        }

        return Result<SafeguardingCaseNote>.Success(new SafeguardingCaseNote
        {
            Id = Guid.CreateVersion7(),
            CaseId = caseId,
            AuthorOfficerUserId = authorOfficerUserId,
            NoteText = noteText.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
    }
}

public sealed class SafeguardingAccessLog : Entity
{
    private SafeguardingAccessLog() { }

    [DataClass(DataClass.Operational)]
    public Guid CaseId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid AccessedByUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public string Action { get; private set; } = "ViewCase";

    [DataClass(DataClass.Operational)]
    public DateTimeOffset AccessedAtUtc { get; private set; }

    public static SafeguardingAccessLog Create(Guid caseId, Guid accessedByUserId, string action = "ViewCase")
    {
        return new SafeguardingAccessLog
        {
            Id = Guid.CreateVersion7(),
            CaseId = caseId,
            AccessedByUserId = accessedByUserId,
            Action = action,
            AccessedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
