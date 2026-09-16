using SeniorConnect.Modules.Family.Domain;
using SeniorConnect.Modules.Identity.Contracts;

namespace SeniorConnect.Modules.Family.Application;

public sealed record FamilyPermissionDto(
    Guid Id,
    PermissionType PermissionType,
    bool IsGranted,
    DateTimeOffset UpdatedAtUtc);

public sealed record FamilyRelationshipDto(
    Guid Id,
    Guid SeniorUserId,
    Guid CaregiverUserId,
    RelationshipType RelationshipType,
    RelationshipStatus Status,
    string? InvitationCode,
    DateTimeOffset? InvitationExpiresAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ConfirmedAtUtc,
    IReadOnlyList<FamilyPermissionDto> Permissions,
    string? SeniorDisplayName = null,
    string? SeniorPhoneNumber = null);

public sealed record InviteCaregiverRequest(
    Guid SeniorUserId,
    RelationshipType RelationshipType,
    int ExpiresInDays = 7);

public sealed record AcceptInvitationRequest(
    string InvitationCode);

public sealed record UpdatePermissionsRequest(
    IReadOnlyDictionary<PermissionType, bool> Permissions);

public sealed record CreateSeniorWithZugangskarteRequest(
    string DisplayName,
    string? PhoneNumber,
    string? PostalCode,
    string? City,
    RelationshipType RelationshipType,
    int ValidForDays = 7);

public sealed record ZugangskarteDto(
    Guid Id,
    Guid SeniorUserId,
    Guid CreatedByCaregiverUserId,
    string PairingCode,
    string QrPayload,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    bool IsClaimed,
    Guid? ClaimedByUserId = null);

public sealed record ClaimZugangskarteRequest(
    string PairingCode,
    string? QrToken = null);

public sealed record ClaimZugangskarteResponse(
    ZugangskarteDto Zugangskarte,
    UserSessionDto? Session);

public sealed record SeniorAccessLogDto(
    Guid Id,
    Guid SeniorUserId,
    Guid AccessedByUserId,
    string AccessedByUserName,
    string Action,
    string ResourceAccessed,
    string PlainLanguageDescription,
    DateTimeOffset TimestampUtc);

public sealed record TrustedContactDto(
    Guid Id,
    Guid SeniorUserId,
    string Name,
    string PhoneNumber,
    string? Email,
    string Relationship,
    bool IsPrimaryEmergency,
    bool NotifyOnSafetyAlert,
    DateTimeOffset CreatedAtUtc);

public sealed record CreateTrustedContactRequest(
    Guid SeniorUserId,
    string Name,
    string PhoneNumber,
    string Relationship,
    string? Email = null,
    bool IsPrimaryEmergency = false,
    bool NotifyOnSafetyAlert = true);

public sealed record UpdateTrustedContactRequest(
    string Name,
    string PhoneNumber,
    string Relationship,
    string? Email = null,
    bool IsPrimaryEmergency = false,
    bool NotifyOnSafetyAlert = true);

public sealed record SafetyAlertDto(
    Guid Id,
    Guid SeniorUserId,
    Guid TriggeredByUserId,
    SafetyAlertCategory Category,
    SafetyAlertStatus Status,
    string Details,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? AcknowledgedAtUtc,
    Guid? AcknowledgedByUserId,
    DateTimeOffset? ResolvedAtUtc,
    Guid? ResolvedByUserId,
    string? ResolutionNotes);

public sealed record TriggerSafetyAlertRequest(
    Guid SeniorUserId,
    SafetyAlertCategory Category,
    string Details);

public sealed record ResolveSafetyAlertRequest(
    string ResolutionNotes);
