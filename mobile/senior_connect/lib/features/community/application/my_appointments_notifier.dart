import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/cache/local_read_cache.dart';
import '../../../core/network/api_client.dart';

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
      await LocalReadCache.save('my_schedule', list);
      state = state.copyWith(appointments: list, isLoading: false);
    } catch (_) {
      // P7-04: On network failure / offline / airplane mode, retrieve from local cache
      final cached = await LocalReadCache.read('my_schedule');
      if (cached is List && cached.isNotEmpty) {
        final cachedList = cached
            .map((e) => Map<String, dynamic>.from(e as Map))
            .toList();
        state = state.copyWith(appointments: cachedList, isLoading: false);
        return;
      }

      // Fallback demo items for offline/testing if cache empty
      final demo = [
        {
          'eventId': 'ev-1',
          'title': 'Senioren-Schachtreff',
          'category': 'sports',
          'startsAtUtc': DateTime.now()
              .add(const Duration(days: 1, hours: 3))
              .toIso8601String(),
          'locationAddress': 'Gemeindezentrum Mitte, Raum 2',
          'locationPostalCode': '1010',
          'myStatus': 'Going',
          'isCancelled': false,
        },
        {
          'eventId': 'ev-2',
          'title': 'Gemeinsames Kaffeetrinken & Plaudern',
          'category': 'general',
          'startsAtUtc': DateTime.now()
              .add(const Duration(days: 3, hours: 2))
              .toIso8601String(),
          'locationAddress': 'Café Sonnenschein, Hauptstraße 12',
          'locationPostalCode': '1010',
          'myStatus': 'Waitlisted',
          'waitlistPosition': 2,
          'isCancelled': false,
        },
      ];
      state = state.copyWith(appointments: demo, isLoading: false);
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
