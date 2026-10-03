// lib/features/community/domain/community_group.dart
//
// Mirrors CommunityGroupDto (backend/modules/Community/Application/Dtos.cs).

import 'group_enums.dart';

class CommunityGroup {
  const CommunityGroup({
    required this.id,
    required this.creatorUserId,
    required this.creatorType,
    required this.organizationId,
    required this.name,
    required this.description,
    required this.type,
    required this.category,
    required this.visibility,
    required this.status,
    required this.city,
    required this.region,
    required this.postalCode,
    required this.latitude,
    required this.longitude,
    required this.primaryLanguageCode,
    required this.coverImageUrl,
    required this.rules,
    required this.maxMembers,
    required this.memberCount,
    required this.verificationStatus,
    required this.createdAtUtc,
    required this.myMembershipStatus,
    required this.myRole,
  });

  factory CommunityGroup.fromJson(Map<String, dynamic> json) => CommunityGroup(
    id: json['id'] as String,
    creatorUserId: json['creatorUserId'] as String? ?? '',
    creatorType: enumFromJson(
      CreatorType.values,
      json['creatorType'],
      CreatorType.individual,
    ),
    organizationId: json['organizationId'] as String?,
    name: json['name'] as String? ?? '',
    description: json['description'] as String? ?? '',
    type: enumFromJson(GroupType.values, json['type'], GroupType.interestGroup),
    category: json['category'] as String? ?? '',
    visibility: enumFromJson(
      GroupVisibility.values,
      json['visibility'],
      GroupVisibility.public,
    ),
    status: enumFromJson(GroupStatus.values, json['status'], GroupStatus.draft),
    city: json['city'] as String?,
    region: json['region'] as String?,
    postalCode: json['postalCode'] as String?,
    latitude: (json['latitude'] as num?)?.toDouble(),
    longitude: (json['longitude'] as num?)?.toDouble(),
    primaryLanguageCode: json['primaryLanguageCode'] as String?,
    coverImageUrl: json['coverImageUrl'] as String?,
    rules: json['rules'] as String? ?? '',
    maxMembers: json['maxMembers'] as int?,
    memberCount: json['memberCount'] as int? ?? 0,
    verificationStatus: enumFromJson(
      GroupVerificationStatus.values,
      json['verificationStatus'],
      GroupVerificationStatus.unverified,
    ),
    createdAtUtc:
        DateTime.tryParse(json['createdAtUtc'] as String? ?? '')?.toUtc() ??
        DateTime.now().toUtc(),
    myMembershipStatus: enumFromJsonOrNull(
      GroupMembershipStatus.values,
      json['myMembershipStatus'],
    ),
    myRole: enumFromJsonOrNull(GroupMemberRole.values, json['myRole']),
  );

  final String id;
  final String creatorUserId;
  final CreatorType creatorType;
  final String? organizationId;
  final String name;
  final String description;
  final GroupType type;
  final String category;
  final GroupVisibility visibility;
  final GroupStatus status;
  final String? city;
  final String? region;
  final String? postalCode;
  final double? latitude;
  final double? longitude;
  final String? primaryLanguageCode;
  final String? coverImageUrl;
  final String rules;
  final int? maxMembers;
  final int memberCount;
  final GroupVerificationStatus verificationStatus;
  final DateTime createdAtUtc;
  final GroupMembershipStatus? myMembershipStatus;
  final GroupMemberRole? myRole;

  /// Whether the caller already has an accepted seat in this group.
  bool get isActiveMember => myMembershipStatus == GroupMembershipStatus.active;

  /// Owner, Admin, or Moderator — allowed to post announcements, pin posts,
  /// and hide/remove other members' posts (backend-enforced; this only gates
  /// which actions the UI offers).
  bool get canManagePosts =>
      myRole == GroupMemberRole.owner ||
      myRole == GroupMemberRole.admin ||
      myRole == GroupMemberRole.moderator;
}
