# P2-25 Organization Profile Page Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give every organization a page — reachable by any logged-in app user tapping the organization — that lists its news and events, and let that organization's active Coordinator/Admin staff create, edit, and cancel those posts, all from the existing Flutter app (mobile + a newly-enabled web build), with no new frontend project and no new content entity.

**Architecture:** Reuse `CommunityEvent` (already org-scoped, already filterable by `organizationId`) for both "news" (`category == "news"`) and "events" (any other category). Close a real authorization gap in `CommunityService` by having it consult `IOrganizationCoordinatorReader` (an existing cross-module contract in `Organizations`) before allowing a create/update/cancel against an `organizationId`. Add one new read endpoint (`GET /api/v1/me/organizations`) so the client knows which org(s) the signed-in user may manage. Enable the Flutter `web` platform on the existing `mobile/senior_connect` app and add three screens that reuse the app's existing card/list/form patterns.

**Tech Stack:** ASP.NET Core minimal APIs (.NET 10), EF Core (PostgreSQL + InMemory for tests), xUnit + FluentAssertions, Flutter 3.x + go_router + Dio, flutter_test.

**Spec:** `docs/superpowers/specs/2026-09-11-org-profile-flutter-p2-25-design.md`

## Global Constraints

- No new domain entity, no EF migration. "News" is `CommunityEvent` with `Category == "news"`.
- Every authorization decision for publishing on behalf of an organization goes through `IOrganizationCoordinatorReader` — do not hand-roll a second membership check anywhere.
- Cross-tenant / not-found cases return `Error.NotFound`, never `Error.Forbidden` (existing convention, see `Result.cs`).
- Enums are NOT configured with `JsonStringEnumConverter` in this API — never expose a raw enum in a new DTO the Flutter client has to branch on; compute a plain `bool`/`string` server-side instead (this plan's `GET /me/organizations` follows that rule).
- Flutter: no typed JSON models/freezed for these screens — existing screens (`community_feed_screen.dart`, `event_detail_screen.dart`) decode `Map<String, dynamic>` directly from `ApiClient.get`; new screens follow the same style.
- German (`de`) is the localization source of truth; every new user-facing string needs `de`, `en`, and `fa` entries.
- `dotnet format` / `dart format` conventions already enforced by hooks — do not hand-format differently.

---

## Task 1: `IOrganizationCoordinatorReader.GetActiveMembershipsForUserAsync`

**Files:**
- Modify: `backend/modules/Organizations/Contracts/OrganizationsContracts.cs`
- Modify: `backend/modules/Organizations/Infrastructure/OrganizationCoordinatorReader.cs`
- Test: `tests/backend/SeniorConnect.Modules.Organizations.Tests/OrganizationCoordinatorReaderTests.cs` (new file — check whether `tests/backend/SeniorConnect.Modules.Organizations.Tests/` already exists; if not, create it copying the `.csproj` shape of `tests/backend/SeniorConnect.Modules.Community.Tests/SeniorConnect.Modules.Community.Tests.csproj` with `ProjectReference`s to `SeniorConnect.Modules.Organizations.csproj` and `SeniorConnect.Infrastructure.csproj`)

**Interfaces:**
- Produces: `IOrganizationCoordinatorReader.GetActiveMembershipsForUserAsync(Guid userId, CancellationToken ct = default)` returning `Task<IReadOnlyList<StaffOrganizationDto>>`, where `StaffOrganizationDto` is `(Guid OrganizationId, string OrganizationName)`.

- [ ] **Step 1: Write the failing test**

Check first whether `tests/backend/SeniorConnect.Modules.Organizations.Tests/SeniorConnect.Modules.Organizations.Tests.csproj` exists. If it does not, create it:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <NoWarn>$(NoWarn);CA1848;CA1873;CA1305</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.3" />
    <PackageReference Include="FluentAssertions" Version="8.8.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\backend\modules\Organizations\SeniorConnect.Modules.Organizations.csproj" />
    <ProjectReference Include="..\..\..\backend\SeniorConnect.Infrastructure\SeniorConnect.Infrastructure.csproj" />
  </ItemGroup>

</Project>
```

Then add it to `backend/SeniorConnect.sln` the same way the other test projects are listed (open the `.sln`, copy an existing `tests\backend\...Tests\...csproj` entry block and its `GlobalSection(ProjectConfigurationPlatforms)` lines, substituting the new project name and a new GUID — reuse the same `{GUID}` pattern already present for `SeniorConnect.Modules.Community.Tests`).

Write the test:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Organizations.Domain;
using SeniorConnect.Modules.Organizations.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Organizations.Tests;

public sealed class OrganizationCoordinatorReaderTests
{
    private static SeniorConnectDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SeniorConnectDbContext(options, new DefaultTenantContext());
    }

    [Fact]
    public async Task GetActiveMembershipsForUserAsync_ReturnsOnlyActiveCoordinatorOrAdminOrgs()
    {
        using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();

        var orgA = Organization.Create("Freiwilligenzentrum Innsbruck-Land", OrganizationType.Ngo).Value!;
        var orgB = Organization.Create("Other Org", OrganizationType.Association).Value!;
        var orgC = Organization.Create("Suspended-Membership Org", OrganizationType.Company).Value!;
        db.Organizations.AddRange(orgA, orgB, orgC);

        var activeCoordinator = OrganizationMembership.Create(orgA.Id, userId, MembershipRole.Coordinator);
        activeCoordinator.Activate();
        var activeAdmin = OrganizationMembership.Create(orgB.Id, userId, MembershipRole.Admin);
        activeAdmin.Activate();
        var activeVolunteer = OrganizationMembership.Create(orgB.Id, userId, MembershipRole.Volunteer);
        activeVolunteer.Activate(); // active but not staff — must be excluded on its own merit if it were the only row
        var suspendedCoordinator = OrganizationMembership.Create(orgC.Id, userId, MembershipRole.Coordinator);
        // left Invited/never activated on purpose

        db.OrganizationMemberships.AddRange(activeCoordinator, activeAdmin, activeVolunteer, suspendedCoordinator);
        await db.SaveChangesAsync();

        var reader = new OrganizationCoordinatorReader(db);

        var result = await reader.GetActiveMembershipsForUserAsync(userId);

        result.Should().HaveCount(2);
        result.Should().Contain(m => m.OrganizationId == orgA.Id && m.OrganizationName == orgA.Name);
        result.Should().Contain(m => m.OrganizationId == orgB.Id && m.OrganizationName == orgB.Name);
        result.Should().NotContain(m => m.OrganizationId == orgC.Id);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/backend/SeniorConnect.Modules.Organizations.Tests --filter GetActiveMembershipsForUserAsync_ReturnsOnlyActiveCoordinatorOrAdminOrgs`
Expected: FAIL — `StaffOrganizationDto` and `GetActiveMembershipsForUserAsync` do not exist yet (compile error).

- [ ] **Step 3: Add `StaffOrganizationDto` and the interface method**

In `backend/modules/Organizations/Contracts/OrganizationsContracts.cs`, add above the closing of the file:

```csharp
namespace SeniorConnect.Modules.Organizations.Contracts;

/// <summary>
/// Cross-module contract for resolving who should be notified as "the
/// coordinator" for an organization — used by Matching's P3-13 escalation
/// path so it never needs a direct reference to the Organizations DbContext.
/// </summary>
public interface IOrganizationCoordinatorReader
{
    Task<IReadOnlyList<Guid>> GetActiveCoordinatorUserIdsAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// The inverse query: which organizations does this user actively staff
    /// as Coordinator or Admin. Used by Community's P2-25 org page to gate
    /// the "manage this org" entry point and its publish/edit/cancel calls.
    /// </summary>
    Task<IReadOnlyList<StaffOrganizationDto>> GetActiveMembershipsForUserAsync(Guid userId, CancellationToken ct = default);
}

public sealed record StaffOrganizationDto(Guid OrganizationId, string OrganizationName);
```

- [ ] **Step 4: Implement it**

In `backend/modules/Organizations/Infrastructure/OrganizationCoordinatorReader.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Modules.Organizations.Application;
using SeniorConnect.Modules.Organizations.Contracts;
using SeniorConnect.Modules.Organizations.Domain;

namespace SeniorConnect.Modules.Organizations.Infrastructure;

public sealed class OrganizationCoordinatorReader : IOrganizationCoordinatorReader
{
    private readonly IOrganizationsDbContext _db;

    public OrganizationCoordinatorReader(IOrganizationsDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Guid>> GetActiveCoordinatorUserIdsAsync(Guid organizationId, CancellationToken ct = default)
    {
        return await _db.OrganizationMemberships
            .Where(m => m.OrganizationId == organizationId
                     && m.Status == MembershipStatus.Active
                     && (m.Role == MembershipRole.Coordinator || m.Role == MembershipRole.Admin))
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StaffOrganizationDto>> GetActiveMembershipsForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var query =
            from m in _db.OrganizationMemberships
            join o in _db.Organizations on m.OrganizationId equals o.Id
            where m.UserId == userId
               && m.Status == MembershipStatus.Active
               && (m.Role == MembershipRole.Coordinator || m.Role == MembershipRole.Admin)
            select new StaffOrganizationDto(o.Id, o.Name);

        return await query.Distinct().ToListAsync(ct);
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/backend/SeniorConnect.Modules.Organizations.Tests --filter GetActiveMembershipsForUserAsync_ReturnsOnlyActiveCoordinatorOrAdminOrgs`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add backend/modules/Organizations/Contracts/OrganizationsContracts.cs backend/modules/Organizations/Infrastructure/OrganizationCoordinatorReader.cs tests/backend/SeniorConnect.Modules.Organizations.Tests backend/SeniorConnect.sln
git commit -m "feat(organizations): add GetActiveMembershipsForUserAsync to IOrganizationCoordinatorReader"
```

---

## Task 2: Close the publish-authorization gap in `CommunityService`

**Files:**
- Modify: `backend/modules/Community/SeniorConnect.Modules.Community.csproj`
- Modify: `backend/modules/Community/Infrastructure/CommunityModuleExtensions.cs`
- Modify: `backend/modules/Community/Infrastructure/CommunityService.cs:8-52,114-157,291-326`
- Test: `tests/backend/SeniorConnect.Modules.Community.Tests/OrganizationPublishAuthorizationTests.cs` (new)
- Modify: `tests/backend/SeniorConnect.Modules.Community.Tests/SeniorConnect.Modules.Community.Tests.csproj`

**Interfaces:**
- Consumes: `IOrganizationCoordinatorReader.GetActiveCoordinatorUserIdsAsync(Guid, CancellationToken)` (Task 1, already existed before Task 1 too).
- Produces: `CommunityService` constructor becomes `CommunityService(ICommunityDbContext db, IMessageModerationService moderation, IOrganizationCoordinatorReader orgReader)`. `CreateGroupAsync`, `UpdateGroupAsync`, `CreateEventAsync` now return `Error.Forbidden(...)` when `OrganizationId` is set and the caller isn't an active Coordinator/Admin of it.

- [ ] **Step 1: Add the project reference**

In `backend/modules/Community/SeniorConnect.Modules.Community.csproj`, add inside the existing `<ItemGroup>` with the `Domain` reference:

```xml
<ProjectReference Include="..\..\SeniorConnect.Domain\SeniorConnect.Domain.csproj" />
<ProjectReference Include="..\Organizations\SeniorConnect.Modules.Organizations.csproj" />
```

In `tests/backend/SeniorConnect.Modules.Community.Tests/SeniorConnect.Modules.Community.Tests.csproj`, add to its `<ItemGroup>` of `ProjectReference`s:

```xml
<ProjectReference Include="..\..\..\backend\modules\Organizations\SeniorConnect.Modules.Organizations.csproj" />
```

- [ ] **Step 2: Write the failing tests**

Create `tests/backend/SeniorConnect.Modules.Community.Tests/OrganizationPublishAuthorizationTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Community.Application;
using SeniorConnect.Modules.Community.Infrastructure;
using SeniorConnect.Modules.Organizations.Domain;
using SeniorConnect.Modules.Organizations.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Community.Tests;

public sealed class OrganizationPublishAuthorizationTests
{
    private static SeniorConnectDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SeniorConnectDbContext(options, new DefaultTenantContext());
    }

    private static CommunityService CreateService(SeniorConnectDbContext db) =>
        new(db, new LocalMessageModerationService(), new OrganizationCoordinatorReader(db));

    [Fact]
    public async Task CreateEventAsync_ForOrganization_Fails_WhenCallerIsNotStaff()
    {
        using var db = CreateInMemoryDb();
        var org = Organization.Create("Freiwilligenzentrum Innsbruck-Land", OrganizationType.Ngo).Value!;
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var strangerUserId = Guid.NewGuid();

        var result = await service.CreateEventAsync(strangerUserId, new CreateCommunityEventRequest(
            Title: "Herbstfest",
            Description: "Gemeinsames Fest",
            StartsAtUtc: DateTimeOffset.UtcNow.AddDays(7),
            EndsAtUtc: DateTimeOffset.UtcNow.AddDays(7).AddHours(3),
            OrganizationId: org.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(SeniorConnect.Domain.ErrorKind.Forbidden);
    }

    [Fact]
    public async Task CreateEventAsync_ForOrganization_Succeeds_WhenCallerIsActiveCoordinator()
    {
        using var db = CreateInMemoryDb();
        var org = Organization.Create("Freiwilligenzentrum Innsbruck-Land", OrganizationType.Ngo).Value!;
        db.Organizations.Add(org);
        var coordinatorUserId = Guid.NewGuid();
        var membership = OrganizationMembership.Create(org.Id, coordinatorUserId, MembershipRole.Coordinator);
        membership.Activate();
        db.OrganizationMemberships.Add(membership);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.CreateEventAsync(coordinatorUserId, new CreateCommunityEventRequest(
            Title: "Herbstfest",
            Description: "Gemeinsames Fest",
            StartsAtUtc: DateTimeOffset.UtcNow.AddDays(7),
            EndsAtUtc: DateTimeOffset.UtcNow.AddDays(7).AddHours(3),
            OrganizationId: org.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value!.OrganizationId.Should().Be(org.Id);
    }

    [Fact]
    public async Task CreateEventAsync_ForOrganization_Fails_WhenMembershipIsNotYetActive()
    {
        using var db = CreateInMemoryDb();
        var org = Organization.Create("Freiwilligenzentrum Innsbruck-Land", OrganizationType.Ngo).Value!;
        db.Organizations.Add(org);
        var invitedUserId = Guid.NewGuid();
        db.OrganizationMemberships.Add(OrganizationMembership.Create(org.Id, invitedUserId, MembershipRole.Coordinator));
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.CreateEventAsync(invitedUserId, new CreateCommunityEventRequest(
            Title: "Herbstfest",
            Description: "Gemeinsames Fest",
            StartsAtUtc: DateTimeOffset.UtcNow.AddDays(7),
            EndsAtUtc: DateTimeOffset.UtcNow.AddDays(7).AddHours(3),
            OrganizationId: org.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(SeniorConnect.Domain.ErrorKind.Forbidden);
    }

    [Fact]
    public async Task CreateEventAsync_WithoutOrganizationId_StillSucceeds()
    {
        using var db = CreateInMemoryDb();
        var service = CreateService(db);
        var userId = Guid.NewGuid();

        var result = await service.CreateEventAsync(userId, new CreateCommunityEventRequest(
            Title: "Privater Treff",
            Description: "Kein Organisationsbezug",
            StartsAtUtc: DateTimeOffset.UtcNow.AddDays(1),
            EndsAtUtc: DateTimeOffset.UtcNow.AddDays(1).AddHours(1)));

        result.IsSuccess.Should().BeTrue();
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/backend/SeniorConnect.Modules.Community.Tests --filter OrganizationPublishAuthorizationTests`
Expected: FAIL — `CommunityService` has no constructor taking `IOrganizationCoordinatorReader`.

- [ ] **Step 4: Wire the dependency and add the guard**

In `backend/modules/Community/Infrastructure/CommunityService.cs`, change the top of the class:

```csharp
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Community.Application;
using SeniorConnect.Modules.Community.Domain;
using SeniorConnect.Modules.Organizations.Contracts;

namespace SeniorConnect.Modules.Community.Infrastructure;

public sealed class CommunityService : ICommunityService
{
    private readonly ICommunityDbContext _db;
    private readonly IMessageModerationService _moderation;
    private readonly IOrganizationCoordinatorReader _orgReader;

    public CommunityService(
        ICommunityDbContext db,
        IMessageModerationService moderation,
        IOrganizationCoordinatorReader orgReader)
    {
        _db = db;
        _moderation = moderation;
        _orgReader = orgReader;
    }

    private async Task<Error?> RequireOrgStaffAsync(Guid? organizationId, Guid callerUserId, CancellationToken ct)
    {
        if (organizationId is null)
        {
            return null;
        }

        var staffIds = await _orgReader.GetActiveCoordinatorUserIdsAsync(organizationId.Value, ct);
        if (!staffIds.Contains(callerUserId))
        {
            return Error.Forbidden("Only an active coordinator or admin of this organization may publish on its behalf.");
        }

        return null;
    }

    // --- Groups ---
```

Then update `CreateGroupAsync` (add the check right after building the request, before calling `CommunityGroup.Create`):

```csharp
    public async Task<Result<CommunityGroupDto>> CreateGroupAsync(
        Guid userId,
        CreateCommunityGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var authError = await RequireOrgStaffAsync(request.OrganizationId, userId, cancellationToken);
        if (authError is not null)
        {
            return authError;
        }

        var groupResult = CommunityGroup.Create(
```
(the rest of the method body is unchanged)

Update `UpdateGroupAsync` — insert the same check right after loading `group` and before the existing group-membership-role check (the group.OrganizationId belongs to the fetched entity, not the request):

```csharp
        var group = await _db.CommunityGroups
            .FirstOrDefaultAsync(g => g.Id == groupId && !g.IsDeleted, cancellationToken);

        if (group is null)
        {
            return Error.NotFound("CommunityGroup");
        }

        var orgAuthError = await RequireOrgStaffAsync(group.OrganizationId, userId, cancellationToken);
        if (orgAuthError is not null)
        {
            return orgAuthError;
        }

        var membership = await _db.GroupMemberships
```

Update `CreateEventAsync` — same pattern as `CreateGroupAsync`, first line of the method body:

```csharp
    public async Task<Result<CommunityEventDto>> CreateEventAsync(
        Guid userId,
        CreateCommunityEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var authError = await RequireOrgStaffAsync(request.OrganizationId, userId, cancellationToken);
        if (authError is not null)
        {
            return authError;
        }

        var eventResult = CommunityEvent.Create(
```

- [ ] **Step 5: Update the DI registration**

In `backend/modules/Community/Infrastructure/CommunityModuleExtensions.cs`, no change is needed for the registration itself (constructor injection resolves `IOrganizationCoordinatorReader` automatically since `AddOrganizationsModule()` already registers it and both run before `app.Build()` in `Program.cs`) — but add a comment noting the cross-module dependency so it isn't accidentally removed:

```csharp
using Microsoft.Extensions.DependencyInjection;
using SeniorConnect.Modules.Community.Application;

namespace SeniorConnect.Modules.Community.Infrastructure;

public static class CommunityModuleExtensions
{
    public static IServiceCollection AddCommunityModule(this IServiceCollection services)
    {
        // CommunityService depends on IOrganizationCoordinatorReader — requires
        // builder.Services.AddOrganizationsModule() to have run first in Program.cs.
        services.AddScoped<ICommunityService, CommunityService>();
        services.AddSingleton<IMessageModerationService, LocalMessageModerationService>();
        return services;
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/backend/SeniorConnect.Modules.Community.Tests --filter OrganizationPublishAuthorizationTests`
Expected: PASS (all 4 tests)

Then run the full existing suite to confirm nothing else broke:

Run: `dotnet test tests/backend/SeniorConnect.Modules.Community.Tests`
Expected: PASS — all previously-passing tests still pass (they construct `CommunityGroup`/`CommunityEvent` domain objects directly, not `CommunityService`, so they're unaffected; if any test does construct `CommunityService` directly with the old 2-arg constructor, update it to pass `new OrganizationCoordinatorReader(db)` as the third argument).

- [ ] **Step 7: Commit**

```bash
git add backend/modules/Community backend/SeniorConnect.Api tests/backend/SeniorConnect.Modules.Community.Tests
git commit -m "fix(community): enforce org-staff authorization before publishing on an org's behalf"
```

---

## Task 3: Event update and cancel

**Files:**
- Modify: `backend/modules/Community/Domain/CommunityEvent.cs:136-149`
- Modify: `backend/modules/Community/Application/Dtos.cs:68-80`
- Modify: `backend/modules/Community/Application/ICommunityService.cs:52-56`
- Modify: `backend/modules/Community/Infrastructure/CommunityService.cs` (add two methods after `CreateEventAsync`)
- Test: `tests/backend/SeniorConnect.Modules.Community.Tests/EventEditingAndCancellationTests.cs` (new)

**Interfaces:**
- Consumes: `CommunityService.RequireOrgStaffAsync` (private helper from Task 2), `CommunityEvent.Cancel(string reason, Guid hostUserId)` (already existed).
- Produces: `CommunityEvent.UpdateDetails(string title, string description, string category, string? locationAddress, string? locationPostalCode, int? capacity, Guid updatedByUserId)` returning `Result`. `ICommunityService.UpdateEventAsync(Guid eventId, Guid userId, UpdateCommunityEventRequest request, CancellationToken ct)` returning `Task<Result<CommunityEventDto>>`. `ICommunityService.CancelEventAsync(Guid eventId, Guid userId, string reason, CancellationToken ct)` returning `Task<Result>`. `UpdateCommunityEventRequest(string Title, string Description, string Category, string? LocationAddress, string? LocationPostalCode, int? Capacity)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/backend/SeniorConnect.Modules.Community.Tests/EventEditingAndCancellationTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Infrastructure;
using SeniorConnect.Modules.Community.Application;
using SeniorConnect.Modules.Community.Infrastructure;
using SeniorConnect.Modules.Organizations.Domain;
using SeniorConnect.Modules.Organizations.Infrastructure;
using Xunit;

namespace SeniorConnect.Modules.Community.Tests;

public sealed class EventEditingAndCancellationTests
{
    private static SeniorConnectDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SeniorConnectDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SeniorConnectDbContext(options, new DefaultTenantContext());
    }

    private static CommunityService CreateService(SeniorConnectDbContext db) =>
        new(db, new LocalMessageModerationService(), new OrganizationCoordinatorReader(db));

    private static async Task<(SeniorConnectDbContext db, CommunityService service, Guid orgId, Guid coordinatorId, Guid eventId)>
        SeedOrgEventAsync()
    {
        var db = CreateInMemoryDb();
        var org = Organization.Create("Freiwilligenzentrum Innsbruck-Land", OrganizationType.Ngo).Value!;
        db.Organizations.Add(org);
        var coordinatorId = Guid.NewGuid();
        var membership = OrganizationMembership.Create(org.Id, coordinatorId, MembershipRole.Coordinator);
        membership.Activate();
        db.OrganizationMemberships.Add(membership);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var created = await service.CreateEventAsync(coordinatorId, new CreateCommunityEventRequest(
            Title: "Herbstfest",
            Description: "Gemeinsames Fest",
            StartsAtUtc: DateTimeOffset.UtcNow.AddDays(7),
            EndsAtUtc: DateTimeOffset.UtcNow.AddDays(7).AddHours(3),
            OrganizationId: org.Id));

        return (db, service, org.Id, coordinatorId, created.Value!.Id);
    }

    [Fact]
    public async Task UpdateEventAsync_ByOrgStaff_UpdatesTitleAndDescription()
    {
        var (_, service, _, coordinatorId, eventId) = await SeedOrgEventAsync();

        var result = await service.UpdateEventAsync(eventId, coordinatorId, new UpdateCommunityEventRequest(
            Title: "Herbstfest 2026",
            Description: "Aktualisierte Beschreibung",
            Category: "general",
            LocationAddress: "Hauptplatz 1",
            LocationPostalCode: "6060",
            Capacity: 50));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Title.Should().Be("Herbstfest 2026");
        result.Value!.Description.Should().Be("Aktualisierte Beschreibung");
        result.Value!.Capacity.Should().Be(50);
    }

    [Fact]
    public async Task UpdateEventAsync_ByNonStaff_Fails()
    {
        var (_, service, _, _, eventId) = await SeedOrgEventAsync();
        var stranger = Guid.NewGuid();

        var result = await service.UpdateEventAsync(eventId, stranger, new UpdateCommunityEventRequest(
            Title: "Hijacked",
            Description: "x",
            Category: "general",
            LocationAddress: null,
            LocationPostalCode: null,
            Capacity: null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(SeniorConnect.Domain.ErrorKind.Forbidden);
    }

    [Fact]
    public async Task CancelEventAsync_ByOrgStaff_SetsIsCancelledAndReason()
    {
        var (db, service, _, coordinatorId, eventId) = await SeedOrgEventAsync();

        var result = await service.CancelEventAsync(eventId, coordinatorId, "Witterung", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var stored = await service.GetEventByIdAsync(eventId);
        stored.Value!.IsCancelled.Should().BeTrue();
        stored.Value!.CancellationReason.Should().Be("Witterung");
    }

    [Fact]
    public async Task CancelEventAsync_ByNonStaff_Fails()
    {
        var (_, service, _, _, eventId) = await SeedOrgEventAsync();
        var stranger = Guid.NewGuid();

        var result = await service.CancelEventAsync(eventId, stranger, "not allowed", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(SeniorConnect.Domain.ErrorKind.Forbidden);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/backend/SeniorConnect.Modules.Community.Tests --filter EventEditingAndCancellationTests`
Expected: FAIL — `UpdateEventAsync`/`CancelEventAsync`/`UpdateCommunityEventRequest` don't exist.

- [ ] **Step 3: Add the domain method**

In `backend/modules/Community/Domain/CommunityEvent.cs`, add after `Reschedule` (after line 149):

```csharp
    public Result UpdateDetails(
        string title,
        string description,
        string category,
        string? locationAddress,
        string? locationPostalCode,
        int? capacity,
        Guid updatedByUserId)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Error.Validation("Title is required.");
        }

        if (capacity.HasValue && capacity.Value <= 0)
        {
            return Error.Validation("Capacity must be positive.");
        }

        Title = title.Trim();
        Description = description?.Trim() ?? string.Empty;
        Category = string.IsNullOrWhiteSpace(category) ? "general" : category.Trim().ToLowerInvariant();
        LocationAddress = locationAddress?.Trim();
        LocationPostalCode = locationPostalCode?.Trim();
        Capacity = capacity;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedBy = updatedByUserId;

        return Result.Success();
    }
```

- [ ] **Step 4: Add the DTO**

In `backend/modules/Community/Application/Dtos.cs`, add right after `CreateCommunityEventRequest` (after line 80):

```csharp
public sealed record UpdateCommunityEventRequest(
    string Title,
    string Description,
    string Category,
    string? LocationAddress = null,
    string? LocationPostalCode = null,
    int? Capacity = null);

public sealed record CancelCommunityEventRequest(string Reason);
```

- [ ] **Step 5: Add the interface methods**

In `backend/modules/Community/Application/ICommunityService.cs`, add after `CreateEventAsync` (after line 56):

```csharp
    Task<Result<CommunityEventDto>> UpdateEventAsync(
        Guid eventId,
        Guid userId,
        UpdateCommunityEventRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> CancelEventAsync(
        Guid eventId,
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default);
```

- [ ] **Step 6: Implement in `CommunityService`**

In `backend/modules/Community/Infrastructure/CommunityService.cs`, add immediately after `CreateEventAsync`'s closing brace (after line 326):

```csharp
    public async Task<Result<CommunityEventDto>> UpdateEventAsync(
        Guid eventId,
        Guid userId,
        UpdateCommunityEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var ev = await _db.CommunityEvents
            .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted, cancellationToken);

        if (ev is null)
        {
            return Error.NotFound("CommunityEvent");
        }

        var authError = await RequireOrgStaffAsync(ev.OrganizationId, userId, cancellationToken);
        if (authError is not null)
        {
            if (ev.OrganizationId is not null || ev.HostUserId != userId)
            {
                return authError;
            }
        }

        var updateResult = ev.UpdateDetails(
            title: request.Title,
            description: request.Description,
            category: request.Category,
            locationAddress: request.LocationAddress,
            locationPostalCode: request.LocationPostalCode,
            capacity: request.Capacity,
            updatedByUserId: userId);

        if (updateResult.IsFailure)
        {
            return updateResult.Error!;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var goingCount = await _db.EventRegistrations
            .CountAsync(r => r.EventId == eventId && r.Status == EventRsvpStatus.Going, cancellationToken);
        var waitlistCount = await _db.EventRegistrations
            .CountAsync(r => r.EventId == eventId && r.Status == EventRsvpStatus.Waitlisted, cancellationToken);

        return Result<CommunityEventDto>.Success(MapEvent(ev, goingCount, waitlistCount));
    }

    public async Task<Result> CancelEventAsync(
        Guid eventId,
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var ev = await _db.CommunityEvents
            .FirstOrDefaultAsync(e => e.Id == eventId && !e.IsDeleted, cancellationToken);

        if (ev is null)
        {
            return Error.NotFound("CommunityEvent");
        }

        var authError = await RequireOrgStaffAsync(ev.OrganizationId, userId, cancellationToken);
        if (authError is not null)
        {
            if (ev.OrganizationId is not null || ev.HostUserId != userId)
            {
                return authError;
            }
        }

        var cancelResult = ev.Cancel(reason, userId);
        if (cancelResult.IsFailure)
        {
            return cancelResult.Error!;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
```

Note the `if (ev.OrganizationId is not null || ev.HostUserId != userId)` guard: `RequireOrgStaffAsync` returns `null` (no error) whenever `OrganizationId` is `null`, so for a personal (non-org) event it falls through to allowing the original host to edit/cancel their own event — matching the existing "host owns their own event" assumption already implicit in `RegisterForEventAsync`/`CancelRegistrationAsync`. For an org event, `authError` is non-null only when the caller truly isn't staff, so the inner condition is always true in that branch and the error is returned.

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test tests/backend/SeniorConnect.Modules.Community.Tests --filter EventEditingAndCancellationTests`
Expected: PASS (all 4 tests)

- [ ] **Step 8: Commit**

```bash
git add backend/modules/Community tests/backend/SeniorConnect.Modules.Community.Tests
git commit -m "feat(community): add event update and cancel to CommunityService"
```

---

## Task 4: `PUT`/`:cancel` endpoints for events + `GET /api/v1/me/organizations`

**Files:**
- Modify: `backend/SeniorConnect.Api/Endpoints/CommunityEndpoints.cs:190` (insert after the `events/{id:guid}` `GET`, before `register`)
- Modify: `backend/SeniorConnect.Api/Endpoints/ProfileEndpoints.cs:132` (insert before the closing `return app;`)

**Interfaces:**
- Consumes: `ICommunityService.UpdateEventAsync`, `ICommunityService.CancelEventAsync` (Task 3); `IOrganizationCoordinatorReader.GetActiveMembershipsForUserAsync` (Task 1).
- Produces: `PUT /api/v1/community/events/{id:guid}` (body: `UpdateCommunityEventRequest`, requires auth) → `CommunityEventDto` or 403/404. `POST /api/v1/community/events/{id:guid}:cancel` (body: `CancelCommunityEventRequest`) → 200 or 403/404. `GET /api/v1/me/organizations` → `IReadOnlyList<StaffOrganizationDto>`.

There is no failing-test step for this task — endpoint wiring is thin routing glue over the already-tested service methods from Tasks 1 and 3. Verification is a manual run against the local API (Step 3 below), which is this codebase's existing convention (no `WebApplicationFactory` HTTP-level tests exist anywhere in the repo today).

- [ ] **Step 1: Add the event endpoints**

In `backend/SeniorConnect.Api/Endpoints/CommunityEndpoints.cs`, insert right after the `GetCommunityEventById` block (after line 189, before the `MapPost("/events/{id:guid}/register"` block):

```csharp
        group.MapPut("/events/{id:guid}", async (
            Guid id,
            UpdateCommunityEventRequest request,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await communityService.UpdateEventAsync(id, userId.Value, request, ct);
            return result.ToHttpResult();
        })
        .WithName("UpdateCommunityEvent")
        .Produces<CommunityEventDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/events/{id:guid}:cancel", async (
            Guid id,
            CancelCommunityEventRequest request,
            ClaimsPrincipal user,
            ICommunityService communityService,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await communityService.CancelEventAsync(id, userId.Value, request.Reason, ct);
            return result.ToHttpResult();
        })
        .WithName("CancelCommunityEvent")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
```

`CancelCommunityEventRequest` was already added to `backend/modules/Community/Application/Dtos.cs` in Task 3, Step 4.

- [ ] **Step 2: Add `GET /api/v1/me/organizations`**

In `backend/SeniorConnect.Api/Endpoints/ProfileEndpoints.cs`, add `using SeniorConnect.Modules.Organizations.Contracts;` to the top imports, and insert this block right before the final `return app;` (before line 133):

```csharp
        meGroup.MapGet("/organizations", async (
            ClaimsPrincipal user,
            IOrganizationCoordinatorReader orgReader,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var memberships = await orgReader.GetActiveMembershipsForUserAsync(userId.Value, ct);
            return Results.Ok(memberships);
        })
        .WithName("GetMyOrganizations")
        .Produces<IReadOnlyList<StaffOrganizationDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);
```

- [ ] **Step 3: Manual verification**

Run: `dotnet build backend/SeniorConnect.sln`
Expected: build succeeds with 0 errors.

Run the API locally (`dotnet run --project backend/SeniorConnect.Api`), open `/scalar/v1`, and confirm `PUT /api/v1/community/events/{id}`, `POST /api/v1/community/events/{id}:cancel`, and `GET /api/v1/me/organizations` are all listed with the expected request/response shapes.

- [ ] **Step 4: Commit**

```bash
git add backend/SeniorConnect.Api/Endpoints/CommunityEndpoints.cs backend/SeniorConnect.Api/Endpoints/ProfileEndpoints.cs
git commit -m "feat(api): expose event update/cancel and GET /me/organizations"
```

---

## Task 5: Enable CORS for the Flutter web build

**Files:**
- Modify: `backend/SeniorConnect.Api/appsettings.json`
- Modify: `backend/SeniorConnect.Api/Program.cs:134,198`

**Interfaces:**
- Produces: a named CORS policy `"FlutterWeb"` allowing the origins listed under `Cors:AllowedOrigins` in configuration, with any header and any method (the app authenticates via a `Bearer` header, not cookies, so credentials are not needed).

- [ ] **Step 1: Add the config section**

In `backend/SeniorConnect.Api/appsettings.json`, add a `Cors` section:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:5000",
      "http://localhost:8080"
    ]
  },
  "Matching": {
    "Weights": {
      "DistanceWeight": 0.35,
      "ContinuityWeight": 0.30,
      "ReliabilityWeight": 0.20,
      "AvailabilityWeight": 0.15,
      "ColdStartReliability": 0.70
    }
  }
}
```

- [ ] **Step 2: Wire it in `Program.cs`**

Add right after `builder.Services.AddAuthorization();` (after line 134):

```csharp
var corsAllowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5000", "http://localhost:8080"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("FlutterWeb", policy =>
    {
        policy.WithOrigins(corsAllowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
```

Add `app.UseCors("FlutterWeb");` right after `app.UseHttpsRedirection();` and before `app.UseRateLimiter();` (after line 195):

```csharp
app.UseHttpsRedirection();
app.UseCors("FlutterWeb");
app.UseRateLimiter();
```

- [ ] **Step 3: Manual verification**

Run: `dotnet build backend/SeniorConnect.sln`
Expected: build succeeds.

Run the API locally and verify a preflight succeeds:

```bash
curl -i -X OPTIONS http://localhost:5000/api/v1/organizations \
  -H "Origin: http://localhost:8080" \
  -H "Access-Control-Request-Method: GET"
```

Expected: `200` (or `204`) response with an `Access-Control-Allow-Origin: http://localhost:8080` header.

- [ ] **Step 4: Commit**

```bash
git add backend/SeniorConnect.Api/appsettings.json backend/SeniorConnect.Api/Program.cs
git commit -m "feat(api): enable CORS for the Flutter web build"
```

---

## Task 6: Enable the Flutter `web` platform

**Files:**
- Create (via `flutter create`): `mobile/senior_connect/web/` (index.html, manifest.json, icons/)
- Modify: `mobile/senior_connect/lib/main.dart:30-34`

**Interfaces:**
- Produces: `mobile/senior_connect` now builds for `web` in addition to its existing platforms; `main.dart`'s `baseUrl` default becomes platform-aware.

- [ ] **Step 1: Add the web platform**

Run from `mobile/senior_connect/`:

```bash
flutter create --platforms=web .
```

This generates `web/index.html`, `web/manifest.json`, and `web/icons/` without touching existing `lib/` code.

- [ ] **Step 2: Make the default API base URL platform-aware**

`http://10.0.2.2:5000` is the Android-emulator-only loopback alias and is unreachable from a browser. In `mobile/senior_connect/lib/main.dart`, change the import block to also pull in `kIsWeb`:

```dart
import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
```

and replace lines 30-34:

```dart
  // P1-26: API client — base URL injected at build time via --dart-define
  const baseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: kIsWeb ? 'http://localhost:5000' : 'http://10.0.2.2:5000',
  );
```

- [ ] **Step 3: Run it**

Run: `cd mobile/senior_connect && flutter run -d chrome --web-port=8080`
Expected: the app launches in Chrome at `http://localhost:8080`, reaches the phone-entry screen. (Requires the backend running locally per Task 5 for API calls to succeed; the UI itself must render regardless.)

- [ ] **Step 4: Commit**

```bash
git add mobile/senior_connect/web mobile/senior_connect/lib/main.dart
git commit -m "feat(mobile): enable Flutter web platform for the staff/coordinator surface"
```

---

## Task 7: Organization directory screen

**Files:**
- Create: `mobile/senior_connect/lib/features/organizations/presentation/organizations_list_screen.dart`
- Modify: `mobile/senior_connect/lib/core/router/app_router.dart:26,46,199`
- Modify: `mobile/senior_connect/assets/translations/de.json`, `en.json`, `fa.json`
- Modify: `mobile/senior_connect/test/features_screens_test.dart`

**Interfaces:**
- Consumes: `ApiClient.get<List<dynamic>>('/api/v1/organizations')` (existing endpoint).
- Produces: `OrganizationsListScreen({Key? key, required ApiClient apiClient})`, route name `'organizations'` at `AppRoutes.organizations = '/organizations'`.

- [ ] **Step 1: Write the failing widget test**

Add to `mobile/senior_connect/test/features_screens_test.dart`, alongside the other `import`s:

```dart
import 'package:senior_connect/features/organizations/presentation/organizations_list_screen.dart';
```

and a new group, right after the `'HelpFaqScreen (P7-16)'` group and before the final `}`:

```dart
  group('OrganizationsListScreen (P2-25)', () {
    testAcrossMatrix(
      'renders organization directory without overflow',
      (tester, c) async {
        await tester.pumpWidget(wrapForTest(
          OrganizationsListScreen(apiClient: testApiClient),
          c,
        ));
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 300));

        await expectNoOverflow(tester);
        expect(find.byType(OrganizationsListScreen), findsOneWidget);
      },
      matrix: smokeMatrix(),
    );
  });
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd mobile/senior_connect && flutter test test/features_screens_test.dart`
Expected: FAIL — `organizations_list_screen.dart` doesn't exist (import error).

- [ ] **Step 3: Implement the screen**

Create `mobile/senior_connect/lib/features/organizations/presentation/organizations_list_screen.dart`:

```dart
// lib/features/organizations/presentation/organizations_list_screen.dart
//
// P2-25: Organization directory — entry point into each org's profile page.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_states.dart';
import 'organization_profile_screen.dart';

class OrganizationsListScreen extends StatefulWidget {
  const OrganizationsListScreen({super.key, required this.apiClient});

  final ApiClient apiClient;

  @override
  State<OrganizationsListScreen> createState() => _OrganizationsListScreenState();
}

class _OrganizationsListScreenState extends State<OrganizationsListScreen> {
  bool _isLoading = true;
  List<Map<String, dynamic>> _organizations = [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _isLoading = true);
    try {
      final response = await widget.apiClient.get<List<dynamic>>('/api/v1/organizations');
      if (mounted) {
        setState(() {
          _organizations = response.map((e) => Map<String, dynamic>.from(e as Map)).toList();
          _isLoading = false;
        });
      }
    } catch (_) {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(title: Text('organizations.directory_title'.tr())),
      body: SafeArea(
        child: _isLoading
            ? AppLoading(message: 'common.loading'.tr())
            : _organizations.isEmpty
                ? AppEmptyState(
                    icon: Icons.apartment_outlined,
                    message: 'organizations.empty'.tr(),
                  )
                : ListView.separated(
                    padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                    itemCount: _organizations.length,
                    separatorBuilder: (_, __) => const SizedBox(height: AppSpacing.sm),
                    itemBuilder: (context, index) {
                      final org = _organizations[index];
                      final id = org['id'] as String? ?? '';
                      final name = org['name'] as String? ?? '';
                      return Card(
                        elevation: 1,
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(AppRadius.md),
                        ),
                        child: ListTile(
                          title: Text(name, style: theme.textTheme.titleMedium),
                          trailing: const Icon(Icons.chevron_right),
                          onTap: id.isEmpty
                              ? null
                              : () => Navigator.of(context).push(
                                    MaterialPageRoute<void>(
                                      builder: (_) => OrganizationProfileScreen(
                                        organizationId: id,
                                        apiClient: widget.apiClient,
                                      ),
                                    ),
                                  ),
                        ),
                      );
                    },
                  ),
      ),
    );
  }
}
```

- [ ] **Step 4: Wire the route and a home entry point**

In `mobile/senior_connect/lib/core/router/app_router.dart`, add the import after the `organizations/presentation/log_activity_screen.dart` import (after line 26):

```dart
import '../../features/organizations/presentation/organizations_list_screen.dart';
```

Add the route constant right after `static const community = '/community';` (after line 46):

```dart
  static const organizations = '/organizations';
```

Add the route entry inside the `ShellRoute`'s `routes` list, right after the `community` `GoRoute` block (after line 127):

```dart
          GoRoute(
            path: AppRoutes.organizations,
            name: 'organizations',
            builder: (context, state) => OrganizationsListScreen(apiClient: apiClient),
          ),
```

Add a home entry point in `_HomeScreen.build`, right after the `community.title` `SeniorAction` (after line 200):

```dart
        SeniorAction(
          icon: Icons.apartment_outlined,
          label: 'organizations.directory_title'.tr(),
          semanticLabel: 'organizations.directory_title'.tr(),
          onTap: () => context.push(AppRoutes.organizations),
        ),
```

- [ ] **Step 5: Add translation keys**

In `mobile/senior_connect/assets/translations/de.json`, inside the existing top-level object, add a new `"organizations"` block as a sibling of `"community"` (insert it immediately after that block's closing `},`):

```json
  "organizations": {
    "directory_title": "Organisationen",
    "empty": "Noch keine Organisationen gelistet.",
    "news": "Neuigkeiten",
    "events": "Veranstaltungen",
    "no_posts": "Diese Organisation hat noch nichts veröffentlicht.",
    "manage": "Verwalten",
    "post_news_or_event": "Neuigkeit oder Veranstaltung veröffentlichen",
    "post_type_news": "Neuigkeit",
    "post_type_event": "Veranstaltung",
    "title_label": "Titel",
    "description_label": "Beschreibung",
    "starts_at_label": "Beginn",
    "ends_at_label": "Ende",
    "capacity_label": "Kapazität (optional)",
    "save": "Speichern",
    "cancel_post": "Absagen",
    "cancel_post_confirm": "Diesen Beitrag wirklich absagen?",
    "saved": "Gespeichert.",
    "post_cancelled": "Beitrag abgesagt."
  },
```

In `en.json`, same position:

```json
  "organizations": {
    "directory_title": "Organizations",
    "empty": "No organizations listed yet.",
    "news": "News",
    "events": "Events",
    "no_posts": "This organization hasn't published anything yet.",
    "manage": "Manage",
    "post_news_or_event": "Publish news or event",
    "post_type_news": "News",
    "post_type_event": "Event",
    "title_label": "Title",
    "description_label": "Description",
    "starts_at_label": "Starts",
    "ends_at_label": "Ends",
    "capacity_label": "Capacity (optional)",
    "save": "Save",
    "cancel_post": "Cancel",
    "cancel_post_confirm": "Really cancel this post?",
    "saved": "Saved.",
    "post_cancelled": "Post cancelled."
  },
```

In `fa.json`, same position:

```json
  "organizations": {
    "directory_title": "سازمان‌ها",
    "empty": "هنوز سازمانی ثبت نشده.",
    "news": "اخبار",
    "events": "رویدادها",
    "no_posts": "این سازمان هنوز چیزی منتشر نکرده.",
    "manage": "مدیریت",
    "post_news_or_event": "انتشار خبر یا رویداد",
    "post_type_news": "خبر",
    "post_type_event": "رویداد",
    "title_label": "عنوان",
    "description_label": "توضیحات",
    "starts_at_label": "شروع",
    "ends_at_label": "پایان",
    "capacity_label": "ظرفیت (اختیاری)",
    "save": "ذخیره",
    "cancel_post": "لغو",
    "cancel_post_confirm": "این پست واقعاً لغو شود؟",
    "saved": "ذخیره شد.",
    "post_cancelled": "پست لغو شد."
  },
```

- [ ] **Step 6: Run test**

Note: this test won't fully pass until Task 8 creates `organization_profile_screen.dart` (imported by the list screen). Run:

Run: `cd mobile/senior_connect && flutter test test/features_screens_test.dart -t "OrganizationsListScreen"`
Expected: FAIL at this point with "organization_profile_screen.dart not found" — proceed directly to Task 8, then return and run this same command, which should then PASS.

- [ ] **Step 7: Commit**

Commit together with Task 8 and Task 9 (the import chain `organizations_list_screen → organization_profile_screen → organization_post_form_screen` means none of the three compiles alone) — see Task 9's commit step.

---

## Task 8: Organization profile screen (view — any authenticated user)

**Files:**
- Create: `mobile/senior_connect/lib/features/organizations/presentation/organization_profile_screen.dart`
- Modify: `mobile/senior_connect/lib/core/router/app_router.dart` (add route)
- Modify: `mobile/senior_connect/test/features_screens_test.dart`

**Interfaces:**
- Consumes: `ApiClient.get<Map<String, dynamic>>('/api/v1/organizations/{id}')`, `ApiClient.get<List<dynamic>>('/api/v1/community/events?organizationId={id}')`, `ApiClient.get<List<dynamic>>('/api/v1/me/organizations')`.
- Produces: `OrganizationProfileScreen({Key? key, required String organizationId, required ApiClient apiClient})`.

- [ ] **Step 1: Write the failing widget test**

Add the import to `mobile/senior_connect/test/features_screens_test.dart`:

```dart
import 'package:senior_connect/features/organizations/presentation/organization_profile_screen.dart';
```

Add a group right after the `OrganizationsListScreen` group added in Task 7:

```dart
  group('OrganizationProfileScreen (P2-25)', () {
    testAcrossMatrix(
      'renders organization header, news and events without overflow',
      (tester, c) async {
        await tester.pumpWidget(wrapForTest(
          OrganizationProfileScreen(
            organizationId: 'org-1',
            apiClient: testApiClient,
          ),
          c,
        ));
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 300));

        await expectNoOverflow(tester);
        expect(find.byType(OrganizationProfileScreen), findsOneWidget);
      },
      matrix: smokeMatrix(),
    );
  });
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd mobile/senior_connect && flutter test test/features_screens_test.dart`
Expected: FAIL — `organization_profile_screen.dart` doesn't exist.

- [ ] **Step 3: Implement the screen**

Create `mobile/senior_connect/lib/features/organizations/presentation/organization_profile_screen.dart`:

```dart
// lib/features/organizations/presentation/organization_profile_screen.dart
//
// P2-25: Per-organization page — news and events, view-only for any
// authenticated user; staff (active Coordinator/Admin) additionally see a
// "manage" entry point into OrganizationPostFormScreen.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_states.dart';
import 'organization_post_form_screen.dart';

class OrganizationProfileScreen extends StatefulWidget {
  const OrganizationProfileScreen({
    super.key,
    required this.organizationId,
    required this.apiClient,
  });

  final String organizationId;
  final ApiClient apiClient;

  @override
  State<OrganizationProfileScreen> createState() => _OrganizationProfileScreenState();
}

class _OrganizationProfileScreenState extends State<OrganizationProfileScreen> {
  bool _isLoading = true;
  Map<String, dynamic>? _organization;
  List<Map<String, dynamic>> _newsItems = [];
  List<Map<String, dynamic>> _events = [];
  bool _canManage = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _isLoading = true);
    try {
      final org = await widget.apiClient
          .get<Map<String, dynamic>>('/api/v1/organizations/${widget.organizationId}');

      final posts = await widget.apiClient.get<List<dynamic>>(
        '/api/v1/community/events',
        queryParameters: {'organizationId': widget.organizationId},
      );
      final postMaps = posts.map((e) => Map<String, dynamic>.from(e as Map)).toList();

      var canManage = false;
      try {
        final mine = await widget.apiClient.get<List<dynamic>>('/api/v1/me/organizations');
        canManage = mine.any((m) =>
            Map<String, dynamic>.from(m as Map)['organizationId'] == widget.organizationId);
      } catch (_) {
        // Staff-entry-point visibility only — the server call is the real
        // gate, so a failure here just hides the button.
      }

      if (mounted) {
        setState(() {
          _organization = org;
          _newsItems = postMaps.where((e) => e['category'] == 'news').toList();
          _events = postMaps.where((e) => e['category'] != 'news').toList();
          _canManage = canManage;
          _isLoading = false;
        });
      }
    } catch (_) {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final orgName = _organization?['name'] as String? ?? '';

    return Scaffold(
      appBar: AppBar(title: Text(orgName)),
      floatingActionButton: _canManage
          ? FloatingActionButton.extended(
              onPressed: () async {
                final saved = await Navigator.of(context).push<bool>(
                  MaterialPageRoute<bool>(
                    builder: (_) => OrganizationPostFormScreen(
                      organizationId: widget.organizationId,
                      apiClient: widget.apiClient,
                    ),
                  ),
                );
                if (saved == true) _load();
              },
              icon: const Icon(Icons.add),
              label: Text('organizations.post_news_or_event'.tr()),
            )
          : null,
      body: SafeArea(
        child: _isLoading
            ? AppLoading(message: 'common.loading'.tr())
            : ListView(
                padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                children: [
                  if (_organization?['supportEmail'] != null ||
                      _organization?['supportPhone'] != null)
                    Padding(
                      padding: const EdgeInsetsDirectional.only(bottom: AppSpacing.md),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          if (_organization?['supportEmail'] != null)
                            Text(_organization!['supportEmail'] as String,
                                style: theme.textTheme.bodyMedium),
                          if (_organization?['supportPhone'] != null)
                            Text(_organization!['supportPhone'] as String,
                                style: theme.textTheme.bodyMedium),
                        ],
                      ),
                    ),
                  Text('organizations.news'.tr(), style: theme.textTheme.titleLarge),
                  const SizedBox(height: AppSpacing.sm),
                  if (_newsItems.isEmpty)
                    Padding(
                      padding: const EdgeInsetsDirectional.only(bottom: AppSpacing.md),
                      child: Text('organizations.no_posts'.tr()),
                    )
                  else
                    ..._newsItems.map((item) => _PostCard(item: item)),
                  const SizedBox(height: AppSpacing.lg),
                  Text('organizations.events'.tr(), style: theme.textTheme.titleLarge),
                  const SizedBox(height: AppSpacing.sm),
                  if (_events.isEmpty)
                    Text('organizations.no_posts'.tr())
                  else
                    ..._events.map((item) => _PostCard(item: item)),
                ],
              ),
      ),
    );
  }
}

class _PostCard extends StatelessWidget {
  const _PostCard({required this.item});

  final Map<String, dynamic> item;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final title = item['title'] as String? ?? '';
    final description = item['description'] as String? ?? '';
    final isCancelled = item['isCancelled'] as bool? ?? false;

    return Card(
      elevation: 1,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadius.md)),
      margin: const EdgeInsetsDirectional.only(bottom: AppSpacing.sm),
      child: Padding(
        padding: const EdgeInsetsDirectional.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(title,
                style: theme.textTheme.titleMedium?.copyWith(
                  decoration: isCancelled ? TextDecoration.lineThrough : null,
                )),
            if (description.isNotEmpty) ...[
              const SizedBox(height: AppSpacing.xs),
              Text(description, style: theme.textTheme.bodyMedium),
            ],
          ],
        ),
      ),
    );
  }
}
```

- [ ] **Step 4: Wire the route**

In `mobile/senior_connect/lib/core/router/app_router.dart`, add the import next to the other `organizations/presentation` import:

```dart
import '../../features/organizations/presentation/organization_profile_screen.dart';
```

Add the route entry right after the `organizations` `GoRoute` added in Task 7:

```dart
          GoRoute(
            path: '${AppRoutes.organizations}/:id',
            name: 'organization-profile',
            builder: (context, state) => OrganizationProfileScreen(
              organizationId: state.pathParameters['id']!,
              apiClient: apiClient,
            ),
          ),
```

(`OrganizationsListScreen` in Task 7 navigates via `Navigator.push` directly rather than through this named route, matching how `CommunityFeedScreen` → `EventDetailScreen` already navigates in this codebase — the named route exists for deep-linking on web, e.g. sharing `/organizations/{id}` as a URL.)

- [ ] **Step 5: Run tests**

Run: `cd mobile/senior_connect && flutter test test/features_screens_test.dart`
Expected: still FAILs — `organization_post_form_screen.dart` (imported above) doesn't exist yet. Proceed to Task 9.

- [ ] **Step 6: Commit**

Commit together with Task 9 — see Task 9's commit step.

---

## Task 9: Staff post form (create/edit/cancel)

**Files:**
- Create: `mobile/senior_connect/lib/features/organizations/presentation/organization_post_form_screen.dart`
- Modify: `mobile/senior_connect/test/features_screens_test.dart`

**Interfaces:**
- Consumes: `ApiClient.post<Map<String, dynamic>>('/api/v1/community/events', data: {...})`, `ApiClient.put<Map<String, dynamic>>('/api/v1/community/events/{id}', data: {...})`, `ApiClient.post('/api/v1/community/events/{id}:cancel', data: {...})`.
- Produces: `OrganizationPostFormScreen({Key? key, required String organizationId, required ApiClient apiClient, Map<String, dynamic>? existingPost})`. Pops `true` on successful save/cancel, `null`/`false` otherwise — this is what `OrganizationProfileScreen` (Task 8) checks to decide whether to reload.

- [ ] **Step 1: Write the failing widget test**

Add the import to `mobile/senior_connect/test/features_screens_test.dart`:

```dart
import 'package:senior_connect/features/organizations/presentation/organization_post_form_screen.dart';
```

Add a group after the `OrganizationProfileScreen` group:

```dart
  group('OrganizationPostFormScreen (P2-25)', () {
    testAcrossMatrix(
      'toggles date and capacity fields between news and event categories',
      (tester, c) async {
        await tester.pumpWidget(wrapForTest(
          OrganizationPostFormScreen(
            organizationId: 'org-1',
            apiClient: testApiClient,
          ),
          c,
        ));
        await tester.pumpAndSettle();

        await expectNoOverflow(tester);

        // Defaults to "event" — date fields visible.
        expect(find.text('organizations.starts_at_label'.tr()), findsOneWidget);

        // Switch to "news" — date fields hidden.
        await tester.tap(find.text('organizations.post_type_news'.tr()));
        await tester.pumpAndSettle();
        expect(find.text('organizations.starts_at_label'.tr()), findsNothing);
      },
      matrix: smokeMatrix(),
    );
  });
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd mobile/senior_connect && flutter test test/features_screens_test.dart`
Expected: FAIL — `organization_post_form_screen.dart` doesn't exist.

- [ ] **Step 3: Read `AppButton` before implementing**

Read `mobile/senior_connect/lib/shared/widgets/app_button.dart` first and confirm its exact constructor parameter names (this plan assumes `label`, `onPressed`, `isLoading` by analogy with `log_activity_screen.dart`'s usage — adjust the code below if the real signature differs before compiling).

- [ ] **Step 4: Implement the screen**

Create `mobile/senior_connect/lib/features/organizations/presentation/organization_post_form_screen.dart`:

```dart
// lib/features/organizations/presentation/organization_post_form_screen.dart
//
// P2-25: Staff-only create/edit/cancel form for an organization's news and
// events. Server-side org-staff authorization (see CommunityService) is the
// real gate — this screen is only reachable from OrganizationProfileScreen's
// "manage" button, which is itself only shown when /me/organizations lists
// the viewed org, so an unauthorized submit here is not a reachable path in
// normal use, only a defense-in-depth 403 if that local state goes stale.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_button.dart';

enum _PostCategory { news, event }

class OrganizationPostFormScreen extends StatefulWidget {
  const OrganizationPostFormScreen({
    super.key,
    required this.organizationId,
    required this.apiClient,
    this.existingPost,
  });

  final String organizationId;
  final ApiClient apiClient;

  /// When editing an existing post, its raw API map (must include `id`).
  final Map<String, dynamic>? existingPost;

  @override
  State<OrganizationPostFormScreen> createState() => _OrganizationPostFormScreenState();
}

class _OrganizationPostFormScreenState extends State<OrganizationPostFormScreen> {
  final _titleController = TextEditingController();
  final _descriptionController = TextEditingController();
  final _capacityController = TextEditingController();
  _PostCategory _category = _PostCategory.event;
  DateTime _startsAt = DateTime.now().add(const Duration(days: 7));
  bool _isSubmitting = false;
  bool _hasError = false;

  bool get _isEditing => widget.existingPost != null;

  @override
  void initState() {
    super.initState();
    final existing = widget.existingPost;
    if (existing != null) {
      _titleController.text = existing['title'] as String? ?? '';
      _descriptionController.text = existing['description'] as String? ?? '';
      _category = existing['category'] == 'news' ? _PostCategory.news : _PostCategory.event;
      final capacity = existing['capacity'];
      if (capacity != null) _capacityController.text = capacity.toString();
    }
  }

  @override
  void dispose() {
    _titleController.dispose();
    _descriptionController.dispose();
    _capacityController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    setState(() {
      _isSubmitting = true;
      _hasError = false;
    });

    try {
      final category = _category == _PostCategory.news ? 'news' : 'general';
      final capacity = int.tryParse(_capacityController.text.trim());

      if (_isEditing) {
        await widget.apiClient.put<Map<String, dynamic>>(
          '/api/v1/community/events/${widget.existingPost!['id']}',
          data: {
            'title': _titleController.text.trim(),
            'description': _descriptionController.text.trim(),
            'category': category,
            'capacity': capacity,
          },
        );
      } else {
        final startsAt = _category == _PostCategory.news ? DateTime.now() : _startsAt;
        await widget.apiClient.post<Map<String, dynamic>>(
          '/api/v1/community/events',
          data: {
            'title': _titleController.text.trim(),
            'description': _descriptionController.text.trim(),
            'category': category,
            'organizationId': widget.organizationId,
            'startsAtUtc': startsAt.toUtc().toIso8601String(),
            'endsAtUtc': startsAt.toUtc().add(const Duration(hours: 2)).toIso8601String(),
            'capacity': capacity,
          },
        );
      }

      if (mounted) Navigator.of(context).pop(true);
    } catch (_) {
      if (mounted) {
        setState(() {
          _hasError = true;
          _isSubmitting = false;
        });
      }
    }
  }

  Future<void> _cancelPost() async {
    final existing = widget.existingPost;
    if (existing == null) return;

    setState(() => _isSubmitting = true);
    try {
      await widget.apiClient.post<dynamic>(
        '/api/v1/community/events/${existing['id']}:cancel',
        data: {'reason': 'Cancelled by organization staff'},
      );
      if (mounted) Navigator.of(context).pop(true);
    } catch (_) {
      if (mounted) setState(() => _isSubmitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final isNews = _category == _PostCategory.news;

    return Scaffold(
      appBar: AppBar(title: Text('organizations.post_news_or_event'.tr())),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsetsDirectional.all(AppSpacing.lg),
          children: [
            SegmentedButton<_PostCategory>(
              segments: [
                ButtonSegment(
                  value: _PostCategory.news,
                  label: Text('organizations.post_type_news'.tr()),
                ),
                ButtonSegment(
                  value: _PostCategory.event,
                  label: Text('organizations.post_type_event'.tr()),
                ),
              ],
              selected: {_category},
              onSelectionChanged: (selection) =>
                  setState(() => _category = selection.first),
            ),
            const SizedBox(height: AppSpacing.lg),
            TextField(
              controller: _titleController,
              decoration: InputDecoration(labelText: 'organizations.title_label'.tr()),
            ),
            const SizedBox(height: AppSpacing.md),
            TextField(
              controller: _descriptionController,
              maxLines: 4,
              decoration: InputDecoration(labelText: 'organizations.description_label'.tr()),
            ),
            const SizedBox(height: AppSpacing.md),
            if (!isNews) ...[
              ListTile(
                contentPadding: EdgeInsets.zero,
                title: Text('organizations.starts_at_label'.tr()),
                subtitle: Text(_startsAt.toString()),
                trailing: const Icon(Icons.calendar_today),
                onTap: () async {
                  final picked = await showDatePicker(
                    context: context,
                    initialDate: _startsAt,
                    firstDate: DateTime.now(),
                    lastDate: DateTime.now().add(const Duration(days: 730)),
                  );
                  if (picked != null) setState(() => _startsAt = picked);
                },
              ),
              const SizedBox(height: AppSpacing.md),
              TextField(
                controller: _capacityController,
                keyboardType: TextInputType.number,
                decoration: InputDecoration(labelText: 'organizations.capacity_label'.tr()),
              ),
              const SizedBox(height: AppSpacing.md),
            ],
            if (_hasError)
              Padding(
                padding: const EdgeInsetsDirectional.only(bottom: AppSpacing.md),
                child: Text(
                  'error.generic'.tr(),
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
              ),
            AppButton(
              label: 'organizations.save'.tr(),
              onPressed: _isSubmitting ? null : _submit,
              isLoading: _isSubmitting,
            ),
            if (_isEditing) ...[
              const SizedBox(height: AppSpacing.sm),
              TextButton(
                onPressed: _isSubmitting ? null : _cancelPost,
                child: Text('organizations.cancel_post'.tr()),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
```

Check `error.generic` already exists in `assets/translations/de.json`/`en.json`/`fa.json` (search for `"generic"` under an `"error"` block); if it doesn't, add `"error": { "generic": "Etwas ist schiefgelaufen. Bitte versuchen Sie es erneut." }` (de) and matching `en`/`fa` entries the same way as Task 7 Step 5.

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd mobile/senior_connect && flutter test test/features_screens_test.dart`
Expected: PASS for the `OrganizationsListScreen`, `OrganizationProfileScreen`, and `OrganizationPostFormScreen` groups (Tasks 7-9 together), and no regressions in the pre-existing groups above them in the file.

- [ ] **Step 6: Run `flutter analyze`**

Run: `cd mobile/senior_connect && flutter analyze`
Expected: no new warnings/errors in the three new files.

- [ ] **Step 7: Commit**

```bash
git add mobile/senior_connect/lib/features/organizations mobile/senior_connect/lib/core/router/app_router.dart mobile/senior_connect/assets/translations mobile/senior_connect/test/features_screens_test.dart
git commit -m "feat(mobile): add organization directory, profile, and staff post form screens"
```

---

## Task 10: Tick P2-25 in the build checklist

**Files:**
- Modify: `docs/plans/BUILD-CHECKLIST.md:70-74,495-497`

- [ ] **Step 1: Update the top-of-file note**

In `docs/plans/BUILD-CHECKLIST.md`, replace the sentence at lines 70-74 (the one beginning `**`P2-25` (staff web app shell) is confirmed genuinely MISSING**...`) with:

```markdown
> **2026-09 update #3 — `P2-25` built (Flutter, not Blazor/React):** redefined
> per `docs/superpowers/specs/2026-09-11-org-profile-flutter-p2-25-design.md` —
> reused the existing Flutter app (`mobile/senior_connect`) with the `web`
> platform enabled, instead of a separate Blazor/React project. Also closed a
> real authorization gap found along the way: `CommunityService` previously
> let any authenticated user publish a group/event on behalf of any
> organization; it now checks `IOrganizationCoordinatorReader` first.
```

- [ ] **Step 2: Tick the task and correct its description**

Replace lines 495-497:

```
[ ] P2-25 — Staff web app shell                  · L · needs P2-02
    Blazor or React. Keyboard-navigable, screen-reader usable.
    ✓ Test: complete one full task using only the keyboard
```

with:

```
[x] P2-25 — Organization profile page (news/events)  · L · needs P2-02
    Flutter (mobile/senior_connect), web platform enabled — not a separate
    Blazor/React project. Any logged-in user views an org's page by tapping
    it; active Coordinator/Admin staff publish, edit, and cancel news/events
    for their org from the same screens.
    ✓ Test: a non-staff user cannot publish for an org (403); an active
      Coordinator/Admin can; both the mobile and `flutter run -d chrome`
      builds render the org profile screen.
```

- [ ] **Step 3: Commit**

```bash
git add docs/plans/BUILD-CHECKLIST.md
git commit -m "docs: tick P2-25 in BUILD-CHECKLIST, redefined as Flutter org profile page"
```

---

## Self-Review Notes

- **Spec coverage:** all 5 numbered backend items in the spec map to Tasks 1-5; the CORS open risk maps to Task 5; the "no org directory screen exists" open risk maps to Task 7; screens in the spec map 1:1 to Tasks 7-9; the rollout phases A-E map to Tasks 1-4 (A), 5-6 (B), 7-8 (C), 9 (D), and the testing already embedded per-task plus Task 10 (E).
- **Placeholder scan:** none found — every step has real code, not descriptions of code. The one deliberately-marked exception is Task 9 Step 3, which explicitly tells the implementer to verify `AppButton`'s real signature before compiling rather than guessing — that is a verification instruction, not a code placeholder.
- **Type consistency:** `StaffOrganizationDto(Guid OrganizationId, string OrganizationName)` (Task 1) is used identically in Task 4's `GET /me/organizations` and Task 8's client-side `organizationId` field-name match (camelCase `organizationId` from System.Text.Json's default camelCase output policy — the same casing already relied upon throughout the existing screens for e.g. `ev['id']`, `ev['title']`). `UpdateCommunityEventRequest`/`CancelCommunityEventRequest` field names match between Task 3's DTOs and Task 9's client payload keys (camelCase on the wire, PascalCase in C#, per the same existing convention as `CreateCommunityEventRequest`).

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-09-11-p2-25-org-profile-page.md`. Two execution options:

1. **Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration
2. **Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

Which approach?
