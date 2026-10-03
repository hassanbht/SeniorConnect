using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Organizations.Application;
using SeniorConnect.Modules.Organizations.Contracts;
using SeniorConnect.Modules.Organizations.Domain;

namespace SeniorConnect.Modules.Organizations.Infrastructure;

public sealed class OrganizationAccessGuard : IOrganizationAccessGuard, IOrganizationDirectory
{
    // authorization.md §3 capability names, surfaced in the 403 body.
    public const string StaffCapability = "ManageOrganizationVolunteers";
    public const string AdminCapability = "ManageOrganizationSettings";

    private readonly IOrganizationsDbContext _db;

    public OrganizationAccessGuard(IOrganizationsDbContext db)
    {
        _db = db;
    }

    public Task<Result> EnsureStaffAsync(Guid userId, Guid organizationId, CancellationToken ct = default) =>
        EnsureRoleAsync(userId, organizationId, StaffCapability, ct, MembershipRole.Coordinator, MembershipRole.Admin);

    public Task<Result> EnsureAdminAsync(Guid userId, Guid organizationId, CancellationToken ct = default) =>
        EnsureRoleAsync(userId, organizationId, AdminCapability, ct, MembershipRole.Admin);

    private async Task<Result> EnsureRoleAsync(
        Guid userId, Guid organizationId, string capability, CancellationToken ct, params MembershipRole[] roles)
    {
        var activeRoles = await _db.OrganizationMemberships
            .AsNoTracking()
            .Where(m => m.OrganizationId == organizationId
                     && m.UserId == userId
                     && m.Status == MembershipStatus.Active)
            .Select(m => m.Role)
            .ToListAsync(ct);

        // Not a member → the org's internals are invisible: 404, never 403.
        if (activeRoles.Count == 0)
        {
            return Error.NotFound("Organization");
        }

        return activeRoles.Any(roles.Contains)
            ? Result.Success()
            : Error.CapabilityMissing(capability);
    }

    public async Task<IReadOnlyList<Guid>> GetActiveVolunteerUserIdsAsync(Guid organizationId, CancellationToken ct = default)
    {
        return await _db.OrganizationMemberships
            .AsNoTracking()
            .Where(m => m.OrganizationId == organizationId
                     && m.Role == MembershipRole.Volunteer
                     && m.Status == MembershipStatus.Active)
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<RosterThresholds> GetRosterThresholdsAsync(Guid organizationId, CancellationToken ct = default)
    {
        var json = await _db.OrganizationPolicies
            .AsNoTracking()
            .Where(p => p.OrganizationId == organizationId && p.PolicyKey == RosterThresholds.PolicyKey)
            .Select(p => p.PolicyValueJson)
            .FirstOrDefaultAsync(ct);

        return ParseThresholds(json);
    }

    public static RosterThresholds ParseThresholds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return RosterThresholds.Default;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var active = ReadPositiveInt(root, "activeDays") ?? RosterThresholds.Default.ActiveDays;
            var dormant = ReadPositiveInt(root, "dormantDays") ?? RosterThresholds.Default.DormantDays;
            // An inconsistent policy must not silently invert the statuses.
            return dormant > active ? new RosterThresholds(active, dormant) : RosterThresholds.Default;
        }
        catch (JsonException)
        {
            return RosterThresholds.Default;
        }
    }

    private static int? ReadPositiveInt(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var parsed)
        && parsed > 0
            ? parsed
            : null;

    public async Task<string?> GetOrganizationNameAsync(Guid organizationId, CancellationToken ct = default)
    {
        return await _db.Organizations
            .AsNoTracking()
            .Where(o => o.Id == organizationId && !o.IsDeleted)
            .Select(o => o.Name)
            .FirstOrDefaultAsync(ct);
    }
}
