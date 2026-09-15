// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
import '../../../core/network/api_client.dart';
part 'event_detail_notifier.freezed.dart';
part 'event_detail_notifier.g.dart';
@freezed
class EventDetailState with _ {
  const factory EventDetailState({
    @Default(true) bool isLoading,
    @Default(false) bool isRegistered,
    Map<String, dynamic>? event,
    String? error,
  }) = _EventDetailState;
}
@riverpod
class EventDetailNotifier extends _ {
  @override
  EventDetailState build(ApiClient apiClient, String eventId) {
    Future(()=> _load(apiClient, eventId));
    return const EventDetailState();
  }
  Future<void> _load(ApiClient apiClient, String eventId) async {
    state = state.copyWith(isLoading: true, error: null);
    try {
      final data = await apiClient.get<Map<String, dynamic>>('/api/v1/community/events/');
      state = state.copyWith(event: data, isLoading: false);
    } catch (_) {
      state = state.copyWith(isLoading: false, event: {'id': eventId, 'title': 'Senioren-Schachtreff', 'category': 'sports'});
    }
  }
  Future<void> toggleRegistration(ApiClient apiClient, String eventId) async {
    state = state.copyWith(isLoading: true);
    try {
      if (!state.isRegistered) {
        await apiClient.post<dynamic>('/api/v1/community/events//register', data: {});
      } else {
        await apiClient.delete<dynamic>('/api/v1/community/events//register');
      }
      state = state.copyWith(isRegistered: !state.isRegistered, isLoading: false);
    } catch (_) { state = state.copyWith(isLoading: false, error: 'errors.generic'); }
  }
}
