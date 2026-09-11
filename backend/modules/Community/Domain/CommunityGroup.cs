using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Community.Domain;

public enum GroupVisibilityScope
{
    Public,
    LocalNeighborhood,
    OrganizationMembers,
    PrivateInvitation
}

public enum GroupJoinPolicy
{
    Open,
    RequestApproval,
    InviteOnly
}

public sealed class CommunityGroup : Entity, IAuditable, ISoftDeletable, IOrganizationScoped
{
    private CommunityGroup() { }

    [DataClass(DataClass.Operational)]
    public Guid CreatorUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? OrganizationId { get; private set; }

    [DataClass(DataClass.Operational)]
    public string Title { get; private set; } = string.Empty;

    [DataClass(DataClass.PersonalData)]
    public string Description { get; private set; } = string.Empty;

    [DataClass(DataClass.Operational)]
    public string Category { get; private set; } = "general";

    [DataClass(DataClass.Operational)]
    public GroupVisibilityScope Scope { get; private set; } = GroupVisibilityScope.Public;

    [DataClass(DataClass.Operational)]
    public GroupJoinPolicy JoinPolicy { get; private set; } = GroupJoinPolicy.Open;

    [DataClass(DataClass.PersonalData)]
    public string? LocationPostalCode { get; private set; }

    [DataClass(DataClass.Operational)]
    public int? MaxMembers { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsArchived { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsDeleted { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? CreatedBy { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? UpdatedBy { get; private set; }

    public static Result<CommunityGroup> Create(
        Guid creatorUserId,
        string title,
        string description,
        string category = "general",
        GroupVisibilityScope scope = GroupVisibilityScope.Public,
        GroupJoinPolicy joinPolicy = GroupJoinPolicy.Open,
        Guid? organizationId = null,
        string? locationPostalCode = null,
        int? maxMembers = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Error.Validation("Title is required.");
        }

        if (organizationId is null || organizationId == Guid.Empty)
        {
            // BR-COMM-06 / ADR-020: only an Organization may publish a public
            // group or page. Individual users get no self-service group
            // creation. See docs/decisions/ADR-020.
            return Error.OrganizationRequiredForGroup();
        }

        var now = DateTimeOffset.UtcNow;
        var group = new CommunityGroup
        {
            Id = Guid.CreateVersion7(),
            CreatorUserId = creatorUserId,
            OrganizationId = organizationId,
            Title = title.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Category = string.IsNullOrWhiteSpace(category) ? "general" : category.Trim().ToLowerInvariant(),
            Scope = scope,
            JoinPolicy = joinPolicy,
            LocationPostalCode = locationPostalCode?.Trim(),
            MaxMembers = maxMembers > 0 ? maxMembers : null,
            IsArchived = false,
            IsDeleted = false,
            CreatedAtUtc = now,
            CreatedBy = creatorUserId,
            UpdatedAtUtc = now,
            UpdatedBy = creatorUserId
        };

        return Result<CommunityGroup>.Success(group);
    }

    public Result Update(
        string title,
        string description,
        string category,
        GroupVisibilityScope scope,
        GroupJoinPolicy joinPolicy,
        string? locationPostalCode,
        int? maxMembers,
        Guid updatedByUserId)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Error.Validation("Title is required.");
        }

        Title = title.Trim();
        Description = description?.Trim() ?? string.Empty;
        Category = string.IsNullOrWhiteSpace(category) ? "general" : category.Trim().ToLowerInvariant();
        Scope = scope;
        JoinPolicy = joinPolicy;
        LocationPostalCode = locationPostalCode?.Trim();
        MaxMembers = maxMembers > 0 ? maxMembers : null;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedByUserId;

        return Result.Success();
    }

    public void Archive(Guid updatedByUserId)
    {
        IsArchived = true;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedByUserId;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
