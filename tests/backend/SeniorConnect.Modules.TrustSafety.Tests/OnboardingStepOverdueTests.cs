using FluentAssertions;
using SeniorConnect.Modules.TrustSafety.Domain;
using Xunit;

namespace SeniorConnect.Modules.TrustSafety.Tests;

/// <summary>
/// P2-23: days_open is COMPUTED, never stored, and drives the "overdue"
/// flag an applicant sees on their own pipeline status.
/// </summary>
public sealed class OnboardingStepOverdueTests
{
    [Fact]
    public void DaysOpen_IsNull_BeforeTheStepStarts()
    {
        var step = VolunteerApplicationStep.Create(Guid.NewGuid(), ApplicationStepType.Interview, sortOrder: 1, slaDays: 14);

        step.DaysOpen.Should().BeNull();
        step.IsOverdue.Should().BeFalse();
    }

    [Fact]
    public void IsOverdue_IsTrue_WhenOpenLongerThanSlaAndStillInProgress()
    {
        var step = VolunteerApplicationStep.Create(Guid.NewGuid(), ApplicationStepType.BackgroundCheck, sortOrder: 2, slaDays: 14);
        step.Start();

        SetOpenedAtUtc(step, DateTimeOffset.UtcNow.AddDays(-15));

        step.DaysOpen.Should().Be(15);
        step.IsOverdue.Should().BeTrue("a 15-day-old step against a 14-day SLA must show overdue");
    }

    [Fact]
    public void IsOverdue_IsFalse_OnceTheStepIsCompleted()
    {
        var step = VolunteerApplicationStep.Create(Guid.NewGuid(), ApplicationStepType.Approval, sortOrder: 5, slaDays: 14);
        step.Start();
        SetOpenedAtUtc(step, DateTimeOffset.UtcNow.AddDays(-30));

        step.Complete(Guid.NewGuid());

        step.IsOverdue.Should().BeFalse("a completed step is no longer awaited, regardless of how long it took");
        step.DaysOpen.Should().Be(30);
    }

    private static void SetOpenedAtUtc(VolunteerApplicationStep step, DateTimeOffset value)
    {
        typeof(VolunteerApplicationStep)
            .GetProperty(nameof(VolunteerApplicationStep.OpenedAtUtc))!
            .SetValue(step, value);
    }
}
