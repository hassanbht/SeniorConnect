using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Community.Domain;

public enum CreatorType
{
    Individual,
    Volunteer,
    OrganizationStaff,
    VerifiedOrganization
}

public enum GroupType
{
    SeniorCommunity,
    MigrantLanguageCommunity,
    InterestGroup,
    LearningGroup,
    FamilyGroup,
    VolunteerGroup,
    NeighborhoodGroup,
    OrganizationCommunity,
    SupportGroup
}

public enum GroupVisibility
{
    Public,
    Private,
    OrganizationPrivate
}

public enum GroupStatus
{
    Draft,
    Active,
    Paused,
    Archived,
    Blocked
}

public enum GroupVerificationStatus
{
    Unverified,
    OrganizationVerified,
    PlatformVerified
}

public sealed class CommunityGroup : Entity, IAuditable, ISoftDeletable, IOrganizationScoped
{
    private CommunityGroup() { }

    [DataClass(DataClass.Operational)]
    public Guid CreatorUserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public CreatorType CreatorType { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? OrganizationId { get; private set; }

    [DataClass(DataClass.Operational)]
    public string Name { get; private set; } = string.Empty;

    [DataClass(DataClass.PersonalData)]
    public string Description { get; private set; } = string.Empty;

    [DataClass(DataClass.Operational)]
    public GroupType Type { get; private set; }

    [DataClass(DataClass.Operational)]
    public string Category { get; private set; } = "general";

    [DataClass(DataClass.Operational)]
    public GroupVisibility Visibility { get; private set; } = GroupVisibility.Public;

    [DataClass(DataClass.Operational)]
    public GroupStatus Status { get; private set; } = GroupStatus.Active;

    [DataClass(DataClass.Operational)]
    public string? City { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? Region { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? PostalCode { get; private set; }

    [DataClass(DataClass.Operational)]
    public double? Latitude { get; private set; }

    [DataClass(DataClass.Operational)]
    public double? Longitude { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? PrimaryLanguageCode { get; private set; }

    [DataClass(DataClass.Operational)]
    public string? CoverImageUrl { get; private set; }

    [DataClass(DataClass.PersonalData)]
    public string Rules { get; private set; } = string.Empty;

    [DataClass(DataClass.Operational)]
    public int? MaxMembers { get; private set; }

    [DataClass(DataClass.Operational)]
    public GroupVerificationStatus VerificationStatus { get; private set; } = GroupVerificationStatus.Unverified;

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
        CreatorType creatorType,
        string name,
        string description,
        GroupType type,
        GroupVisibility visibility = GroupVisibility.Public,
        Guid? organizationId = null,
        string? city = null,
        string? region = null,
        string? postalCode = null,
        double? latitude = null,
        double? longitude = null,
        string? primaryLanguageCode = null,
        string? coverImageUrl = null,
        string? rules = null,
        int? maxMembers = null,
        GroupVerificationStatus verificationStatus = GroupVerificationStatus.Unverified)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("Name is required.");
        }

        if (!GeoCoordinate.IsValidPair(latitude, longitude))
        {
            return GeoCoordinate.Invalid();
        }

        if (creatorType == CreatorType.VerifiedOrganization || creatorType == CreatorType.OrganizationStaff)
        {
            if (organizationId is null || organizationId == Guid.Empty)
            {
                return Error.OrganizationRequiredForGroup();
            }
        }

        // Private/organization-private groups must not be created by a plain individual
        // without an organization context unless explicitly allowed later.
        if (visibility == GroupVisibility.OrganizationPrivate && (organizationId is null || organizationId == Guid.Empty))
        {
            return Error.Validation("Organization-private groups require an organization.");
        }

        var now = DateTimeOffset.UtcNow;
        var group = new CommunityGroup
        {
            Id = Guid.CreateVersion7(),
            CreatorUserId = creatorUserId,
            CreatorType = creatorType,
            OrganizationId = organizationId,
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Type = type,
            Category = MapTypeToCategory(type),
            Visibility = visibility,
            Status = GroupStatus.Active,
            City = city?.Trim(),
            Region = region?.Trim(),
            PostalCode = postalCode?.Trim(),
            Latitude = latitude,
            Longitude = longitude,
            PrimaryLanguageCode = primaryLanguageCode?.Trim().ToLowerInvariant(),
            CoverImageUrl = coverImageUrl?.Trim(),
            Rules = rules?.Trim() ?? string.Empty,
            MaxMembers = maxMembers > 0 ? maxMembers : null,
            VerificationStatus = verificationStatus,
            IsDeleted = false,
            CreatedAtUtc = now,
            CreatedBy = creatorUserId,
            UpdatedAtUtc = now,
            UpdatedBy = creatorUserId
        };

        return Result<CommunityGroup>.Success(group);
    }

    public Result Update(
        string name,
        string description,
        GroupType type,
        GroupVisibility visibility,
        string? city,
        string? region,
        string? postalCode,
        double? latitude,
        double? longitude,
        string? primaryLanguageCode,
        string? coverImageUrl,
        string? rules,
        int? maxMembers,
        Guid updatedByUserId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("Name is required.");
        }

        if (!GeoCoordinate.IsValidPair(latitude, longitude))
        {
            return GeoCoordinate.Invalid();
        }

        if (visibility == GroupVisibility.OrganizationPrivate && (OrganizationId is null || OrganizationId == Guid.Empty))
        {
            return Error.Validation("Organization-private groups require an organization.");
        }

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        Type = type;
        Category = MapTypeToCategory(type);
        Visibility = visibility;
        City = city?.Trim();
        Region = region?.Trim();
        PostalCode = postalCode?.Trim();
        Latitude = latitude;
        Longitude = longitude;
        PrimaryLanguageCode = primaryLanguageCode?.Trim().ToLowerInvariant();
        CoverImageUrl = coverImageUrl?.Trim();
        Rules = rules?.Trim() ?? string.Empty;
        MaxMembers = maxMembers > 0 ? maxMembers : null;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedByUserId;

        return Result.Success();
    }

    public void Archive(Guid updatedByUserId)
    {
        Status = GroupStatus.Archived;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedByUserId;
    }

    public void Pause(Guid updatedByUserId)
    {
        Status = GroupStatus.Paused;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedByUserId;
    }

    public void Activate(Guid updatedByUserId)
    {
        Status = GroupStatus.Active;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedByUserId;
    }

    /// <summary>
    /// BR-COMM-05: a name/description flagged by screening is held (Draft) — not
    /// discoverable, not joinable — until a human reviewer activates or blocks it.
    /// </summary>
    public void HoldForReview()
    {
        Status = GroupStatus.Draft;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Block(Guid updatedByUserId)
    {
        Status = GroupStatus.Blocked;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedByUserId;
    }

    public void SetVerificationStatus(GroupVerificationStatus status, Guid updatedByUserId)
    {
        VerificationStatus = status;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedByUserId;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static string MapTypeToCategory(GroupType type) => type switch
    {
        GroupType.SeniorCommunity => "senior_community",
        GroupType.MigrantLanguageCommunity => "migrant_language_community",
        GroupType.InterestGroup => "interest_group",
        GroupType.LearningGroup => "learning_group",
        GroupType.FamilyGroup => "family_group",
        GroupType.VolunteerGroup => "volunteer_group",
        GroupType.NeighborhoodGroup => "neighborhood_group",
        GroupType.OrganizationCommunity => "organization_community",
        GroupType.SupportGroup => "support_group",
        _ => "general"
    };
}
