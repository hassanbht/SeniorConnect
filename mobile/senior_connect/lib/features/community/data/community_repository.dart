// lib/features/community/data/community_repository.dart
//
// Wraps the Community Groups / Posts / Reports API
// (`/api/v1/community/*` — see backend/SeniorConnect.Api/Endpoints/CommunityEndpoints.cs).
//
// Deliberately does NOT wrap the update/archive-group or member-moderation
// endpoints (mute/ban/remove/invite/appoint-moderator) — those back UI that
// is explicitly out of scope for this feature slice (see
// docs/plans/active/2026-09-community-platform-analysis-and-plan.md).

import '../../../core/network/api_client.dart';
import '../domain/community_group.dart';
import '../domain/community_post.dart';
import '../domain/group_enums.dart';
import '../domain/group_membership.dart';

abstract class CommunityRepository {
  Future<CommunityGroup> createGroup({
    required String name,
    required String description,
    required GroupType type,
    GroupVisibility visibility = GroupVisibility.public,
    String? city,
    String? region,
    String? postalCode,
    String? primaryLanguageCode,
    String? rules,
  });

  Future<List<CommunityGroup>> getGroups({
    String? category,
    String? city,
    String? region,
    String? language,
    GroupType? type,
    String? q,
  });

  Future<CommunityGroup> getGroupById(String id);

  Future<GroupMembership> joinGroup(String id);

  Future<void> leaveGroup(String id);

  Future<List<GroupMembership>> getGroupMembers(String id);

  Future<List<CommunityPost>> getGroupPosts(String id);

  Future<CommunityPost> createPost(
    String groupId, {
    required String title,
    required String content,
    CommunityPostType postType = CommunityPostType.post,
    bool isPinned = false,
  });

  Future<void> hidePost(String groupId, String postId, {String? reason});

  Future<void> removePost(String groupId, String postId, {String? reason});

  Future<void> createReport({
    required CommunityReportTargetType targetType,
    required String targetId,
    required CommunityReportCategory category,
    required String description,
    String? groupId,
  });
}

class CommunityRepositoryImpl implements CommunityRepository {
  CommunityRepositoryImpl(this._apiClient);

  final ApiClient _apiClient;

  @override
  Future<CommunityGroup> createGroup({
    required String name,
    required String description,
    required GroupType type,
    GroupVisibility visibility = GroupVisibility.public,
    String? city,
    String? region,
    String? postalCode,
    String? primaryLanguageCode,
    String? rules,
  }) async {
    final response = await _apiClient.post<Map<String, dynamic>>(
      '/api/v1/community/groups',
      data: {
        'name': name,
        'description': description,
        'type': type.index,
        'visibility': visibility.index,
        'city': ?city,
        'region': ?region,
        'postalCode': ?postalCode,
        'primaryLanguageCode': ?primaryLanguageCode,
        'rules': ?rules,
      },
    );
    return CommunityGroup.fromJson(response);
  }

  @override
  Future<List<CommunityGroup>> getGroups({
    String? category,
    String? city,
    String? region,
    String? language,
    GroupType? type,
    String? q,
  }) async {
    final response = await _apiClient.get<List<dynamic>>(
      '/api/v1/community/groups',
      queryParameters: {
        'category': ?category,
        'city': ?city,
        'region': ?region,
        'language': ?language,
        'type': ?type?.index,
        'q': ?q,
      },
    );
    return response
        .map(
          (e) => CommunityGroup.fromJson(Map<String, dynamic>.from(e as Map)),
        )
        .toList();
  }

  @override
  Future<CommunityGroup> getGroupById(String id) async {
    final response = await _apiClient.get<Map<String, dynamic>>(
      '/api/v1/community/groups/$id',
    );
    return CommunityGroup.fromJson(response);
  }

  @override
  Future<GroupMembership> joinGroup(String id) async {
    final response = await _apiClient.post<Map<String, dynamic>>(
      '/api/v1/community/groups/$id/join',
    );
    return GroupMembership.fromJson(response);
  }

  @override
  Future<void> leaveGroup(String id) =>
      _apiClient.post<dynamic>('/api/v1/community/groups/$id:leave');

  @override
  Future<List<GroupMembership>> getGroupMembers(String id) async {
    final response = await _apiClient.get<List<dynamic>>(
      '/api/v1/community/groups/$id/members',
    );
    return response
        .map(
          (e) => GroupMembership.fromJson(Map<String, dynamic>.from(e as Map)),
        )
        .toList();
  }

  @override
  Future<List<CommunityPost>> getGroupPosts(String id) async {
    final response = await _apiClient.get<List<dynamic>>(
      '/api/v1/community/groups/$id/posts',
    );
    return response
        .map((e) => CommunityPost.fromJson(Map<String, dynamic>.from(e as Map)))
        .toList();
  }

  @override
  Future<CommunityPost> createPost(
    String groupId, {
    required String title,
    required String content,
    CommunityPostType postType = CommunityPostType.post,
    bool isPinned = false,
  }) async {
    final response = await _apiClient.post<Map<String, dynamic>>(
      '/api/v1/community/groups/$groupId/posts',
      data: {
        'title': title,
        'content': content,
        'postType': postType.index,
        'isPinned': isPinned,
      },
    );
    return CommunityPost.fromJson(response);
  }

  @override
  Future<void> hidePost(String groupId, String postId, {String? reason}) =>
      _apiClient.post<dynamic>(
        '/api/v1/community/groups/$groupId/posts/$postId:hide',
        data: {'reason': ?reason},
      );

  @override
  Future<void> removePost(String groupId, String postId, {String? reason}) =>
      _apiClient.delete<dynamic>(
        '/api/v1/community/groups/$groupId/posts/$postId',
        data: {'reason': ?reason},
      );

  @override
  Future<void> createReport({
    required CommunityReportTargetType targetType,
    required String targetId,
    required CommunityReportCategory category,
    required String description,
    String? groupId,
  }) => _apiClient.post<dynamic>(
    '/api/v1/community/reports',
    data: {
      'targetType': targetType.index,
      'targetId': targetId,
      'category': category.index,
      'description': description,
      'groupId': ?groupId,
    },
  );
}
