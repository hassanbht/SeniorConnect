// lib/features/community/application/create_post_notifier.dart

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../data/community_repository.dart';
import '../domain/community_post.dart';
import '../domain/group_enums.dart';

class CreatePostState {
  const CreatePostState({
    this.isAnnouncement = false,
    this.isSubmitting = false,
    this.error,
  });

  final bool isAnnouncement;
  final bool isSubmitting;
  final String? error;

  CreatePostState copyWith({
    bool? isAnnouncement,
    bool? isSubmitting,
    String? error,
  }) {
    return CreatePostState(
      isAnnouncement: isAnnouncement ?? this.isAnnouncement,
      isSubmitting: isSubmitting ?? this.isSubmitting,
      error: error,
    );
  }
}

class CreatePostNotifier extends StateNotifier<CreatePostState> {
  CreatePostNotifier({required this.groupId, required this._repository})
    : super(const CreatePostState());

  final String groupId;
  final CommunityRepository _repository;

  void setIsAnnouncement(bool value) =>
      state = state.copyWith(isAnnouncement: value);

  Future<CommunityPost?> submit({
    required String title,
    required String content,
  }) async {
    state = state.copyWith(isSubmitting: true, error: null);
    try {
      final post = await _repository.createPost(
        groupId,
        title: title.trim(),
        content: content.trim(),
        postType: state.isAnnouncement
            ? CommunityPostType.announcement
            : CommunityPostType.post,
        isPinned: state.isAnnouncement,
      );
      state = state.copyWith(isSubmitting: false);
      return post;
    } on DioException catch (e) {
      state = state.copyWith(
        isSubmitting: false,
        error: mapDioError(e).l10nKey,
      );
      return null;
    } catch (_) {
      state = state.copyWith(isSubmitting: false, error: 'errors.generic');
      return null;
    }
  }
}

class CreatePostParams {
  const CreatePostParams({required this.groupId, required this.apiClient});

  final String groupId;
  final ApiClient apiClient;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is CreatePostParams &&
          other.groupId == groupId &&
          identical(other.apiClient, apiClient));

  @override
  int get hashCode => Object.hash(groupId, apiClient);
}

final createPostProvider = StateNotifierProvider.autoDispose
    .family<CreatePostNotifier, CreatePostState, CreatePostParams>(
      (ref, params) => CreatePostNotifier(
        groupId: params.groupId,
        repository: CommunityRepositoryImpl(params.apiClient),
      ),
    );
