import 'package:easy_localization/easy_localization.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';

class AssignmentDetails {
  const AssignmentDetails({
    required this.categoryNameKey,
    required this.address,
    required this.notes,
    required this.seniorDisplayName,
    required this.seniorPhone,
    required this.rowVersion,
  });

  final String categoryNameKey;
  final String? address;
  final String? notes;
  final String? seniorDisplayName;
  final String? seniorPhone;
  final int rowVersion;
}

class ActiveAssignmentState {
  const ActiveAssignmentState({
    this.isLoading = true,
    this.isCheckedIn = false,
    this.isCompleted = false,
    this.isActionInProgress = false,
    this.loadError,
    this.actionError,
    this.details,
  });

  final bool isLoading;
  final bool isCheckedIn;
  final bool isCompleted;
  final bool isActionInProgress;
  final String? loadError;
  final String? actionError;
  final AssignmentDetails? details;

  ActiveAssignmentState copyWith({
    bool? isLoading,
    bool? isCheckedIn,
    bool? isCompleted,
    bool? isActionInProgress,
    Object? loadError = _sentinel,
    Object? actionError = _sentinel,
    Object? details = _sentinel,
  }) {
    return ActiveAssignmentState(
      isLoading: isLoading ?? this.isLoading,
      isCheckedIn: isCheckedIn ?? this.isCheckedIn,
      isCompleted: isCompleted ?? this.isCompleted,
      isActionInProgress: isActionInProgress ?? this.isActionInProgress,
      loadError:
          loadError == _sentinel ? this.loadError : loadError as String?,
      actionError:
          actionError == _sentinel ? this.actionError : actionError as String?,
      details: details == _sentinel
          ? this.details
          : details as AssignmentDetails?,
    );
  }
}

const _sentinel = Object();

class ActiveAssignmentNotifier extends StateNotifier<ActiveAssignmentState> {
  ActiveAssignmentNotifier({
    required this.apiClient,
    this.assignmentId,
  }) : super(const ActiveAssignmentState()) {
    loadAssignment();
  }

  final ApiClient apiClient;
  final String? assignmentId;

  Future<void> loadAssignment() async {
    if (assignmentId == null) {
      state = state.copyWith(
        isLoading: false,
        loadError: 'errors.generic'.tr(),
      );
      return;
    }

    state = state.copyWith(isLoading: true, loadError: null);

    try {
      final requestData = await apiClient.get<dynamic>(
        '/api/v1/help-requests/$assignmentId',
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

      final details = AssignmentDetails(
        categoryNameKey: categoryNameKey,
        address: m['locationAddress'] as String?,
        notes: m['notes'] as String?,
        seniorDisplayName: m['seniorDisplayName'] as String?,
        seniorPhone: m['seniorPhone'] as String?,
        rowVersion: (m['rowVersion'] as num?)?.toInt() ?? 0,
      );

      final status = m['status'] as String?;

      state = state.copyWith(
        details: details,
        isCheckedIn: status == 'InProgress' || status == 'Completed',
        isCompleted: status == 'Completed',
        isLoading: false,
      );
    } catch (_) {
      state = state.copyWith(
        isLoading: false,
        loadError: 'errors.generic'.tr(),
      );
    }
  }

  Future<bool> handleCheckIn() async {
    state = state.copyWith(isActionInProgress: true, actionError: null);
    try {
      await apiClient.post<dynamic>(
        '/api/v1/help-requests/$assignmentId:check-in',
      );
      state = state.copyWith(
        isCheckedIn: true,
        isActionInProgress: false,
      );
      return true;
    } catch (_) {
      state = state.copyWith(
        isActionInProgress: false,
        actionError: 'errors.generic'.tr(),
      );
      return false;
    }
  }

  Future<bool> handleComplete() async {
    state = state.copyWith(isActionInProgress: true, actionError: null);
    try {
      await apiClient.post<dynamic>(
        '/api/v1/help-requests/$assignmentId:complete',
        data: {'actualDurationMinutes': 60},
      );
      state = state.copyWith(
        isCompleted: true,
        isActionInProgress: false,
      );
      return true;
    } catch (_) {
      state = state.copyWith(
        isActionInProgress: false,
        actionError: 'errors.generic'.tr(),
      );
      return false;
    }
  }
}

class ActiveAssignmentParams {
  const ActiveAssignmentParams({
    required this.apiClient,
    this.assignmentId,
  });

  final ApiClient apiClient;
  final String? assignmentId;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is ActiveAssignmentParams &&
          identical(other.apiClient, apiClient) &&
          other.assignmentId == assignmentId);

  @override
  int get hashCode => Object.hash(apiClient, assignmentId);
}

final activeAssignmentProvider = StateNotifierProvider.autoDispose.family<
    ActiveAssignmentNotifier, ActiveAssignmentState, ActiveAssignmentParams>(
  (ref, params) {
    return ActiveAssignmentNotifier(
      apiClient: params.apiClient,
      assignmentId: params.assignmentId,
    );
  },
);
