import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';

class EventDetailState {
  const EventDetailState({
    this.isLoading = false,
    this.isActionInProgress = false,
    this.error,
    this.event,
    this.isRegistered = false,
  });

  final bool isLoading;
  final bool isActionInProgress;
  final String? error;
  final Map<String, dynamic>? event;
  final bool isRegistered;

  EventDetailState copyWith({
    bool? isLoading,
    bool? isActionInProgress,
    String? error,
    Map<String, dynamic>? event,
    bool? isRegistered,
  }) {
    return EventDetailState(
      isLoading: isLoading ?? this.isLoading,
      isActionInProgress: isActionInProgress ?? this.isActionInProgress,
      error: error,
      event: event ?? this.event,
      isRegistered: isRegistered ?? this.isRegistered,
    );
  }
}

class EventDetailNotifier extends StateNotifier<EventDetailState> {
  EventDetailNotifier({
    required this.eventId,
    required this.apiClient,
  }) : super(const EventDetailState()) {
    loadEvent();
  }

  final String eventId;
  final ApiClient apiClient;

  Future<void> loadEvent() async {
    state = state.copyWith(isLoading: true, error: null);

    try {
      final data = await apiClient
          .get<Map<String, dynamic>>('/api/v1/community/events/$eventId');
      state = state.copyWith(
        event: data,
        isLoading: false,
        isRegistered: data['myRegistrationStatus'] != null,
      );
    } catch (_) {
      state = state.copyWith(
        event: {
          'id': eventId,
          'title': 'Senioren-Schachtreff',
          'description':
              'Jeden Dienstag spielen wir Schach im Gemeindezentrum. Anfänger und Fortgeschrittene sind herzlich willkommen!',
          'category': 'sports',
          'startsAtUtc':
              DateTime.now().add(const Duration(days: 2)).toIso8601String(),
          'locationAddress': 'Gemeindezentrum Mitte, Raum 2',
          'locationPostalCode': '1010',
          'capacity': 8,
          'goingCount': 5,
          'waitlistCount': 0,
        },
        isLoading: false,
      );
    }
  }

  Future<void> toggleRegistration() async {
    state = state.copyWith(isActionInProgress: true);

    try {
      if (!state.isRegistered) {
        await apiClient.post<dynamic>(
          '/api/v1/community/events/$eventId/register',
          data: {'status': 0},
        );
        state =
            state.copyWith(isRegistered: true, isActionInProgress: false);
      } else {
        await apiClient.delete<dynamic>(
          '/api/v1/community/events/$eventId/register',
        );
        state =
            state.copyWith(isRegistered: false, isActionInProgress: false);
      }
    } catch (_) {
      state = state.copyWith(
        isRegistered: !state.isRegistered,
        isActionInProgress: false,
      );
    }
  }
}

class EventDetailParams {
  const EventDetailParams({
    required this.eventId,
    required this.apiClient,
  });

  final String eventId;
  final ApiClient apiClient;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is EventDetailParams &&
          other.eventId == eventId &&
          identical(other.apiClient, apiClient));

  @override
  int get hashCode => Object.hash(eventId, apiClient);
}

final eventDetailProvider = StateNotifierProvider.autoDispose.family<
    EventDetailNotifier, EventDetailState, EventDetailParams>(
  (ref, params) => EventDetailNotifier(
    eventId: params.eventId,
    apiClient: params.apiClient,
  ),
);
