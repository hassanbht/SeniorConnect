using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Profiles.Domain;

namespace SeniorConnect.Modules.Profiles.Application;

public interface IProfilesDbContext
{
    DbSet<SupportProfile> SupportProfiles { get; }
    DbSet<VolunteerProfile> VolunteerProfiles { get; }
    DbSet<Interest> Interests { get; }
    DbSet<Language> Languages { get; }
    DbSet<UserLanguage> UserLanguages { get; }
    DbSet<Skill> Skills { get; }
    DbSet<VolunteerSkill> VolunteerSkills { get; }
    DbSet<AvailabilitySlot> AvailabilitySlots { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
