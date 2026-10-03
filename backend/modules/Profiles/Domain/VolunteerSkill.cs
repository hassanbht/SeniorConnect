using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Profiles.Domain;

public sealed class VolunteerSkill
{
    private VolunteerSkill() { }

    [DataClass(DataClass.Operational)]
    public Guid VolunteerProfileId { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid SkillId { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateTimeOffset? VerifiedAtUtc { get; private set; }

    [DataClass(DataClass.Operational)]
    public Guid? VerifiedByOrgId { get; private set; }

    public static VolunteerSkill Create(Guid volunteerProfileId, Guid skillId)
    {
        return new VolunteerSkill
        {
            VolunteerProfileId = volunteerProfileId,
            SkillId = skillId
        };
    }

    public void Verify(Guid orgId)
    {
        VerifiedAtUtc = DateTimeOffset.UtcNow;
        VerifiedByOrgId = orgId;
    }
}
