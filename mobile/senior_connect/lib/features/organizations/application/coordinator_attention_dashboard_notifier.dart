import 'package:dio/dio.dart';
import 'package:easy_localization/easy_localization.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';

class CoordinatorAttentionState {
  const CoordinatorAttentionState({
    this.isLoading = true,
    this.isSendingReminders = false,
    this.errorMessage,
    this.unconfirmedCount = 0,
    this.disputedCount = 0,
    this.pendingAppsCount = 0,
    this.expiringVerifications = const [],
    this.silentVolunteers = const [],
  });

  final bool isLoading;
  final bool isSendingReminders;
  final String? errorMessage;
  final int unconfirmedCount;
  final int disputedCount;
  final int pendingAppsCount;
  final List<dynamic> expiringVerifications;
  final List<dynamic> silentVolunteers;

  CoordinatorAttentionState copyWith({
    bool? isLoading,
    bool? isSendingReminders,
    String? errorMessage,
    int? unconfirmedCount,
    int? disputedCount,
    int? pendingAppsCount,
    List<dynamic>? expiringVerifications,
    List<dynamic>? silentVolunteers,
  }) {
    return CoordinatorAttentionState(
      isLoading: isLoading ?? this.isLoading,
      isSendingReminders: isSendingReminders ?? this.isSendingReminders,
      errorMessage: errorMessage,
      unconfirmedCount: unconfirmedCount ?? this.unconfirmedCount,
      disputedCount: disputedCount ?? this.disputedCount,
      pendingAppsCount: pendingAppsCount ?? this.pendingAppsCount,
      expiringVerifications:
          expiringVerifications ?? this.expiringVerifications,
      silentVolunteers: silentVolunteers ?? this.silentVolunteers,
    );
  }
}

class CoordinatorAttentionNotifier
    extends StateNotifier<CoordinatorAttentionState> {
  CoordinatorAttentionNotifier({
    required this.organizationId,
    required this.apiClient,
  }) : super(const CoordinatorAttentionState()) {
    loadDashboard();
  }

  final String organizationId;
  final ApiClient apiClient;

  Future<void> loadDashboard() async {
    state = state.copyWith(isLoading: true, errorMessage: null);

    try {
      final triageResp = await apiClient.get<dynamic>(
        '/api/v1/coordinator/triage?organizationId=$organizationId',
      );

      var unconfirmed = 0;
      var disputed = 0;
      var pendingApps = 0;

      if (triageResp is Map<String, dynamic>) {
        unconfirmed = triageResp['unconfirmedActivitiesCount'] as int? ?? 0;
        disputed = triageResp['disputedActivitiesCount'] as int? ?? 0;
        pendingApps = triageResp['pendingApplicationsCount'] as int? ?? 0;
      }

      var expiringVerifications = <dynamic>[];
      try {
        final expResp = await apiClient.get<dynamic>(
          '/api/v1/coordinator/attention/expiring-verifications',
        );
        if (expResp is List) {
          expiringVerifications = expResp;
        }
      } catch (_) {}

      var silentVolunteers = <dynamic>[];
      try {
        final silentResp = await apiClient.get<dynamic>(
          '/api/v1/coordinator/attention/silent-volunteers?organizationId=$organizationId',
        );
        if (silentResp is List) {
          silentVolunteers = silentResp;
        }
      } catch (_) {}

      state = state.copyWith(
        isLoading: false,
        unconfirmedCount: unconfirmed,
        disputedCount: disputed,
        pendingAppsCount: pendingApps,
        expiringVerifications: expiringVerifications,
        silentVolunteers: silentVolunteers,
      );
    } on DioException catch (e) {
      state = state.copyWith(
        isLoading: false,
        errorMessage: mapDioError(e).l10nKey.tr(),
      );
    } catch (_) {
      state = state.copyWith(
        isLoading: false,
        errorMessage: 'errors.generic'.tr(),
      );
    }
  }

  Future<int?> sendMonthlyReminders() async {
    state = state.copyWith(isSendingReminders: true);
    try {
      final resp = await apiClient.post<dynamic>(
        '/api/v1/coordinator/volunteers/reminders:send-monthly?organizationId=$organizationId',
      );
      final dispatched =
          resp is Map<String, dynamic> ? resp['remindersDispatched'] ?? 0 : 0;
      state = state.copyWith(isSendingReminders: false);
      await loadDashboard();
      return (dispatched as num).toInt();
    } catch (_) {
      state = state.copyWith(isSendingReminders: false);
      return null;
    }
  }
}

class CoordinatorAttentionParams {
  const CoordinatorAttentionParams({
    required this.organizationId,
    required this.apiClient,
  });

  final String organizationId;
  final ApiClient apiClient;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is CoordinatorAttentionParams &&
          other.organizationId == organizationId &&
          identical(other.apiClient, apiClient));

  @override
  int get hashCode => Object.hash(organizationId, apiClient);
}

final coordinatorAttentionProvider = StateNotifierProvider.autoDispose.family<
    CoordinatorAttentionNotifier,
    CoordinatorAttentionState,
    CoordinatorAttentionParams>(
  (ref, params) => CoordinatorAttentionNotifier(
    organizationId: params.organizationId,
    apiClient: params.apiClient,
  ),
);
