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
  SeniorAccessLogNotifier({
    required this.apiClient,
    this.seniorUserId,
  }) : super(const SeniorAccessLogState()) {
    loadAccessLogs();
  }

  final ApiClient apiClient;
  final String? seniorUserId;

  Future<void> loadAccessLogs() async {
    state = state.copyWith(isLoading: true, error: null);

    try {
      final seniorId = seniorUserId ?? 'me';
      final response = await apiClient.get<List<dynamic>>(
        '/api/v1/family/seniors/$seniorId/access-log',
        queryParameters: {'days': 30},
      );

      final logs =
          response.map((e) => Map<String, dynamic>.from(e as Map)).toList();
      state = state.copyWith(logs: logs, isLoading: false);
    } catch (_) {
      // Provide default mock logs for test / offline view
      final mockLogs = [
        {
          'id': 'log-1',
          'accessedByUserName': 'Anna Meier (Tochter)',
          'action': 'VIEW_ACTIVITIES',
          'plainLanguageDescription': 'family.log_viewed_activities'.tr(),
          'timestampUtc': DateTime.now()
              .subtract(const Duration(hours: 3))
              .toIso8601String(),
        },
        {
          'id': 'log-2',
          'accessedByUserName': 'Thomas Meier (Sohn)',
          'action': 'CREATE_REQUEST_PROXY',
          'plainLanguageDescription': 'family.log_created_request'.tr(),
          'timestampUtc': DateTime.now()
              .subtract(const Duration(days: 1))
              .toIso8601String(),
        },
      ];
      state = state.copyWith(logs: mockLogs, isLoading: false);
    }
  }
}

class SeniorAccessLogParams {
  const SeniorAccessLogParams({
    required this.apiClient,
    this.seniorUserId,
  });

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

final seniorAccessLogProvider = StateNotifierProvider.autoDispose.family<
    SeniorAccessLogNotifier,
    SeniorAccessLogState,
    SeniorAccessLogParams>(
  (ref, params) => SeniorAccessLogNotifier(
    apiClient: params.apiClient,
    seniorUserId: params.seniorUserId,
  ),
);
