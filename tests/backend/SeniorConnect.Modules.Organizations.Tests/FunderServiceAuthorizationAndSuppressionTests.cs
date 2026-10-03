using FluentAssertions;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Reporting.Domain;
using SeniorConnect.Modules.Reporting.Infrastructure;
using Xunit;
using static SeniorConnect.Modules.Organizations.Tests.OrganizationAccessGuardTests;

namespace SeniorConnect.Modules.Organizations.Tests;

/// <summary>BR-FUNDER-03/04/05 and P2-34.</summary>
public sealed class FunderServiceAuthorizationAndSuppressionTests
{
    private static FunderMonthlyReportView Row(Guid funderId, Guid orgId, int month, string category,
        int people, int volunteers, int activities, double hours) => new()
    {
        FunderId = funderId,
        OrganizationId = orgId,
        Month = new DateOnly(2026, month, 1),
        CategoryCode = category,
        ActivityCount = activities,
        DistinctVolunteers = volunteers,
        DistinctPeopleSupported = people,
        Hours = hours,
        IsSuppressed = false,
    };

    [Fact]
    public async Task Summary_ReturnsNotFound_ForLoggedInUserWithoutFunderMembership()
    {
        // Arrange — the defect: any logged-in user could read any funder dashboard.
        using var db = CreateDb();
        var funder = Funder.Create("Gemeinde Test", FunderType.Municipality);
        db.Funders.Add(funder);
        await db.SaveChangesAsync();
        var service = new FunderService(db);

        // Act
        var result = await service.GetFunderSummaryAsync(Guid.NewGuid(), funder.Id);

        // Assert
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
    }

    [Fact]
    public async Task Dashboard_ReturnsNotFound_ForMemberOfAnotherFunder_AndForInvitedMember()
    {
        using var db = CreateDb();
        var funderA = Funder.Create("Gemeinde A", FunderType.Municipality);
        var funderB = Funder.Create("Gemeinde B", FunderType.Municipality);
        var memberOfB = Guid.NewGuid();
        var invitedToA = Guid.NewGuid();
        var membershipB = FunderMembership.Create(funderB.Id, memberOfB);
        membershipB.Activate();
        db.Funders.AddRange(funderA, funderB);
        db.FunderMemberships.AddRange(membershipB, FunderMembership.Create(funderA.Id, invitedToA));
        await db.SaveChangesAsync();
        var service = new FunderService(db);
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 12, 31);

        (await service.GetMultiOrgDashboardAsync(memberOfB, funderA.Id, from, to)).Error!.Kind.Should().Be(ErrorKind.NotFound);
        (await service.GetMultiOrgDashboardAsync(invitedToA, funderA.Id, from, to)).Error!.Kind.Should().Be(ErrorKind.NotFound);
    }

    [Fact]
    public async Task Summary_Succeeds_ForActiveMember_AndIsAudited()
    {
        using var db = CreateDb();
        var funder = Funder.Create("Gemeinde Test", FunderType.Municipality);
        var viewer = Guid.NewGuid();
        var membership = FunderMembership.Create(funder.Id, viewer);
        membership.Activate();
        db.Funders.Add(funder);
        db.FunderMemberships.Add(membership);
        db.FundingRelationships.Add(FundingRelationship.Create(funder.Id, Guid.NewGuid(), new DateOnly(2026, 1, 1)));
        await db.SaveChangesAsync();

        var result = await new FunderService(db).GetFunderSummaryAsync(viewer, funder.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalFundedOrganizations.Should().Be(1);
        db.AuditEntries.Should().ContainSingle(a =>
            a.Action == "funder.summary.read" && a.ActorUserId == viewer && a.SubjectId == funder.Id);
    }

    [Fact]
    public void A_seven_person_cohort_renders_as_below_ten_in_every_measure()
    {
        var row = Row(Guid.NewGuid(), Guid.NewGuid(), 6, "shopping", people: 7, volunteers: 12, activities: 31, hours: 46.5);

        var dto = FunderService.ToSuppressedDto(row);

        dto.IsSuppressed.Should().BeTrue();
        new[] { dto.ActivityCount, dto.DistinctVolunteers, dto.DistinctPeopleSupported, dto.TotalHours }
            .Should().AllBe(FunderService.Suppressed, "the whole row is masked, never cell by cell");
    }

    [Fact]
    public void A_suppressed_cohort_cannot_be_recovered_by_subtracting_visible_rows_from_the_total()
    {
        // Arrange: one visible cohort (12 people) and one suppressed (7 people).
        var funderId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var visible = Row(funderId, orgId, 6, "accompaniment", people: 12, volunteers: 11, activities: 40, hours: 80.0);
        var small = Row(funderId, orgId, 6, "shopping", people: 7, volunteers: 10, activities: 31, hours: 46.5);

        // Act
        var aggregate = FunderService.Aggregate([visible, small]);

        // Assert: totals equal the sum of the VISIBLE rows, so total − visible = 0.
        aggregate.TotalPeopleSupported.Should().Be("12");
        aggregate.TotalHours.Should().Be("80.0 h");
        aggregate.Rows.Single(r => r.CategoryCode == "shopping").IsSuppressed.Should().BeTrue();
        aggregate.TotalPeopleSupported.Should().NotBe("19", "19 − 12 would reveal the 7-person cohort");
    }

    [Fact]
    public void Totals_are_suppressed_when_the_visible_total_covers_fewer_than_ten_people()
    {
        var onlySmall = Row(Guid.NewGuid(), Guid.NewGuid(), 6, "shopping", people: 7, volunteers: 3, activities: 9, hours: 12.0);

        var aggregate = FunderService.Aggregate([onlySmall]);

        aggregate.TotalPeopleSupported.Should().Be(FunderService.Suppressed);
        aggregate.TotalHours.Should().Be(FunderService.Suppressed);
    }

    [Fact]
    public void A_row_suppressed_by_the_database_view_stays_suppressed()
    {
        var row = Row(Guid.NewGuid(), Guid.NewGuid(), 6, "shopping", people: 0, volunteers: 0, activities: 0, hours: 0);
        row.ActivityCount = null;
        row.DistinctPeopleSupported = null;
        row.DistinctVolunteers = null;
        row.Hours = null;
        row.IsSuppressed = true;

        FunderService.ToSuppressedDto(row).DistinctPeopleSupported.Should().Be(FunderService.Suppressed);
    }
}
