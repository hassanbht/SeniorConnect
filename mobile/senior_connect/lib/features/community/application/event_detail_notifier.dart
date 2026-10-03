import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';

class EventDetailState {
  const EventDetailState({
    this.isLoading = false,
    this.isActionInProgress = false,
    this.error,
    this.actionError,
    this.event,
    this.isRegistered = false,
  });

  final bool isLoading;
  final bool isActionInProgress;

  /// Localization key; set when the event could not be loaded.
  final String? error;

  /// Localization key; set when (un)registering failed.
  final String? actionError;
  final Map<String, dynamic>? event;
  final bool isRegistered;

  EventDetailState copyWith({
    bool? isLoading,
    bool? isActionInProgress,
    String? error,
    String? actionError,
    Map<String, dynamic>? event,
    bool? isRegistered,
  }) {
    return EventDetailState(
      isLoading: isLoading ?? this.isLoading,
      isActionInProgress: isActionInProgress ?? this.isActionInProgress,
      error: error,
      actionError: actionError,
      event: event ?? this.event,
      isRegistered: isRegistered ?? this.isRegistered,
    );
  }
}

String _errorKey(Object e) =>
    e is DioException ? mapDioError(e).l10nKey : 'errors.generic';

class EventDetailNotifier extends StateNotifier<EventDetailState> {
  EventDetailNotifier({required this.eventId, required this.apiClient})
    : super(const EventDetailState()) {
    loadEvent();
  }

  final String eventId;
  final ApiClient apiClient;

  Future<void> loadEvent() async {
    state = state.copyWith(isLoading: true);

    try {
      final data = await apiClient.get<Map<String, dynamic>>(
        '/api/v1/community/events/$eventId',
      );
      state = state.copyWith(
        event: data,
        isLoading: false,
        isRegistered: data['myRegistrationStatus'] != null,
      );
    } catch (e) {
      state = state.copyWith(isLoading: false, error: _errorKey(e));
    }
  }

  Future<void> toggleRegistration() async {
    state = state.copyWith(isActionInProgress: true);
    final wasRegistered = state.isRegistered;

    try {
      if (!wasRegistered) {
        await apiClient.post<dynamic>(
          '/api/v1/community/events/$eventId/register',
          data: {'status': 0},
        );
      } else {
        await apiClient.post<dynamic>(
          '/api/v1/community/events/$eventId/register:cancel',
        );
      }
      state = state.copyWith(
        isRegistered: !wasRegistered,
        isActionInProgress: false,
      );
    } catch (e) {
      // Keep the real registration state; only report the failure.
      state = state.copyWith(
        isActionInProgress: false,
        actionError: _errorKey(e),
      );
    }
  }
}

class EventDetailParams {
  const EventDetailParams({required this.eventId, required this.apiClient});

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

final eventDetailProvider = StateNotifierProvider.autoDispose
    .family<EventDetailNotifier, EventDetailState, EventDetailParams>(
      (ref, params) => EventDetailNotifier(
        eventId: params.eventId,
        apiClient: params.apiClient,
      ),
    );
