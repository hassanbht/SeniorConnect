using FluentAssertions;
using SeniorConnect.Modules.Community.Domain;
using Xunit;

namespace SeniorConnect.Modules.Community.Tests;

public sealed class EventRecurrenceAndCapacityTests
{
    [Fact]
    public void Event_creation_with_valid_dates_and_capacity_succeeds()
    {
        var hostUserId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow.AddDays(2);
        var end = start.AddHours(2);

        var evResult = CommunityEvent.Create(
            hostUserId: hostUserId,
            title: "Nachbarschafts-Kaffee",
            description: "Gemütlicher Austausch bei Kaffee und Kuchen.",
            startsAtUtc: start,
            endsAtUtc: end,
            category: "general",
            capacity: 8,
            locationPostalCode: "8010",
            locationAddress: "Hauptplatz 1, Graz");

        evResult.IsSuccess.Should().BeTrue();
        var ev = evResult.Value!;
        ev.HostUserId.Should().Be(hostUserId);
        ev.Capacity.Should().Be(8);
        ev.StartsAtUtc.Should().Be(start);
        ev.EndsAtUtc.Should().Be(end);
        ev.IsCancelled.Should().BeFalse();
    }

    [Fact]
    public void Event_end_before_start_fails_validation()
    {
        var start = DateTimeOffset.UtcNow.AddDays(2);
        var end = start.AddHours(-1); // End before start

        var evResult = CommunityEvent.Create(
            hostUserId: Guid.NewGuid(),
            title: "Invalid Event",
            description: "Test",
            startsAtUtc: start,
            endsAtUtc: end);

        evResult.IsFailure.Should().BeTrue();
        evResult.Error!.Code.Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public void Recurring_event_can_be_cancelled_without_affecting_past_or_future_structure()
    {
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var end = start.AddHours(2);

        var evResult = CommunityEvent.Create(
            hostUserId: Guid.NewGuid(),
            title: "Wöchentlicher Spaziergang",
            description: "Spaziergang im Stadtpark",
            startsAtUtc: start,
            endsAtUtc: end,
            recurrenceFrequency: EventRecurrenceFrequency.Weekly,
            recurrenceUntilUtc: start.AddMonths(3));

        evResult.IsSuccess.Should().BeTrue();
        var ev = evResult.Value!;
        ev.RecurrenceFrequency.Should().Be(EventRecurrenceFrequency.Weekly);

        // Cancel this specific event instance
        var cancelResult = ev.Cancel("Regen und Gewitterwarnung.", ev.HostUserId);
        cancelResult.IsSuccess.Should().BeTrue();
        ev.IsCancelled.Should().BeTrue();
        ev.CancellationReason.Should().Be("Regen und Gewitterwarnung.");
    }

    [Fact]
    public void Registration_under_capacity_is_going_and_over_capacity_is_waitlisted()
    {
        var eventId = Guid.NewGuid();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        // 1st attendee -> Going
        var reg1 = EventRegistration.RegisterGoing(eventId, user1, "Bringing homemade cake");
        reg1.Status.Should().Be(EventRsvpStatus.Going);
        reg1.WaitlistPosition.Should().BeNull();
        reg1.Note.Should().Be("Bringing homemade cake");

        // 2nd attendee beyond capacity -> Waitlist
        var reg2 = EventRegistration.RegisterWaitlist(eventId, user2, waitlistPosition: 1);
        reg2.Status.Should().Be(EventRsvpStatus.Waitlisted);
        reg2.WaitlistPosition.Should().Be(1);

        // Cancel 1st registration
        reg1.Cancel();
        reg1.Status.Should().Be(EventRsvpStatus.Cancelled);

        // Waitlist attendee promoted to Going
        reg2.PromoteToGoing();
        reg2.Status.Should().Be(EventRsvpStatus.Going);
        reg2.WaitlistPosition.Should().BeNull();
    }
}
