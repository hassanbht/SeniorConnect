using FluentAssertions;
using SeniorConnect.Domain;
using SeniorConnect.Modules.HelpRequests.Domain;
using Xunit;

namespace SeniorConnect.Modules.HelpRequests.Tests;

public sealed class ProductionLoadResilienceTests
{
    [Fact]
    public void Concurrent_Accept_On_Same_Request_Exactly_One_Wins()
    {
        var seniorId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var request = HelpRequest.Create(
            organizationId: null,
            seniorUserId: seniorId,
            createdByUserId: seniorId,
            categoryId: categoryId,
            safetyLevel: 1,
            trustLevel: 1,
            scheduledStartUtc: now.AddHours(2),
            scheduledEndUtc: now.AddHours(3),
            durationMinutes: 60,
            locationType: LocationType.PublicPlace,
            notes: "Grocery shopping assistance").Value!;

        int successCount = 0;
        int conflictCount = 0;
        var lockObj = new object();

        // 10 concurrent volunteer assign attempts with initial RowVersion = 1
        Parallel.For(0, 10, i =>
        {
            var volunteerId = Guid.NewGuid();
            var assignResult = request.Assign(volunteerId, expectedRowVersion: 1);

            lock (lockObj)
            {
                if (assignResult.IsSuccess)
                {
                    successCount++;
                }
                else if (assignResult.Error?.Kind == ErrorKind.Conflict)
                {
                    conflictCount++;
                }
            }
        });

        successCount.Should().Be(1, "Exactly one concurrent accept attempt must win");
        conflictCount.Should().Be(9, "All 9 other concurrent accept attempts must return Conflict");
        request.Status.Should().Be(HelpRequestStatus.Assigned);
    }

    [Fact]
    public void Rapid_Status_Transitions_Preserve_State_Machine_Integrity()
    {
        var seniorId = Guid.NewGuid();
        var volunteerId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var request = HelpRequest.Create(
            organizationId: null,
            seniorUserId: seniorId,
            createdByUserId: seniorId,
            categoryId: categoryId,
            safetyLevel: 1,
            trustLevel: 1,
            scheduledStartUtc: now.AddHours(2),
            scheduledEndUtc: now.AddHours(3),
            durationMinutes: 60,
            locationType: LocationType.PublicPlace).Value!;

        // 1. Assign
        var assign = request.Assign(volunteerId, expectedRowVersion: 1);
        assign.IsSuccess.Should().BeTrue();

        // 2. Check In
        var checkIn = request.CheckIn(volunteerId);
        checkIn.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(HelpRequestStatus.InProgress);

        // 3. Complete
        var complete = request.Complete(volunteerId, actualDurationMinutes: 60);
        complete.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(HelpRequestStatus.Completed);
        request.DurationMinutes.Should().Be(60);

        // 4. Illegal transition after completion is rejected
        var illegalCancel = request.Cancel(seniorId, "Too late", CancellationReasonCode.SeniorCancelled);
        illegalCancel.IsFailure.Should().BeTrue();
        illegalCancel.Error!.Kind.Should().Be(ErrorKind.Conflict);
    }

    [Fact]
    public void Senior_Cannot_Accept_Own_Help_Request_Under_Concurrency()
    {
        var seniorId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var request = HelpRequest.Create(
            organizationId: null,
            seniorUserId: seniorId,
            createdByUserId: seniorId,
            categoryId: categoryId,
            safetyLevel: 1,
            trustLevel: 1,
            scheduledStartUtc: now.AddHours(2),
            scheduledEndUtc: now.AddHours(3),
            durationMinutes: 60,
            locationType: LocationType.PublicPlace).Value!;

        // Reject self-assignment
        var selfAssign = request.Assign(seniorId, expectedRowVersion: 1);
        selfAssign.IsFailure.Should().BeTrue();
        selfAssign.Error!.Kind.Should().Be(ErrorKind.Validation);
    }
}
