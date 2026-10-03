// lib/features/community/application/group_discovery_notifier.dart
//
// Groups tab of the Community screen — list + filter (category, city,
// language, type, free-text search).

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../data/community_repository.dart';
import '../domain/community_group.dart';
import '../domain/group_enums.dart';

class GroupDiscoveryState {
  const GroupDiscoveryState({
    this.isLoading = false,
    this.error,
    this.groups = const [],
    this.searchText = '',
    this.type,
    this.city,
    this.language,
  });

  final bool isLoading;
  final String? error;
  final List<CommunityGroup> groups;
  final String searchText;
  final GroupType? type;
  final String? city;
  final String? language;

  bool get hasActiveFilters => type != null || city != null || language != null;

  GroupDiscoveryState copyWith({
    bool? isLoading,
    String? error,
    List<CommunityGroup>? groups,
    String? searchText,
    Object? type = _sentinel,
    Object? city = _sentinel,
    Object? language = _sentinel,
  }) {
    return GroupDiscoveryState(
      isLoading: isLoading ?? this.isLoading,
      error: error,
      groups: groups ?? this.groups,
      searchText: searchText ?? this.searchText,
      type: type == _sentinel ? this.type : type as GroupType?,
      city: city == _sentinel ? this.city : city as String?,
      language: language == _sentinel ? this.language : language as String?,
    );
  }
}

const _sentinel = Object();

class GroupDiscoveryNotifier extends StateNotifier<GroupDiscoveryState> {
  GroupDiscoveryNotifier({required this._repository})
    : super(const GroupDiscoveryState()) {
    load();
  }

  final CommunityRepository _repository;

  Future<void> load() async {
    state = state.copyWith(isLoading: true, error: null);
    try {
      final groups = await _repository.getGroups(
        q: state.searchText.isEmpty ? null : state.searchText,
        type: state.type,
        city: state.city,
        language: state.language,
      );
      state = state.copyWith(groups: groups, isLoading: false);
    } on DioException catch (e) {
      state = state.copyWith(isLoading: false, error: mapDioError(e).l10nKey);
    } catch (_) {
      state = state.copyWith(isLoading: false, error: 'errors.generic');
    }
  }

  void setSearchText(String value) {
    state = state.copyWith(searchText: value);
    load();
  }

  void setFilters({GroupType? type, String? city, String? language}) {
    state = state.copyWith(type: type, city: city, language: language);
    load();
  }

  void clearFilters() {
    state = state.copyWith(type: null, city: null, language: null);
    load();
  }
}

class GroupDiscoveryParams {
  const GroupDiscoveryParams({required this.apiClient});

  final ApiClient apiClient;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is GroupDiscoveryParams && identical(other.apiClient, apiClient));

  @override
  int get hashCode => apiClient.hashCode;
}

final groupDiscoveryProvider = StateNotifierProvider.autoDispose
    .family<GroupDiscoveryNotifier, GroupDiscoveryState, GroupDiscoveryParams>(
      (ref, params) => GroupDiscoveryNotifier(
        repository: CommunityRepositoryImpl(params.apiClient),
      ),
    );
