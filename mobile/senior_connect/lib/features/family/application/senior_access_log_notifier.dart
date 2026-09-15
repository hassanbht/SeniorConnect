// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
import '../../../core/network/api_client.dart';
part 'senior_access_log_notifier.freezed.dart';
part 'senior_access_log_notifier.g.dart';
@freezed
class SeniorAccessLogState with _ {
  const factory SeniorAccessLogState({
    @Default(true) bool isLoading,
    @Default([]) List<Map<String, dynamic>> logs,
    String? error,
  }) = _SeniorAccessLogState;
}
@riverpod
class SeniorAccessLogNotifier extends _ {
  @override
  SeniorAccessLogState build(ApiClient apiClient, String? seniorUserId) {
    Future(()=> _load(apiClient, seniorUserId));
    return const SeniorAccessLogState();
  }
  Future<void> _load(ApiClient apiClient, String? uid) async {
    state = state.copyWith(isLoading: true, error: null);
    try {
      final resp = await apiClient.get<List<dynamic>>('/api/v1/family/seniors//access-log', queryParameters: {'days': 30});
      state = state.copyWith(isLoading: false,
        logs: resp.map((e) => Map<String, dynamic>.from(e as Map)).toList());
    } catch (_) { state = const SeniorAccessLogState(isLoading: false, logs: []); }
  }
}
