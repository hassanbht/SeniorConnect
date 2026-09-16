using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Family.Contracts;
using SeniorConnect.Modules.HelpRequests.Application;
using SeniorConnect.Modules.HelpRequests.Domain;
using SeniorConnect.Modules.HelpRequests.Infrastructure;
using SeniorConnect.Modules.Identity.Contracts;
using SeniorConnect.Modules.Profiles.Contracts;
using SeniorConnect.Modules.TrustSafety.Contracts;
using Xunit;

namespace SeniorConnect.Modules.HelpRequests.Tests;

public sealed class CreateHelpRequestOnBehalfTests
{
    private sealed class TestTenantContext : ITenantContext
    {
        public Guid? OrganizationId => null;
        public bool IsPlatformScope => true;
    }

    private static SeniorConnectDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SeniorConnectDbContext(options, new TestTenantContext());
    }

    private sealed class MockFamilyPermissionReader : IFamilyPermissionReader
    {
        private readonly HashSet<(Guid Caregiver, Guid Senior, string Perm)> _permissions = [];

        public void Grant(Guid caregiver, Guid senior, string perm) => _permissions.Add((caregiver, senior, perm));

        public Task<bool> HasPermissionAsync(Guid caregiverUserId, Guid seniorUserId, string permissionType, CancellationToken ct = default)
        {
            return Task.FromResult(_permissions.Contains((caregiverUserId, seniorUserId, permissionType)));
        }
    }

    private sealed class StubTrustLevelReader : ITrustLevelReader
    {
        public Task<int> GetEffectiveTrustLevelAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(1);
        public Task<IReadOnlyDictionary<Guid, int>> GetEffectiveTrustLevelsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, int>>(userIds.ToDictionary(id => id, _ => 1));
    }

    private sealed class StubSafetyBoundaryReader : ISafetyBoundaryReader
    {
        public Task<bool> IsBlockedAsync(Guid userIdA, Guid userIdB, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<Guid>> GetBlockedUserIdsAsync(Guid userId, IReadOnlyCollection<Guid> candidateUserIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<bool> IsBuddyRequiredForLevel3Async(Guid volunteerUserId, CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class StubUserContactReader : IUserContactReader
    {
        public Task<UserContact?> GetContactAsync(Guid userId, CancellationToken ct = default) => Task.FromResult<UserContact?>(null);
    }

    private sealed class StubReliabilityUpdater : IVolunteerReliabilityUpdater
    {
        public Task<decimal?> RecordOutcomeAsync(Guid volunteerUserId, bool wasReliable, CancellationToken cancellationToken = default) => Task.FromResult<decimal?>(1.0m);
        public Task RestoreScoreAsync(Guid volunteerUserId, decimal? score, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static ActivityCategory CreateCategory(string code, string nameKey, int safetyLevel = 1, bool isBlocked = false)
    {
        var category = (ActivityCategory)Activator.CreateInstance(typeof(ActivityCategory), nonPublic: true)!;
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.Id))!.SetValue(category, Guid.NewGuid());
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.Code))!.SetValue(category, code);
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.NameKey))!.SetValue(category, nameKey);
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.DefaultSafetyLevel))!.SetValue(category, safetyLevel);
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.IsBlocked))!.SetValue(category, isBlocked);
        typeof(ActivityCategory).GetProperty(nameof(ActivityCategory.IsActive))!.SetValue(category, true);
        return category;
    }

    [Fact]
    public async Task CreateHelpRequestAsync_WhenActingOnBehalfWithPermission_Succeeds()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var category = CreateCategory("groceries", "category.groceries", 1, isBlocked: false);
        db.ActivityCategories.Add(category);
        await db.SaveChangesAsync();

        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();
        var permReader = new MockFamilyPermissionReader();
        permReader.Grant(caregiverId, seniorId, "CreateHelpRequestsOnBehalf");

        var service = new HelpRequestService(
            db,
            new ActivitySafetyPolicy(),
            new StubTrustLevelReader(),
            new StubSafetyBoundaryReader(),
            new StubUserContactReader(),
            new StubReliabilityUpdater(),
            permReader);

        var now = DateTimeOffset.UtcNow;
        var request = new CreateHelpRequestRequest(
            OrganizationId: null,
            CategoryId: category.Id,
            ScheduledStartUtc: now.AddHours(2),
            ScheduledEndUtc: now.AddHours(4),
            DurationMinutes: 120,
            LocationType: LocationType.SeniorHome,
            SeniorUserId: seniorId);

        // Act: caregiver creates request on behalf of senior
        var result = await service.CreateHelpRequestAsync(caregiverId, seniorId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.SeniorUserId.Should().Be(seniorId);
        result.Value.CreatedByUserId.Should().Be(caregiverId);

        var saved = await db.HelpRequests.FirstAsync(r => r.Id == result.Value.Id);
        saved.SeniorUserId.Should().Be(seniorId);
        saved.CreatedByUserId.Should().Be(caregiverId);
    }

    [Fact]
    public async Task CreateHelpRequestAsync_WhenActingOnBehalfWithoutPermission_ReturnsForbidden()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var category = CreateCategory("groceries", "category.groceries", 1, isBlocked: false);
        db.ActivityCategories.Add(category);
        await db.SaveChangesAsync();

        var seniorId = Guid.NewGuid();
        var caregiverId = Guid.NewGuid();
        // Permission is NOT granted
        var permReader = new MockFamilyPermissionReader();

        var service = new HelpRequestService(
            db,
            new ActivitySafetyPolicy(),
            new StubTrustLevelReader(),
            new StubSafetyBoundaryReader(),
            new StubUserContactReader(),
            new StubReliabilityUpdater(),
            permReader);

        var now = DateTimeOffset.UtcNow;
        var request = new CreateHelpRequestRequest(
            OrganizationId: null,
            CategoryId: category.Id,
            ScheduledStartUtc: now.AddHours(2),
            ScheduledEndUtc: now.AddHours(4),
            DurationMinutes: 120,
            LocationType: LocationType.SeniorHome,
            SeniorUserId: seniorId);

        // Act
        var result = await service.CreateHelpRequestAsync(caregiverId, seniorId, request);

        // Assert: rejected by BR-HELP-04
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("FORBIDDEN");
        result.Error.Detail.Should().Contain("permission");
    }
}
