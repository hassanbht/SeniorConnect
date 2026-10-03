// lib/features/community/domain/community_post.dart
//
// Mirrors CommunityPostDto (backend/modules/Community/Application/Dtos.cs).

import 'group_enums.dart';

class CommunityPost {
  const CommunityPost({
    required this.id,
    required this.authorUserId,
    required this.groupId,
    required this.postType,
    required this.title,
    required this.content,
    required this.isPinned,
    required this.status,
    required this.moderationReason,
    required this.createdAtUtc,
    required this.updatedAtUtc,
  });

  factory CommunityPost.fromJson(Map<String, dynamic> json) => CommunityPost(
    id: json['id'] as String,
    authorUserId: json['authorUserId'] as String,
    groupId: json['groupId'] as String,
    postType: enumFromJson(
      CommunityPostType.values,
      json['postType'],
      CommunityPostType.post,
    ),
    title: json['title'] as String? ?? '',
    content: json['content'] as String? ?? '',
    isPinned: json['isPinned'] as bool? ?? false,
    status: enumFromJson(
      CommunityPostStatus.values,
      json['status'],
      CommunityPostStatus.visible,
    ),
    moderationReason: json['moderationReason'] as String?,
    createdAtUtc:
        DateTime.tryParse(json['createdAtUtc'] as String? ?? '')?.toUtc() ??
        DateTime.now().toUtc(),
    updatedAtUtc:
        DateTime.tryParse(json['updatedAtUtc'] as String? ?? '')?.toUtc() ??
        DateTime.now().toUtc(),
  );

  final String id;
  final String authorUserId;
  final String groupId;
  final CommunityPostType postType;
  final String title;
  final String content;
  final bool isPinned;
  final CommunityPostStatus status;
  final String? moderationReason;
  final DateTime createdAtUtc;
  final DateTime updatedAtUtc;

  bool get isAnnouncement => postType == CommunityPostType.announcement;

  /// The backend only ever returns a caller's own pending-review post to
  /// them (other users' feeds never include it) — so any time this is true
  /// the viewer IS the author, and the UI should show a small notice.
  bool get isAwaitingReview => status == CommunityPostStatus.pendingReview;
}
