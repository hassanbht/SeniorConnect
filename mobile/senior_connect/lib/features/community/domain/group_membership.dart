// lib/features/community/domain/group_membership.dart
//
// Mirrors GroupMembershipDto (backend/modules/Community/Application/Dtos.cs).

import 'group_enums.dart';

class GroupMembership {
  const GroupMembership({
    required this.id,
    required this.groupId,
    required this.userId,
    required this.role,
    required this.status,
    required this.joinedAtUtc,
    required this.mutedUntilUtc,
    required this.banReason,
  });

  factory GroupMembership.fromJson(Map<String, dynamic> json) =>
      GroupMembership(
        id: json['id'] as String,
        groupId: json['groupId'] as String,
        userId: json['userId'] as String,
        role: enumFromJson(
          GroupMemberRole.values,
          json['role'],
          GroupMemberRole.member,
        ),
        status: enumFromJson(
          GroupMembershipStatus.values,
          json['status'],
          GroupMembershipStatus.pending,
        ),
        joinedAtUtc: json['joinedAtUtc'] != null
            ? DateTime.tryParse(json['joinedAtUtc'] as String)?.toUtc()
            : null,
        mutedUntilUtc: json['mutedUntilUtc'] != null
            ? DateTime.tryParse(json['mutedUntilUtc'] as String)?.toUtc()
            : null,
        banReason: json['banReason'] as String?,
      );

  final String id;
  final String groupId;
  final String userId;
  final GroupMemberRole role;
  final GroupMembershipStatus status;
  final DateTime? joinedAtUtc;
  final DateTime? mutedUntilUtc;
  final String? banReason;
}
