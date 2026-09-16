import 'package:dio/dio.dart';
import 'package:easy_localization/easy_localization.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';

class CoordinatorHoursQueueState {
  const CoordinatorHoursQueueState({
    this.isLoading = true,
    this.isBatchProcessing = false,
    this.errorMessage,
    this.unconfirmedActivities = const [],
  });

  final bool isLoading;
  final bool isBatchProcessing;
  final String? errorMessage;
  final List<dynamic> unconfirmedActivities;

  CoordinatorHoursQueueState copyWith({
    bool? isLoading,
    bool? isBatchProcessing,
    String? errorMessage,
    List<dynamic>? unconfirmedActivities,
  }) {
    return CoordinatorHoursQueueState(
      isLoading: isLoading ?? this.isLoading,
      isBatchProcessing: isBatchProcessing ?? this.isBatchProcessing,
      errorMessage: errorMessage,
      unconfirmedActivities:
          unconfirmedActivities ?? this.unconfirmedActivities,
    );
  }
}

class CoordinatorHoursQueueNotifier
    extends StateNotifier<CoordinatorHoursQueueState> {
  CoordinatorHoursQueueNotifier({
    required this.organizationId,
    required this.apiClient,
  }) : super(const CoordinatorHoursQueueState()) {
    loadQueue();
  }

  final String organizationId;
  final ApiClient apiClient;

  Future<void> loadQueue() async {
    state = state.copyWith(isLoading: true, errorMessage: null);

    try {
      final resp = await apiClient.get<dynamic>(
        '/api/v1/coordinator/attention/unconfirmed-hours?organizationId=$organizationId',
      );

      final list = resp is List ? resp : [];
      state = state.copyWith(isLoading: false, unconfirmedActivities: list);
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

  Future<String?> confirmActivity(String activityId) async {
    try {
      await apiClient.post<dynamic>(
        '/api/v1/activities/$activityId:confirm',
      );
      await loadQueue();
      return null;
    } on DioException catch (e) {
      return mapDioError(e).l10nKey;
    } catch (_) {
      return 'errors.generic';
    }
  }

  Future<int> confirmAll() async {
    if (state.unconfirmedActivities.isEmpty) return 0;

    state = state.copyWith(isBatchProcessing: true);
    int successCount = 0;

    for (final act in state.unconfirmedActivities) {
      final id = act['id'] as String?;
      if (id == null) continue;
      try {
        await apiClient.post<dynamic>('/api/v1/activities/$id:confirm');
        successCount++;
      } catch (_) {}
    }

    state = state.copyWith(isBatchProcessing: false);
    await loadQueue();
    return successCount;
  }

  Future<String?> disputeActivity(String activityId, String reason) async {
    try {
      await apiClient.post<dynamic>(
        '/api/v1/activities/$activityId:dispute',
        data: {'reason': reason},
      );
      await loadQueue();
      return null;
    } on DioException catch (e) {
      return mapDioError(e).l10nKey;
    } catch (_) {
      return 'errors.generic';
    }
  }
}

class CoordinatorHoursQueueParams {
  const CoordinatorHoursQueueParams({
    required this.organizationId,
    required this.apiClient,
  });

  final String organizationId;
  final ApiClient apiClient;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is CoordinatorHoursQueueParams &&
          other.organizationId == organizationId &&
          identical(other.apiClient, apiClient));

  @override
  int get hashCode => Object.hash(organizationId, apiClient);
}

final coordinatorHoursQueueProvider = StateNotifierProvider.autoDispose.family<
    CoordinatorHoursQueueNotifier,
    CoordinatorHoursQueueState,
    CoordinatorHoursQueueParams>(
  (ref, params) => CoordinatorHoursQueueNotifier(
    organizationId: params.organizationId,
    apiClient: params.apiClient,
  ),
);
