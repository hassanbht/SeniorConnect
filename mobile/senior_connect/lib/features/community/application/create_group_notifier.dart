// lib/features/community/application/create_group_notifier.dart

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../data/community_repository.dart';
import '../domain/community_group.dart';
import '../domain/group_enums.dart';

class CreateGroupState {
  const CreateGroupState({
    this.type = GroupType.interestGroup,
    this.visibility = GroupVisibility.public,
    this.isSubmitting = false,
    this.error,
  });

  final GroupType type;
  final GroupVisibility visibility;
  final bool isSubmitting;
  final String? error;

  CreateGroupState copyWith({
    GroupType? type,
    GroupVisibility? visibility,
    bool? isSubmitting,
    String? error,
  }) {
    return CreateGroupState(
      type: type ?? this.type,
      visibility: visibility ?? this.visibility,
      isSubmitting: isSubmitting ?? this.isSubmitting,
      error: error,
    );
  }
}

class CreateGroupNotifier extends StateNotifier<CreateGroupState> {
  CreateGroupNotifier({required this._repository})
    : super(const CreateGroupState());

  final CommunityRepository _repository;

  void setType(GroupType type) => state = state.copyWith(type: type);

  void setVisibility(GroupVisibility visibility) =>
      state = state.copyWith(visibility: visibility);

  Future<CommunityGroup?> submit({
    required String name,
    required String description,
    String? city,
    String? region,
    String? primaryLanguageCode,
  }) async {
    state = state.copyWith(isSubmitting: true, error: null);
    try {
      final group = await _repository.createGroup(
        name: name.trim(),
        description: description.trim(),
        type: state.type,
        visibility: state.visibility,
        city: _blankToNull(city),
        region: _blankToNull(region),
        primaryLanguageCode: _blankToNull(primaryLanguageCode),
      );
      state = state.copyWith(isSubmitting: false);
      return group;
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

  String? _blankToNull(String? value) {
    final trimmed = value?.trim();
    return (trimmed == null || trimmed.isEmpty) ? null : trimmed;
  }
}

final createGroupProvider = StateNotifierProvider.autoDispose
    .family<CreateGroupNotifier, CreateGroupState, ApiClient>(
      (ref, apiClient) =>
          CreateGroupNotifier(repository: CommunityRepositoryImpl(apiClient)),
    );
