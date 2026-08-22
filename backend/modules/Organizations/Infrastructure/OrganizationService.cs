using Microsoft.EntityFrameworkCore;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Organizations.Application;
using SeniorConnect.Modules.Organizations.Domain;

namespace SeniorConnect.Modules.Organizations.Infrastructure;

public sealed class OrganizationService : IOrganizationService
{
    private readonly IOrganizationsDbContext _db;

    public OrganizationService(IOrganizationsDbContext db)
    {
        _db = db;
    }

    public async Task<Result<OrganizationDto>> CreateOrganizationAsync(
        CreateOrganizationRequest request,
        Guid? createdBy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("Organization name is required.");
        }

        var org = Organization.Create(
            name: request.Name.Trim(),
            type: request.Type,
            legalName: request.LegalName?.Trim(),
            supportEmail: request.SupportEmail?.Trim().ToLowerInvariant(),
            supportPhone: request.SupportPhone?.Trim(),
            createdBy: createdBy);

        _db.Organizations.Add(org);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<OrganizationDto>.Success(MapOrg(org));
    }

    public async Task<Result<OrganizationDto>> GetOrganizationByIdAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var org = await _db.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId && !o.IsDeleted, cancellationToken);

        if (org is null)
        {
            return Error.NotFound("Organization");
        }

        return Result<OrganizationDto>.Success(MapOrg(org));
    }

    public async Task<Result<IReadOnlyList<OrganizationDto>>> ListOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        var orgs = await _db.Organizations
            .Where(o => !o.IsDeleted && o.Status == OrganizationStatus.Active)
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);

        var dtos = orgs.Select(MapOrg).ToList();
        return Result<IReadOnlyList<OrganizationDto>>.Success(dtos);
    }

    public async Task<Result<BranchDto>> CreateBranchAsync(
        Guid organizationId,
        CreateBranchRequest request,
        Guid? createdBy,
        CancellationToken cancellationToken = default)
    {
        var org = await _db.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId && !o.IsDeleted, cancellationToken);

        if (org is null)
        {
            return Error.NotFound("Organization");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("Branch name is required.");
        }

        var branch = OrganizationBranch.Create(
            organizationId: organizationId,
            name: request.Name.Trim(),
            address: request.Address?.Trim(),
            postalCode: request.PostalCode?.Trim(),
            city: request.City?.Trim(),
            latitude: request.Latitude,
            longitude: request.Longitude,
            createdBy: createdBy);

        _db.OrganizationBranches.Add(branch);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<BranchDto>.Success(MapBranch(branch));
    }

    public async Task<Result<IReadOnlyList<BranchDto>>> GetBranchesAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var branches = await _db.OrganizationBranches
            .Where(b => b.OrganizationId == organizationId && b.IsActive)
            .OrderBy(b => b.Name)
            .ToListAsync(cancellationToken);

        var dtos = branches.Select(MapBranch).ToList();
        return Result<IReadOnlyList<BranchDto>>.Success(dtos);
    }

    public async Task<Result<MembershipDto>> AddMembershipAsync(
        Guid organizationId,
        AddMembershipRequest request,
        Guid? createdBy,
        CancellationToken cancellationToken = default)
    {
        var org = await _db.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId && !o.IsDeleted, cancellationToken);

        if (org is null)
        {
            return Error.NotFound("Organization");
        }

        var existing = await _db.OrganizationMemberships
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == request.UserId, cancellationToken);

        if (existing is not null)
        {
            return new Error("MEMBERSHIP_EXISTS", "User already has a membership in this organization.", ErrorKind.Conflict);
        }

        var membership = OrganizationMembership.Create(
            organizationId: organizationId,
            userId: request.UserId,
            role: request.Role,
            branchId: request.BranchId,
            createdBy: createdBy);

        membership.Activate();

        _db.OrganizationMemberships.Add(membership);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<MembershipDto>.Success(MapMembership(membership));
    }

    public async Task<Result<MembershipDto>> UpdateMembershipAsync(
        Guid organizationId,
        Guid membershipId,
        UpdateMembershipRequest request,
        CancellationToken cancellationToken = default)
    {
        var membership = await _db.OrganizationMemberships
            .FirstOrDefaultAsync(m => m.Id == membershipId && m.OrganizationId == organizationId, cancellationToken);

        if (membership is null)
        {
            return Error.NotFound("Membership");
        }

        membership.ChangeRole(request.Role);
        if (request.Status == MembershipStatus.Active) membership.Activate();
        else if (request.Status == MembershipStatus.Suspended) membership.Suspend();
        else if (request.Status == MembershipStatus.Left) membership.Leave();

        await _db.SaveChangesAsync(cancellationToken);

        return Result<MembershipDto>.Success(MapMembership(membership));
    }

    public async Task<Result<IReadOnlyList<MembershipDto>>> GetMembershipsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var memberships = await _db.OrganizationMemberships
            .Where(m => m.OrganizationId == organizationId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var dtos = memberships.Select(MapMembership).ToList();
        return Result<IReadOnlyList<MembershipDto>>.Success(dtos);
    }

    public async Task<Result<PolicyDto>> SetPolicyAsync(
        Guid organizationId,
        string key,
        string valueJson,
        Guid? updatedBy,
        CancellationToken cancellationToken = default)
    {
        var policy = await _db.OrganizationPolicies
            .FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.PolicyKey == key, cancellationToken);

        if (policy is null)
        {
            policy = OrganizationPolicy.Create(organizationId, key, valueJson, updatedBy);
            _db.OrganizationPolicies.Add(policy);
        }
        else
        {
            policy.UpdateValue(valueJson, updatedBy);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result<PolicyDto>.Success(new PolicyDto(policy.PolicyKey, policy.PolicyValueJson, policy.UpdatedAtUtc));
    }

    public async Task<Result<PolicyDto>> GetPolicyAsync(
        Guid organizationId,
        string key,
        CancellationToken cancellationToken = default)
    {
        var policy = await _db.OrganizationPolicies
            .FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.PolicyKey == key, cancellationToken);

        if (policy is null)
        {
            return Error.NotFound($"Policy '{key}'");
        }

        return Result<PolicyDto>.Success(new PolicyDto(policy.PolicyKey, policy.PolicyValueJson, policy.UpdatedAtUtc));
    }

    public async Task<Result<IReadOnlyList<PolicyDto>>> GetPoliciesAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var policies = await _db.OrganizationPolicies
            .Where(p => p.OrganizationId == organizationId)
            .OrderBy(p => p.PolicyKey)
            .ToListAsync(cancellationToken);

        var dtos = policies.Select(p => new PolicyDto(p.PolicyKey, p.PolicyValueJson, p.UpdatedAtUtc)).ToList();
        return Result<IReadOnlyList<PolicyDto>>.Success(dtos);
    }

    private static OrganizationDto MapOrg(Organization o) => new(
        Id: o.Id,
        Name: o.Name,
        LegalName: o.LegalName,
        Type: o.Type,
        Status: o.Status,
        SupportEmail: o.SupportEmail,
        SupportPhone: o.SupportPhone,
        CreatedAtUtc: o.CreatedAtUtc);

    private static BranchDto MapBranch(OrganizationBranch b) => new(
        Id: b.Id,
        OrganizationId: b.OrganizationId!.Value,
        Name: b.Name,
        Address: b.Address,
        PostalCode: b.PostalCode,
        City: b.City,
        Latitude: b.Latitude,
        Longitude: b.Longitude,
        IsActive: b.IsActive);

    private static MembershipDto MapMembership(OrganizationMembership m) => new(
        Id: m.Id,
        OrganizationId: m.OrganizationId!.Value,
        BranchId: m.BranchId,
        UserId: m.UserId,
        Role: m.Role,
        Status: m.Status,
        JoinedAtUtc: m.JoinedAtUtc,
        CreatedAtUtc: m.CreatedAtUtc);
}
