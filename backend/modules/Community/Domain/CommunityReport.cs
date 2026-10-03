using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Community.Domain;

public enum CommunityReportTargetType
{
    Group,
    Member,
    Post,
    Comment,
    Event,
    Message
}

public enum CommunityReportCategory
{
    Spam,
    Harassment,
    Hate,
    InappropriateContent,
    Impersonation,
    UnsafeBehavior,
    Other
}

public enum CommunityReportStatus
{
    Open,
    UnderReview,
    Resolved,
    EscalatedToSafeguarding,
    Dismissed
}

public sealed class CommunityReport : Entity, IAuditable
{
    private CommunityReport() { }

    [DataClass(DataClass.Operational)]
    public Guid ReporterUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public CommunityReportTargetType TargetType { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid TargetId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? GroupId { get; private set; }

    [DataClass(DataClass.Operational)]
    public CommunityReportCategory Category { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string Description { get; private set; } = string.Empty;

    [DataClass(DataClass.Operational)]
    public CommunityReportStatus Status { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string? ResolutionNote { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? ResolvedByUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    /// <summary>B-2 (Option A, 2026-09-30): traceability link after manual officer conversion.</summary>
    [DataClass(DataClass.Operational)]
    public Guid? SafeguardingCaseId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? ConvertedByOfficerUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? ConvertedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static Result<CommunityReport> Create(
        Guid reporterUserId,
        CommunityReportTargetType targetType,
        Guid targetId,
        CommunityReportCategory category,
        string description,
        Guid? groupId = null)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return Error.Validation("Description is required.");
        }

        var now = DateTimeOffset.UtcNow;
        return Result<CommunityReport>.Success(new CommunityReport
        {
            Id = Guid.CreateVersion7(),
            ReporterUserId = reporterUserId,
            TargetType = targetType,
            TargetId = targetId,
            GroupId = groupId,
            Category = category,
            Description = description.Trim(),
            Status = CommunityReportStatus.Open,
            CreatedAtUtc = now,
            CreatedBy = reporterUserId,
            UpdatedAtUtc = now,
            UpdatedBy = reporterUserId
        });
    }

    public void MarkUnderReview(Guid reviewerUserId)
    {
        Status = CommunityReportStatus.UnderReview;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = reviewerUserId;
    }

    public void Resolve(Guid resolverUserId, string? note = null)
    {
        Status = CommunityReportStatus.Resolved;
        ResolutionNote = note;
        ResolvedByUserId = resolverUserId;
        ResolvedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = resolverUserId;
    }

    /// <summary>B-2 (Option A): records the officer's manual conversion (idempotent guard upstream).</summary>
    public void LinkSafeguardingCase(Guid caseId, Guid officerUserId)
    {
        SafeguardingCaseId = caseId;
        ConvertedByOfficerUserId = officerUserId;
        ConvertedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = officerUserId;
    }

    public void EscalateToSafeguarding(Guid resolverUserId, string? note = null)
    {
        Status = CommunityReportStatus.EscalatedToSafeguarding;
        ResolutionNote = note;
        ResolvedByUserId = resolverUserId;
        ResolvedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = resolverUserId;
    }

    public void Dismiss(Guid resolverUserId, string? note = null)
    {
        Status = CommunityReportStatus.Dismissed;
        ResolutionNote = note;
        ResolvedByUserId = resolverUserId;
        ResolvedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = resolverUserId;
    }
}
