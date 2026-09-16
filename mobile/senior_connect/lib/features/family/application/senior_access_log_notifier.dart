import 'package:easy_localization/easy_localization.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';

class SeniorAccessLogState {
  const SeniorAccessLogState({
    this.isLoading = true,
    this.error,
    this.logs = const [],
  });

  final bool isLoading;
  final String? error;
  final List<Map<String, dynamic>> logs;

  SeniorAccessLogState copyWith({
    bool? isLoading,
    String? error,
    List<Map<String, dynamic>>? logs,
  }) {
    return SeniorAccessLogState(
      isLoading: isLoading ?? this.isLoading,
      error: error,
      logs: logs ?? this.logs,
    );
  }
}

class SeniorAccessLogNotifier extends StateNotifier<SeniorAccessLogState> {
  SeniorAccessLogNotifier({required this.apiClient, this.seniorUserId})
    : super(const SeniorAccessLogState()) {
    loadAccessLogs();
  }

  final ApiClient apiClient;
  final String? seniorUserId;

  Future<void> loadAccessLogs() async {
    state = state.copyWith(isLoading: true, error: null);

    try {
      // This app never stores the caller's own userId client-side
      // (server-side-only authorization) — viewing your own log goes
      // through /me/access-logs; viewing a specific senior's log (as an
      // authorized caregiver) uses the id-addressed route.
      final path = seniorUserId == null
          ? '/api/v1/family/me/access-logs'
          : '/api/v1/family/seniors/$seniorUserId/access-logs';
      final response = await apiClient.get<List<dynamic>>(
        path,
        queryParameters: {'days': 30},
      );

      final logs = response
          .map((e) => Map<String, dynamic>.from(e as Map))
          .toList();
      state = state.copyWith(logs: logs, isLoading: false);
    } catch (_) {
      state = state.copyWith(isLoading: false, error: 'errors.generic'.tr());
    }
  }
}

class SeniorAccessLogParams {
  const SeniorAccessLogParams({required this.apiClient, this.seniorUserId});

  final ApiClient apiClient;
  final String? seniorUserId;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is SeniorAccessLogParams &&
          identical(other.apiClient, apiClient) &&
          other.seniorUserId == seniorUserId);

  @override
  int get hashCode => Object.hash(apiClient, seniorUserId);
}

final seniorAccessLogProvider = StateNotifierProvider.autoDispose
    .family<
      SeniorAccessLogNotifier,
      SeniorAccessLogState,
      SeniorAccessLogParams
    >(
      (ref, params) => SeniorAccessLogNotifier(
        apiClient: params.apiClient,
        seniorUserId: params.seniorUserId,
      ),
    );
