// lib/features/community/application/group_detail_notifier.dart

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../data/community_repository.dart';
import '../domain/community_group.dart';
import '../domain/community_post.dart';

class GroupDetailState {
  const GroupDetailState({
    this.isLoading = false,
    this.error,
    this.group,
    this.posts = const [],
    this.isActionInProgress = false,
    this.actionError,
  });

  final bool isLoading;
  final String? error;
  final CommunityGroup? group;
  final List<CommunityPost> posts;
  final bool isActionInProgress;
  final String? actionError;

  /// Pinned announcements first, then the rest, newest first within each.
  List<CommunityPost> get sortedPosts {
    final pinned = posts.where((p) => p.isPinned).toList()
      ..sort((a, b) => b.createdAtUtc.compareTo(a.createdAtUtc));
    final rest = posts.where((p) => !p.isPinned).toList()
      ..sort((a, b) => b.createdAtUtc.compareTo(a.createdAtUtc));
    return [...pinned, ...rest];
  }

  GroupDetailState copyWith({
    bool? isLoading,
    String? error,
    CommunityGroup? group,
    List<CommunityPost>? posts,
    bool? isActionInProgress,
    String? actionError,
  }) {
    return GroupDetailState(
      isLoading: isLoading ?? this.isLoading,
      error: error,
      group: group ?? this.group,
      posts: posts ?? this.posts,
      isActionInProgress: isActionInProgress ?? this.isActionInProgress,
      actionError: actionError,
    );
  }
}

class GroupDetailNotifier extends StateNotifier<GroupDetailState> {
  GroupDetailNotifier({required this.groupId, required this._repository})
    : super(const GroupDetailState()) {
    load();
  }

  final String groupId;
  final CommunityRepository _repository;

  Future<void> load() async {
    state = state.copyWith(isLoading: true, error: null);
    try {
      final group = await _repository.getGroupById(groupId);
      final posts = await _repository.getGroupPosts(groupId);
      state = state.copyWith(group: group, posts: posts, isLoading: false);
    } on DioException catch (e) {
      state = state.copyWith(isLoading: false, error: mapDioError(e).l10nKey);
    } catch (_) {
      state = state.copyWith(isLoading: false, error: 'errors.generic');
    }
  }

  Future<void> refreshPosts() async {
    try {
      final posts = await _repository.getGroupPosts(groupId);
      state = state.copyWith(posts: posts);
    } on DioException catch (_) {
      // Keep showing the previously-loaded posts on a transient failure.
    }
  }

  Future<void> join() => _runAction(() async {
    await _repository.joinGroup(groupId);
    await load();
  });

  Future<void> leave() => _runAction(() async {
    await _repository.leaveGroup(groupId);
    await load();
  });

  Future<void> _runAction(Future<void> Function() action) async {
    state = state.copyWith(isActionInProgress: true, actionError: null);
    try {
      await action();
      state = state.copyWith(isActionInProgress: false);
    } on DioException catch (e) {
      state = state.copyWith(
        isActionInProgress: false,
        actionError: mapDioError(e).l10nKey,
      );
    } catch (_) {
      state = state.copyWith(
        isActionInProgress: false,
        actionError: 'errors.generic',
      );
    }
  }
}

class GroupDetailParams {
  const GroupDetailParams({required this.groupId, required this.apiClient});

  final String groupId;
  final ApiClient apiClient;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is GroupDetailParams &&
          other.groupId == groupId &&
          identical(other.apiClient, apiClient));

  @override
  int get hashCode => Object.hash(groupId, apiClient);
}

final groupDetailProvider = StateNotifierProvider.autoDispose
    .family<GroupDetailNotifier, GroupDetailState, GroupDetailParams>(
      (ref, params) => GroupDetailNotifier(
        groupId: params.groupId,
        repository: CommunityRepositoryImpl(params.apiClient),
      ),
    );
