using SeniorConnect.Modules.Organizations.Domain;

namespace SeniorConnect.Modules.Organizations.Application;

public sealed record OrganizationDto(
    Guid Id,
    string Name,
    string? LegalName,
    OrganizationType Type,
    OrganizationStatus Status,
    string? SupportEmail,
    string? SupportPhone,
    DateTimeOffset CreatedAtUtc);

public sealed record CreateOrganizationRequest(
    string Name,
    OrganizationType Type,
    string? LegalName = null,
    string? SupportEmail = null,
    string? SupportPhone = null);

public sealed record UpdateOrganizationRequest(
    string Name,
    OrganizationType Type,
    string? LegalName = null,
    string? SupportEmail = null,
    string? SupportPhone = null,
    OrganizationStatus Status = OrganizationStatus.Active);

public sealed record BranchDto(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Address,
    string? PostalCode,
    string? City,
    double? Latitude,
    double? Longitude,
    bool IsActive);

public sealed record CreateBranchRequest(
    string Name,
    string? Address = null,
    string? PostalCode = null,
    string? City = null,
    double? Latitude = null,
    double? Longitude = null);

public sealed record MembershipDto(
    Guid Id,
    Guid OrganizationId,
    Guid? BranchId,
    Guid UserId,
    MembershipRole Role,
    MembershipStatus Status,
    DateTimeOffset? JoinedAtUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record AddMembershipRequest(
    Guid UserId,
    MembershipRole Role,
    Guid? BranchId = null);

public sealed record UpdateMembershipRequest(
    MembershipRole Role,
    MembershipStatus Status);

public sealed record PolicyDto(
    string PolicyKey,
    string PolicyValueJson,
    DateTimeOffset UpdatedAtUtc);

public sealed record SetPolicyRequest(
    string PolicyValueJson);
