using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.TrustSafety.Application;
using SeniorConnect.Modules.TrustSafety.Domain;
using SeniorConnect.Modules.TrustSafety.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.TrustSafety.Tests;

public sealed class ApplicantPipelineTests
{
    private sealed class TestTrustSafetyDbContext : DbContext, ITrustSafetyDbContext
    {
        public TestTrustSafetyDbContext(DbContextOptions<TestTrustSafetyDbContext> options)
            : base(options) { }

        public DbSet<VolunteerApplication> VolunteerApplications => Set<VolunteerApplication>();
        public DbSet<VolunteerApplicationStep> VolunteerApplicationSteps => Set<VolunteerApplicationStep>();
        public DbSet<BuddyAssignment> BuddyAssignments => Set<BuddyAssignment>();
        public DbSet<KeyCustody> KeyCustodies => Set<KeyCustody>();
        public DbSet<ExpenseRecord> ExpenseRecords => Set<ExpenseRecord>();
        public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
    }

    private static TestTrustSafetyDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<TestTrustSafetyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestTrustSafetyDbContext(options);
    }

    [Fact]
    public async Task ApplyAsync_CreatesOpenApplication_WithDefaultSteps()
    {
        using var db = CreateInMemoryDb();
        var service = new OnboardingService(db);
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        var result = await service.ApplyAsync(userId, new ApplyVolunteerRequest(orgId, "Ich möchte älteren Menschen helfen."));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Status.Should().Be(ApplicationStatus.Open);
        result.Value.OrganizationId.Should().Be(orgId);
        result.Value.UserId.Should().Be(userId);
        result.Value.Steps.Should().HaveCount(5);
        result.Value.Steps.Select(s => s.Step).Should().ContainInOrder(
            ApplicationStepType.Interview,
            ApplicationStepType.BackgroundCheck,
            ApplicationStepType.ConfidentialityAgreement,
            ApplicationStepType.Briefing,
            ApplicationStepType.Approval);
    }

    [Fact]
    public async Task GetUserApplicationsAsync_ReturnsApplicantPipelineStatus_WithSteps()
    {
        using var db = CreateInMemoryDb();
        var service = new OnboardingService(db);
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        await service.ApplyAsync(userId, new ApplyVolunteerRequest(orgId, "Motivation"));

        var result = await service.GetUserApplicationsAsync(userId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        var app = result.Value![0];
        app.UserId.Should().Be(userId);
        app.Status.Should().Be(ApplicationStatus.Open);
        app.Steps.Should().HaveCount(5);
        app.Steps[0].SlaDays.Should().Be(7);
    }

    [Fact]
    public async Task DecideApplication_WhenDeclined_StoresDeclineReason_VisibleToApplicant()
    {
        using var db = CreateInMemoryDb();
        var service = new OnboardingService(db);
        var userId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        var applyResult = await service.ApplyAsync(userId, new ApplyVolunteerRequest(orgId, "Motivation"));
        var appId = applyResult.Value!.Id;

        const string reason = "Aktuell keine freien Kapazitäten in Ihrer Region. Bitte versuchen Sie es in 3 Monaten erneut.";
        var decideResult = await service.DecideApplicationAsync(
            appId,
            new DecideApplicationRequest(Approved: false, Reason: reason),
            staffId);

        decideResult.IsSuccess.Should().BeTrue();

        var userAppsResult = await service.GetUserApplicationsAsync(userId);
        userAppsResult.IsSuccess.Should().BeTrue();
        userAppsResult.Value.Should().HaveCount(1);

        var app = userAppsResult.Value![0];
        app.Status.Should().Be(ApplicationStatus.Declined);
        app.DeclineReason.Should().Be(reason);
        app.DecidedByUserId.Should().Be(staffId);
    }

    [Fact]
    public async Task GetUserApplicationsAsync_IsUserIsolated()
    {
        using var db = CreateInMemoryDb();
        var service = new OnboardingService(db);
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        await service.ApplyAsync(userA, new ApplyVolunteerRequest(orgId, "Motivation A"));
        await service.ApplyAsync(userB, new ApplyVolunteerRequest(orgId, "Motivation B"));

        var resultA = await service.GetUserApplicationsAsync(userA);
        var resultB = await service.GetUserApplicationsAsync(userB);

        resultA.Value.Should().HaveCount(1);
        resultA.Value![0].UserId.Should().Be(userA);

        resultB.Value.Should().HaveCount(1);
        resultB.Value![0].UserId.Should().Be(userB);
    }
}
