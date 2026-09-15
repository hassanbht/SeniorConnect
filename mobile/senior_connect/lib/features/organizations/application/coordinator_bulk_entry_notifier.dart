// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
import '../../../core/network/api_client.dart';
part 'coordinator_bulk_entry_notifier.freezed.dart';
part 'coordinator_bulk_entry_notifier.g.dart';
@freezed
class CoordinatorBulkEntryState with _ {
  const factory CoordinatorBulkEntryState({
    @Default(true) bool isLoading,
    @Default(false) bool isSubmitting,
    @Default([]) List<dynamic> volunteers,
    @Default([]) List<dynamic> categories,
    String? selectedVolunteerId,
    String? selectedCategoryId,
    @Default(60) int selectedDurationMinutes,
    @Default(1) int insuranceContext,
  }) = _CoordinatorBulkEntryState;
}
@riverpod
class CoordinatorBulkEntryNotifier extends _ {
  @override
  CoordinatorBulkEntryState build(ApiClient apiClient, String organizationId) {
    Future(()=> _loadRef(apiClient, organizationId));
    return const CoordinatorBulkEntryState();
  }
  Future<void> _loadRef(ApiClient apiClient, String orgId) async {
    state = state.copyWith(isLoading: true);
    try {
      final vols = await apiClient.get<dynamic>('/api/v1/coordinator/volunteers?organizationId=');
      final cats = await apiClient.get<dynamic>('/api/v1/activities/categories');
      final v = vols is List ? vols : <dynamic>[];
      final c = cats is List ? cats : <dynamic>[];
      state = state.copyWith(volunteers: v, categories: c, isLoading: false,
        selectedVolunteerId: v.isNotEmpty ? (v.first as Map<String,dynamic>)['userId'] as String? : null,
        selectedCategoryId: c.isNotEmpty ? (c.first as Map<String,dynamic>)['id'] as String? : null);
    } catch (_) { state = state.copyWith(isLoading: false); }
  }
  void setVolunteer(String? id) => state = state.copyWith(selectedVolunteerId: id);
  void setCategory(String? id) => state = state.copyWith(selectedCategoryId: id);
  void setDuration(int m) => state = state.copyWith(selectedDurationMinutes: m);
  void setSubmitting(bool v) => state = state.copyWith(isSubmitting: v);
}
