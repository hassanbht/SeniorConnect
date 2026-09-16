using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Community.Application;
using SeniorConnect.Modules.Community.Domain;
using SeniorConnect.Modules.Community.Infrastructure;
using SeniorConnect.Modules.Organizations.Domain;
using SeniorConnect.Modules.Organizations.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Community.Tests;

// P5-04 gate: cancelling one occurrence of a recurring event must not cancel
// the series.
public sealed class EventOccurrenceCancellationTests
{
    private static SeniorConnectDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SeniorConnectDbContext(options, new DefaultTenantContext { IsPlatformScope = true });
    }

    private static CommunityService CreateService(SeniorConnectDbContext db) =>
        new(db, new LocalMessageModerationService(), new OrganizationCoordinatorReader(db));

    private static async Task<(CommunityService service, Guid coordinatorId, Guid eventId, DateTimeOffset firstStart)> SeedRecurringEventAsync()
    {
        var db = CreateInMemoryDb();
        var org = Organization.Create("Freiwilligenzentrum Innsbruck-Land", OrganizationType.Ngo);
        db.Organizations.Add(org);
        var coordinatorId = Guid.NewGuid();
        var membership = OrganizationMembership.Create(org.Id, coordinatorId, MembershipRole.Coordinator);
        membership.Activate();
        db.OrganizationMemberships.Add(membership);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var created = await service.CreateEventAsync(coordinatorId, new CreateCommunityEventRequest(
            Title: "Wöchentlicher Spaziergang",
            Description: "Spaziergang im Stadtpark",
            StartsAtUtc: start,
            EndsAtUtc: start.AddHours(2),
            OrganizationId: org.Id,
            RecurrenceFrequency: EventRecurrenceFrequency.Weekly,
            RecurrenceUntilUtc: start.AddMonths(3)));

        return (service, coordinatorId, created.Value!.Id, start);
    }

    [Fact]
    public async Task Cancelling_one_occurrence_does_not_cancel_the_series()
    {
        var (service, coordinatorId, eventId, firstStart) = await SeedRecurringEventAsync();
        var secondOccurrence = firstStart.AddDays(7);

        var result = await service.CancelEventOccurrenceAsync(
            eventId, coordinatorId, new CancelEventOccurrenceRequest(secondOccurrence, "Regenwarnung"));

        result.IsSuccess.Should().BeTrue();

        var updatedEvent = result.Value!;
        updatedEvent.IsCancelled.Should().BeFalse("cancelling one occurrence must never cancel the whole series");
        updatedEvent.Occurrences.Should().ContainSingle(o => o.StartsAtUtc == secondOccurrence && o.IsCancelled);
        updatedEvent.Occurrences.Should().Contain(o => o.StartsAtUtc == firstStart && !o.IsCancelled);
    }

    [Fact]
    public async Task Cancelling_the_same_occurrence_twice_conflicts()
    {
        var (service, coordinatorId, eventId, firstStart) = await SeedRecurringEventAsync();

        var first = await service.CancelEventOccurrenceAsync(
            eventId, coordinatorId, new CancelEventOccurrenceRequest(firstStart, "Regenwarnung"));
        first.IsSuccess.Should().BeTrue();

        var second = await service.CancelEventOccurrenceAsync(
            eventId, coordinatorId, new CancelEventOccurrenceRequest(firstStart, "Again"));

        second.IsFailure.Should().BeTrue();
        second.Error!.Kind.Should().Be(ErrorKind.Conflict);
    }

    [Fact]
    public async Task Cancelling_a_date_that_is_not_an_occurrence_fails_validation()
    {
        var (service, coordinatorId, eventId, firstStart) = await SeedRecurringEventAsync();
        var notAnOccurrence = firstStart.AddDays(3);

        var result = await service.CancelEventOccurrenceAsync(
            eventId, coordinatorId, new CancelEventOccurrenceRequest(notAnOccurrence, "Wrong date"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.Validation);
    }

    [Fact]
    public async Task Cancelling_an_occurrence_of_a_non_recurring_event_fails_validation()
    {
        var db = CreateInMemoryDb();
        var org = Organization.Create("Freiwilligenzentrum Innsbruck-Land", OrganizationType.Ngo);
        db.Organizations.Add(org);
        var coordinatorId = Guid.NewGuid();
        var membership = OrganizationMembership.Create(org.Id, coordinatorId, MembershipRole.Coordinator);
        membership.Activate();
        db.OrganizationMemberships.Add(membership);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var created = await service.CreateEventAsync(coordinatorId, new CreateCommunityEventRequest(
            Title: "Einmaliges Treffen",
            Description: "x",
            StartsAtUtc: start,
            EndsAtUtc: start.AddHours(1),
            OrganizationId: org.Id));

        var result = await service.CancelEventOccurrenceAsync(
            created.Value!.Id, coordinatorId, new CancelEventOccurrenceRequest(start, "n/a"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.Validation);
    }

    [Fact]
    public async Task Cancelling_an_occurrence_by_a_non_staff_stranger_is_forbidden()
    {
        var (service, _, eventId, firstStart) = await SeedRecurringEventAsync();
        var stranger = Guid.NewGuid();

        var result = await service.CancelEventOccurrenceAsync(
            eventId, stranger, new CancelEventOccurrenceRequest(firstStart, "not allowed"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }
}
