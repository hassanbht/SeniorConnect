import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/cache/local_read_cache.dart';
import '../../../core/network/api_client.dart';

/// Local read cache key for the user's schedule (P7-04).
const kMyScheduleCacheKey = 'my_schedule';

class MyAppointmentsState {
  const MyAppointmentsState({
    this.isLoading = false,
    this.error,
    this.appointments = const [],
  });

  final bool isLoading;
  final String? error;
  final List<Map<String, dynamic>> appointments;

  MyAppointmentsState copyWith({
    bool? isLoading,
    String? error,
    List<Map<String, dynamic>>? appointments,
  }) {
    return MyAppointmentsState(
      isLoading: isLoading ?? this.isLoading,
      error: error,
      appointments: appointments ?? this.appointments,
    );
  }
}

class MyAppointmentsNotifier extends StateNotifier<MyAppointmentsState> {
  MyAppointmentsNotifier({required this.apiClient})
    : super(const MyAppointmentsState()) {
    loadAppointments();
  }

  final ApiClient apiClient;

  Future<void> loadAppointments() async {
    state = state.copyWith(isLoading: true, error: null);

    try {
      final response = await apiClient.get<List<dynamic>>(
        '/api/v1/community/my-schedule',
      );
      final list = response
          .map((e) => Map<String, dynamic>.from(e as Map))
          .toList();
      // P7-04: Persist into local read cache for offline / airplane mode resilience
      await LocalReadCache.save(kMyScheduleCacheKey, list);
      state = state.copyWith(appointments: list, isLoading: false);
    } catch (_) {
      // P7-04: On network failure / offline / airplane mode, retrieve from local cache
      final cached = await LocalReadCache.read(kMyScheduleCacheKey);
      if (cached is List && cached.isNotEmpty) {
        final cachedList = cached
            .map((e) => Map<String, dynamic>.from(e as Map))
            .toList();
        state = state.copyWith(appointments: cachedList, isLoading: false);
        return;
      }

      state = state.copyWith(isLoading: false, error: 'errors.generic');
    }
  }

  Future<void> cancelAppointment(String eventId) async {
    try {
      await apiClient.post<dynamic>(
        '/api/v1/community/events/$eventId/register:cancel',
      );
      await loadAppointments();
    } catch (_) {
      // Leave state as-is — don't pretend the cancellation succeeded when
      // the request actually failed (e.g. offline).
    }
  }
}

final myAppointmentsProvider = StateNotifierProvider.autoDispose
    .family<MyAppointmentsNotifier, MyAppointmentsState, ApiClient>(
      (ref, apiClient) => MyAppointmentsNotifier(apiClient: apiClient),
    );
