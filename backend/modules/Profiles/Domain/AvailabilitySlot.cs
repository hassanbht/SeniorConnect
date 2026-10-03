using SeniorConnect.Domain;

namespace SeniorConnect.Modules.Profiles.Domain;

public sealed class AvailabilitySlot : Entity
{
    private AvailabilitySlot() { }

    [DataClass(DataClass.Operational)]
    public Guid UserId { get; private set; }

    [DataClass(DataClass.Operational)]
    public DayOfWeek DayOfWeek { get; private set; }

    [DataClass(DataClass.Operational)]
    public TimeOnly StartTime { get; private set; }

    [DataClass(DataClass.Operational)]
    public TimeOnly EndTime { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateOnly? ValidFrom { get; private set; }

    [DataClass(DataClass.Operational)]
    public DateOnly? ValidUntil { get; private set; }

    public static AvailabilitySlot Create(
        Guid userId,
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        DateOnly? validFrom = null,
        DateOnly? validUntil = null)
    {
        return new AvailabilitySlot
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            DayOfWeek = dayOfWeek,
            StartTime = startTime,
            EndTime = endTime,
            ValidFrom = validFrom,
            ValidUntil = validUntil
        };
    }
}
