// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
import '../../../core/network/api_client.dart';
part 'coordinator_attention_dashboard_notifier.freezed.dart';
part 'coordinator_attention_dashboard_notifier.g.dart';

@freezed
class CoordinatorAttentionDashboardState with _ {
  const factory CoordinatorAttentionDashboardState({
    @Default(true) bool isLoading,
    @Default(false) bool isSendingReminders,
    @Default(0) int unconfirmedCount,
    @Default(0) int disputedCount,
    @Default(0) int pendingAppsCount,
    @Default([]) List<dynamic> expiringVerifications,
    @Default([]) List<dynamic> silentVolunteers,
    String? errorMessage,
  }) = _CoordinatorAttentionDashboardState;
}

@riverpod
class CoordinatorAttentionDashboardNotifier extends _ {
  @override
  CoordinatorAttentionDashboardState build(
    ApiClient apiClient,
    String organizationId,
  ) {
    Future(() => _load(apiClient, organizationId));
    return const CoordinatorAttentionDashboardState();
  }

  Future<void> _load(ApiClient apiClient, String orgId) async {
    state = state.copyWith(isLoading: true, errorMessage: null);
    try {
      final triage = await apiClient.get<dynamic>(
        '/api/v1/coordinator/triage?organizationId=',
      );
      int unconf = 0, disp = 0, pend = 0;
      if (triage is Map<String, dynamic>) {
        unconf = triage['unconfirmedActivitiesCount'] as int? ?? 0;
        disp = triage['disputedActivitiesCount'] as int? ?? 0;
        pend = triage['pendingApplicationsCount'] as int? ?? 0;
      }
      List<dynamic> exp = [], silent = [];
      try {
        final e = await apiClient.get<dynamic>(
          '/api/v1/coordinator/attention/expiring-verifications',
        );
        if (e is List) exp = e;
      } catch (_) {}
      try {
        final s = await apiClient.get<dynamic>(
          '/api/v1/coordinator/attention/silent-volunteers?organizationId=',
        );
        if (s is List) silent = s;
      } catch (_) {}
      state = state.copyWith(
        isLoading: false,
        unconfirmedCount: unconf,
        disputedCount: disp,
        pendingAppsCount: pend,
        expiringVerifications: exp,
        silentVolunteers: silent,
      );
    } catch (_) {
      state = state.copyWith(isLoading: false, errorMessage: 'errors.generic');
    }
  }

  Future<void> sendReminders(String orgId, ApiClient apiClient) async {
    state = state.copyWith(isSendingReminders: true);
    try {
      await apiClient.post<dynamic>(
        '/api/v1/coordinator/volunteers/send-reactivation-reminders',
        data: {'organizationId': orgId},
      );
    } catch (_) {}
    state = state.copyWith(isSendingReminders: false);
  }
}
