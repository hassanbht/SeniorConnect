using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Community.Domain;

public sealed class CommunityCategory : Entity
{
    private CommunityCategory() { }

    [DataClass(DataClass.PublicProfile)]
    public string Code { get; private set; } = string.Empty;

    [DataClass(DataClass.PublicProfile)]
    public string NameKey { get; private set; } = string.Empty;

    [DataClass(DataClass.PublicProfile)]
    public string? DescriptionKey { get; private set; }

    [DataClass(DataClass.Operational)]
    public GroupType GroupType { get; private set; }

    [DataClass(DataClass.Operational)]
    public int SortOrder { get; private set; }

    [DataClass(DataClass.Operational)]
    public bool IsActive { get; private set; }

    public static CommunityCategory Create(
        string code,
        string nameKey,
        GroupType groupType,
        int sortOrder = 0,
        string? descriptionKey = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(nameKey))
        {
            throw new ArgumentException("NameKey is required.", nameof(nameKey));
        }

        return new CommunityCategory
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim().ToLowerInvariant(),
            NameKey = nameKey.Trim(),
            DescriptionKey = descriptionKey?.Trim(),
            GroupType = groupType,
            SortOrder = sortOrder,
            IsActive = true
        };
    }
}
