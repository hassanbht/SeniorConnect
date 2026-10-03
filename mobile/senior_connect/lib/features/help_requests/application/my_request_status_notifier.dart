import 'package:easy_localization/easy_localization.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import 'active_assignment_notifier.dart' show partyUserId;
import '../../../core/cache/reference_data_repository.dart';

class RequestStatusDetails {
  const RequestStatusDetails({
    required this.status,
    required this.categoryNameKey,
    required this.scheduledStartUtc,
    required this.notes,
    required this.volunteerDisplayName,
    required this.volunteerPhone,
    required this.assignedVolunteerUserId,
    this.visitAddressGrantActive = false,
  });

  final String status;
  final String categoryNameKey;
  final DateTime? scheduledStartUtc;
  final String? notes;
  final String? volunteerDisplayName;
  final String? volunteerPhone;

  /// ADR-026 (legal validation pending): whether a live visit-address grant
  /// exists for the current assignment (consent metadata only, no address).
  final bool visitAddressGrantActive;

  /// Subject of a safeguarding concern raised by the senior; null (nobody
  /// assigned yet) hides the action.
  final String? assignedVolunteerUserId;
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
      loadError: loadError == _sentinel ? this.loadError : loadError as String?,
      details: details == _sentinel
          ? this.details
          : details as RequestStatusDetails?,
    );
  }
}

const _sentinel = Object();

class MyRequestStatusNotifier extends StateNotifier<MyRequestStatusState> {
  MyRequestStatusNotifier({required this.apiClient, this.requestId})
    : super(const MyRequestStatusState()) {
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

      final details = RequestStatusDetails(
        status: m['status'] as String? ?? 'Open',
        categoryNameKey: categoryNameKey,
        scheduledStartUtc: DateTime.tryParse(
          m['scheduledStartUtc'] as String? ?? '',
        ),
        notes: m['notes'] as String?,
        volunteerDisplayName: m['volunteerDisplayName'] as String?,
        volunteerPhone: m['volunteerPhone'] as String?,
        assignedVolunteerUserId: partyUserId(m['assignedVolunteerUserId']),
        visitAddressGrantActive: m['visitAddressGrantActive'] as bool? ?? false,
      );

      state = state.copyWith(isLoading: false, details: details);
    } catch (_) {
      state = state.copyWith(
        isLoading: false,
        loadError: 'errors.generic'.tr(),
      );
    }
  }

  /// ADR-026 (legal validation pending): the senior (or their authorized
  /// representative / coordinator-on-behalf on the audited path) releases the
  /// visit address for the CURRENT assignment only. The grant is
  /// assignment-bound and revocable until check-in; a new assignment starts
  /// without a grant. Never contains the address itself.
  Future<bool> grantVisitAddress() async {
    if (requestId == null) return false;
    try {
      await apiClient.post<dynamic>(
        '/api/v1/help-requests/$requestId/visit-address-grant',
        data: <String, dynamic>{},
      );
      await loadStatus();
      return true;
    } catch (_) {
      return false;
    }
  }

  /// Revokes the live grant (possible until check-in — after that, the visit
  /// happened). Idempotent on the backend.
  Future<bool> revokeVisitAddress([String? reason]) async {
    if (requestId == null) return false;
    try {
      await apiClient.delete<dynamic>(
        '/api/v1/help-requests/$requestId/visit-address-grant',
        data: <String, dynamic>{
          if (reason != null && reason.isNotEmpty) 'reason': reason,
        },
      );
      await loadStatus();
      return true;
    } catch (_) {
      return false;
    }
  }
}

class MyRequestStatusParams {
  const MyRequestStatusParams({required this.apiClient, this.requestId});

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
    .family<
      MyRequestStatusNotifier,
      MyRequestStatusState,
      MyRequestStatusParams
    >((ref, params) {
      return MyRequestStatusNotifier(
        apiClient: params.apiClient,
        requestId: params.requestId,
      );
    });
