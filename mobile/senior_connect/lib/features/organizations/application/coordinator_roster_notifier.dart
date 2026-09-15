// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
import '../../../core/network/api_client.dart';
part 'coordinator_roster_notifier.freezed.dart';
part 'coordinator_roster_notifier.g.dart';
@freezed
class CoordinatorRosterState with _ {
  const factory CoordinatorRosterState({
    @Default(true) bool isLoading,
    @Default([]) List<dynamic> allVolunteers,
    @Default('all') String selectedStatusFilter,
    @Default('') String searchQuery,
    String? errorMessage,
  }) = _CoordinatorRosterState;
}
@riverpod
class CoordinatorRosterNotifier extends _ {
  @override
  CoordinatorRosterState build(ApiClient apiClient, String organizationId) {
    Future(()=> loadRoster(apiClient, organizationId));
    return const CoordinatorRosterState();
  }
  Future<void> loadRoster(ApiClient apiClient, String orgId) async {
    state = state.copyWith(isLoading: true, errorMessage: null);
    try {
      final resp = await apiClient.get<dynamic>('/api/v1/coordinator/volunteers?organizationId=');
      state = state.copyWith(allVolunteers: resp is List ? resp : [], isLoading: false);
    } catch (_) { state = state.copyWith(isLoading: false, errorMessage: 'errors.generic'); }
  }
  void setStatusFilter(String f) => state = state.copyWith(selectedStatusFilter: f);
  void setSearchQuery(String q) => state = state.copyWith(searchQuery: q);
  Future<void> reactivate(String userId, String notes, ApiClient apiClient, String orgId) async {
    try {
      await apiClient.post<dynamic>('/api/v1/coordinator/volunteers/', data: {'notes': notes});
      await loadRoster(apiClient, orgId);
    } catch (_) { state = state.copyWith(errorMessage: 'errors.generic'); }
  }
}
