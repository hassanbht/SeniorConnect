// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
import '../../../core/network/api_client.dart';
part 'coordinator_hours_queue_notifier.freezed.dart';
part 'coordinator_hours_queue_notifier.g.dart';

@freezed
class CoordinatorHoursQueueState with _ {
  const factory CoordinatorHoursQueueState({
    @Default(true) bool isLoading,
    @Default(false) bool isBatchProcessing,
    @Default([]) List<dynamic> activities,
    String? errorMessage,
  }) = _CoordinatorHoursQueueState;
}

@riverpod
class CoordinatorHoursQueueNotifier extends _ {
  @override
  CoordinatorHoursQueueState build(ApiClient apiClient, String organizationId) {
    Future(() => loadQueue(apiClient, organizationId));
    return const CoordinatorHoursQueueState();
  }

  Future<void> loadQueue(ApiClient apiClient, String orgId) async {
    state = state.copyWith(isLoading: true, errorMessage: null);
    try {
      final resp = await apiClient.get<dynamic>(
        '/api/v1/coordinator/attention/unconfirmed-hours?organizationId=',
      );
      state = state.copyWith(
        activities: resp is List ? resp : [],
        isLoading: false,
      );
    } catch (_) {
      state = state.copyWith(isLoading: false, errorMessage: 'errors.generic');
    }
  }

  Future<void> confirmActivity(
    String activityId,
    ApiClient apiClient,
    String orgId,
  ) async {
    try {
      await apiClient.post<dynamic>('/api/v1/activities/', data: {});
      await loadQueue(apiClient, orgId);
    } catch (_) {
      state = state.copyWith(errorMessage: 'errors.generic');
    }
  }

  Future<void> batchConfirmAll(ApiClient apiClient, String orgId) async {
    state = state.copyWith(isBatchProcessing: true);
    try {
      for (final a in state.activities) {
        final id = (a as Map<String, dynamic>)['id'] as String?;
        if (id != null)
          await apiClient.post<dynamic>('/api/v1/activities/', data: {});
      }
      await loadQueue(apiClient, orgId);
    } catch (_) {
      state = state.copyWith(errorMessage: 'errors.generic');
    } finally {
      state = state.copyWith(isBatchProcessing: false);
    }
  }
}
