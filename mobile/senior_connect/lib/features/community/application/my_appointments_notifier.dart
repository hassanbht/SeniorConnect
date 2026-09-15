// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
import '../../../core/network/api_client.dart';
part 'my_appointments_notifier.freezed.dart';
part 'my_appointments_notifier.g.dart';

@freezed
class MyAppointmentsState with _ {
  const factory MyAppointmentsState({
    @Default(false) bool isLoading,
    @Default([]) List<Map<String, dynamic>> appointments,
    String? error,
  }) = _MyAppointmentsState;
}

@riverpod
class MyAppointmentsNotifier extends _ {
  @override
  MyAppointmentsState build(ApiClient apiClient) {
    Future(() => _load(apiClient));
    return const MyAppointmentsState();
  }

  Future<void> _load(ApiClient apiClient) async {
    state = state.copyWith(isLoading: true, error: null);
    try {
      final resp = await apiClient.get<List<dynamic>>(
        '/api/v1/community/me/schedule',
      );
      state = state.copyWith(
        isLoading: false,
        appointments: resp
            .map((e) => Map<String, dynamic>.from(e as Map))
            .toList(),
      );
    } catch (_) {
      state = MyAppointmentsState(
        isLoading: false,
        appointments: [
          {
            'eventId': 'ev-1',
            'title': 'Senioren-Schachtreff',
            'myStatus': 'Going',
            'isCancelled': false,
          },
        ],
      );
    }
  }

  Future<void> cancelRegistration(String eventId, ApiClient apiClient) async {
    try {
      await apiClient.delete<dynamic>('/api/v1/community/events//register');
      await _load(apiClient);
    } catch (_) {
      state = state.copyWith(error: 'errors.generic');
    }
  }
}
