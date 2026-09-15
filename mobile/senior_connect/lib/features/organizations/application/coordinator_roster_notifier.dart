import 'package:dio/dio.dart';
import 'package:easy_localization/easy_localization.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';

class CoordinatorRosterState {
  const CoordinatorRosterState({
    this.isLoading = true,
    this.errorMessage,
    this.allVolunteers = const [],
    this.selectedStatusFilter = 'all',
    this.searchQuery = '',
  });

  final bool isLoading;
  final String? errorMessage;
  final List<dynamic> allVolunteers;
  final String selectedStatusFilter;
  final String searchQuery;

  List<dynamic> get filteredVolunteers {
    return allVolunteers.where((v) {
      final m = v as Map<String, dynamic>;
      final status = m['rosterStatus'] as String? ?? '';
      final name = (m['displayName'] as String? ?? '').toLowerCase();
      final phone = (m['phone'] as String? ?? '').toLowerCase();

      final matchesStatus = selectedStatusFilter == 'all' ||
          status.toLowerCase() == selectedStatusFilter.toLowerCase();
      final matchesSearch = searchQuery.isEmpty ||
          name.contains(searchQuery.toLowerCase()) ||
          phone.contains(searchQuery.toLowerCase());

      return matchesStatus && matchesSearch;
    }).toList();
  }

  CoordinatorRosterState copyWith({
    bool? isLoading,
    String? errorMessage,
    List<dynamic>? allVolunteers,
    String? selectedStatusFilter,
    String? searchQuery,
  }) {
    return CoordinatorRosterState(
      isLoading: isLoading ?? this.isLoading,
      errorMessage: errorMessage,
      allVolunteers: allVolunteers ?? this.allVolunteers,
      selectedStatusFilter:
          selectedStatusFilter ?? this.selectedStatusFilter,
      searchQuery: searchQuery ?? this.searchQuery,
    );
  }
}

class CoordinatorRosterNotifier
    extends StateNotifier<CoordinatorRosterState> {
  CoordinatorRosterNotifier({
    required this.organizationId,
    required this.apiClient,
  }) : super(const CoordinatorRosterState()) {
    loadRoster();
  }

  final String organizationId;
  final ApiClient apiClient;

  Future<void> loadRoster() async {
    state = state.copyWith(isLoading: true, errorMessage: null);

    try {
      final resp = await apiClient.get<dynamic>(
        '/api/v1/coordinator/volunteers?organizationId=$organizationId',
      );

      final list = resp is List ? resp : [];
      state = state.copyWith(isLoading: false, allVolunteers: list);
    } on DioException catch (e) {
      state = state.copyWith(
        isLoading: false,
        errorMessage: mapDioError(e).l10nKey.tr(),
      );
    } catch (_) {
      state = state.copyWith(
        isLoading: false,
        errorMessage: 'errors.generic'.tr(),
      );
    }
  }

  void setStatusFilter(String filter) {
    state = state.copyWith(selectedStatusFilter: filter);
  }

  void setSearchQuery(String query) {
    state = state.copyWith(searchQuery: query);
  }

  Future<String?> reactivate(String volunteerUserId) async {
    try {
      await apiClient.post<dynamic>(
        '/api/v1/coordinator/volunteers/$volunteerUserId:reactivate',
        data: {'notes': 'coordinator.reactivated_by_coordinator'.tr()},
      );
      await loadRoster();
      return null;
    } on DioException catch (e) {
      return mapDioError(e).l10nKey;
    } catch (_) {
      return 'errors.generic';
    }
  }
}

class CoordinatorRosterParams {
  const CoordinatorRosterParams({
    required this.organizationId,
    required this.apiClient,
  });

  final String organizationId;
  final ApiClient apiClient;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is CoordinatorRosterParams &&
          other.organizationId == organizationId &&
          identical(other.apiClient, apiClient));

  @override
  int get hashCode => Object.hash(organizationId, apiClient);
}

final coordinatorRosterProvider = StateNotifierProvider.autoDispose.family<
    CoordinatorRosterNotifier,
    CoordinatorRosterState,
    CoordinatorRosterParams>(
  (ref, params) => CoordinatorRosterNotifier(
    organizationId: params.organizationId,
    apiClient: params.apiClient,
  ),
);
