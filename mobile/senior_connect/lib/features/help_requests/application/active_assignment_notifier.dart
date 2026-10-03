import 'package:easy_localization/easy_localization.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../../../core/cache/reference_data_repository.dart';

const _emptyGuid = '00000000-0000-0000-0000-000000000000';

/// A party's user id from a help-request DTO, or null when absent or masked
/// (the server sends Guid.Empty to non-parties). Never use a request id here.
String? partyUserId(Object? value) =>
    value is String && value.isNotEmpty && value != _emptyGuid ? value : null;

class AssignmentDetails {
  const AssignmentDetails({
    required this.categoryNameKey,
    required this.address,
    required this.notes,
    required this.seniorDisplayName,
    required this.seniorPhone,
    required this.rowVersion,
    required this.scheduledDurationMinutes,
    required this.seniorUserId,
  });

  final String categoryNameKey;
  final String? address;
  final String? notes;
  final String? seniorDisplayName;
  final String? seniorPhone;
  final int rowVersion;

  /// Planned length from the request; prefills the actual-duration prompt.
  final int scheduledDurationMinutes;

  /// Subject of a safeguarding concern raised by the volunteer; null hides
  /// the action rather than reporting the wrong person.
  final String? seniorUserId;
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
      loadError: loadError == _sentinel ? this.loadError : loadError as String?,
      actionError: actionError == _sentinel
          ? this.actionError
          : actionError as String?,
      details: details == _sentinel
          ? this.details
          : details as AssignmentDetails?,
    );
  }
}

const _sentinel = Object();

class ActiveAssignmentNotifier extends StateNotifier<ActiveAssignmentState> {
  ActiveAssignmentNotifier({required this.apiClient, this.assignmentId})
    : super(const ActiveAssignmentState()) {
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
      final categoriesData = await ReferenceDataRepository(
        apiClient,
      ).activityCategories();

      final m = requestData as Map<String, dynamic>;
      var categoryNameKey = 'help.category.shopping';
      for (final cm in categoriesData) {
        if (cm['id'] == m['categoryId']) {
          categoryNameKey = cm['nameKey'] as String? ?? categoryNameKey;
          break;
        }
      }

      final details = AssignmentDetails(
        categoryNameKey: categoryNameKey,
        address: m['locationAddress'] as String?,
        notes: m['notes'] as String?,
        seniorDisplayName: m['seniorDisplayName'] as String?,
        seniorPhone: m['seniorPhone'] as String?,
        rowVersion: (m['rowVersion'] as num?)?.toInt() ?? 0,
        scheduledDurationMinutes: (m['durationMinutes'] as num?)?.toInt() ?? 60,
        seniorUserId: partyUserId(m['seniorUserId']),
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
      state = state.copyWith(isCheckedIn: true, isActionInProgress: false);
      return true;
    } catch (_) {
      state = state.copyWith(
        isActionInProgress: false,
        actionError: 'errors.generic'.tr(),
      );
      return false;
    }
  }

  /// Completes the assignment with the duration the volunteer confirmed.
  Future<bool> handleComplete(int actualDurationMinutes) async {
    state = state.copyWith(isActionInProgress: true, actionError: null);
    try {
      await apiClient.post<dynamic>(
        '/api/v1/help-requests/$assignmentId:complete',
        data: {'actualDurationMinutes': actualDurationMinutes},
      );
      state = state.copyWith(isCompleted: true, isActionInProgress: false);
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
  const ActiveAssignmentParams({required this.apiClient, this.assignmentId});

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

final activeAssignmentProvider = StateNotifierProvider.autoDispose
    .family<
      ActiveAssignmentNotifier,
      ActiveAssignmentState,
      ActiveAssignmentParams
    >((ref, params) {
      return ActiveAssignmentNotifier(
        apiClient: params.apiClient,
        assignmentId: params.assignmentId,
      );
    });
