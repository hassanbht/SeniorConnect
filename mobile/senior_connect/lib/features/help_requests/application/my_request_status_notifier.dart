import 'package:easy_localization/easy_localization.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';

class RequestStatusDetails {
  const RequestStatusDetails({
    required this.status,
    required this.categoryNameKey,
    required this.scheduledStartUtc,
    required this.notes,
    required this.volunteerDisplayName,
    required this.volunteerPhone,
  });

  final String status;
  final String categoryNameKey;
  final DateTime? scheduledStartUtc;
  final String? notes;
  final String? volunteerDisplayName;
  final String? volunteerPhone;
}

class MyRequestStatusState {
  const MyRequestStatusState({
    this.isLoading = true,
    this.loadError,
    this.details,
  });

  final bool isLoading;
  final String? loadError;
  final RequestStatusDetails? details;

  MyRequestStatusState copyWith({
    bool? isLoading,
    Object? loadError = _sentinel,
    Object? details = _sentinel,
  }) {
    return MyRequestStatusState(
      isLoading: isLoading ?? this.isLoading,
      loadError:
          loadError == _sentinel ? this.loadError : loadError as String?,
      details: details == _sentinel
          ? this.details
          : details as RequestStatusDetails?,
    );
  }
}

const _sentinel = Object();

class MyRequestStatusNotifier extends StateNotifier<MyRequestStatusState> {
  MyRequestStatusNotifier({
    required this.apiClient,
    this.requestId,
  }) : super(const MyRequestStatusState()) {
    loadStatus();
  }

  final ApiClient apiClient;
  final String? requestId;

  Future<void> loadStatus() async {
    if (requestId == null) {
      state = state.copyWith(
        isLoading: false,
        loadError: 'errors.generic'.tr(),
      );
      return;
    }

    state = state.copyWith(isLoading: true, loadError: null);

    try {
      final requestData = await apiClient.get<dynamic>(
        '/api/v1/help-requests/$requestId',
      );
      final categoriesData = await apiClient.get<dynamic>(
        '/api/v1/activities/categories',
      );

      final m = requestData as Map<String, dynamic>;
      var categoryNameKey = 'help.category.shopping';
      if (categoriesData is List) {
        for (final c in categoriesData) {
          final cm = c as Map<String, dynamic>;
          if (cm['id'] == m['categoryId']) {
            categoryNameKey = cm['nameKey'] as String? ?? categoryNameKey;
            break;
          }
        }
      }

      final details = RequestStatusDetails(
        status: m['status'] as String? ?? 'Open',
        categoryNameKey: categoryNameKey,
        scheduledStartUtc:
            DateTime.tryParse(m['scheduledStartUtc'] as String? ?? ''),
        notes: m['notes'] as String?,
        volunteerDisplayName: m['volunteerDisplayName'] as String?,
        volunteerPhone: m['volunteerPhone'] as String?,
      );

      state = state.copyWith(isLoading: false, details: details);
    } catch (_) {
      state = state.copyWith(
        isLoading: false,
        loadError: 'errors.generic'.tr(),
      );
    }
  }
}

class MyRequestStatusParams {
  const MyRequestStatusParams({
    required this.apiClient,
    this.requestId,
  });

  final ApiClient apiClient;
  final String? requestId;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is MyRequestStatusParams &&
          identical(other.apiClient, apiClient) &&
          other.requestId == requestId);

  @override
  int get hashCode => Object.hash(apiClient, requestId);
}

final myRequestStatusProvider = StateNotifierProvider.autoDispose
    .family<MyRequestStatusNotifier, MyRequestStatusState, MyRequestStatusParams>(
  (ref, params) {
    return MyRequestStatusNotifier(
      apiClient: params.apiClient,
      requestId: params.requestId,
    );
  },
);
