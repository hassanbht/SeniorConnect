// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
import '../../../core/network/api_client.dart';
part 'active_assignment_notifier.freezed.dart';
part 'active_assignment_notifier.g.dart';

@freezed
class AssignmentDetails with _ {
  const factory AssignmentDetails({
    required String categoryNameKey,
    String? address,
    String? notes,
    String? seniorDisplayName,
    String? seniorPhone,
    required int rowVersion,
  }) = _AssignmentDetails;
}

@freezed
class ActiveAssignmentState with _ {
  const factory ActiveAssignmentState({
    @Default(true) bool isLoading,
    @Default(false) bool isCheckedIn,
    @Default(false) bool isCompleted,
    @Default(false) bool isActionInProgress,
    String? loadError,
    String? actionError,
    AssignmentDetails? details,
  }) = _ActiveAssignmentState;
}

@riverpod
class ActiveAssignmentNotifier extends _ {
  @override
  ActiveAssignmentState build(ApiClient apiClient, String? assignmentId) {
    Future(() => _load(apiClient, assignmentId));
    return const ActiveAssignmentState();
  }

  Future<void> _load(ApiClient apiClient, String? id) async {
    if (id == null) {
      state = const ActiveAssignmentState(
        isLoading: false,
        loadError: 'errors.generic',
      );
      return;
    }
    state = const ActiveAssignmentState(isLoading: true);
    try {
      final m = await apiClient.get<Map<String, dynamic>>(
        '/api/v1/help-requests/',
      );
      final cats = await apiClient.get<List<dynamic>>(
        '/api/v1/activities/categories',
      );
      var catKey = 'help.category.shopping';
      final catId = m['categoryId'] as String?;
      if (catId != null) {
        final match = cats
            .cast<Map<String, dynamic>>()
            .where((c) => c['id'] == catId)
            .firstOrNull;
        if (match != null) catKey = 'help.category.';
      }
      state = ActiveAssignmentState(
        isLoading: false,
        isCheckedIn: m['status'] == 'CheckedIn',
        isCompleted: m['status'] == 'Completed',
        details: AssignmentDetails(
          categoryNameKey: catKey,
          address: m['address'] as String?,
          notes: m['notes'] as String?,
          seniorDisplayName: m['seniorDisplayName'] as String?,
          seniorPhone: m['seniorPhone'] as String?,
          rowVersion: m['rowVersion'] as int? ?? 0,
        ),
      );
    } catch (_) {
      state = const ActiveAssignmentState(
        isLoading: false,
        loadError: 'errors.generic',
      );
    }
  }

  Future<void> checkIn(String id, ApiClient apiClient) async {
    state = state.copyWith(isActionInProgress: true, actionError: null);
    try {
      await apiClient.post<dynamic>('/api/v1/help-requests/-in', data: {});
      state = state.copyWith(isCheckedIn: true, isActionInProgress: false);
    } catch (_) {
      state = state.copyWith(
        actionError: 'errors.generic',
        isActionInProgress: false,
      );
    }
  }

  Future<void> complete(String id, ApiClient apiClient) async {
    state = state.copyWith(isActionInProgress: true, actionError: null);
    try {
      await apiClient.post<dynamic>('/api/v1/help-requests/', data: {});
      state = state.copyWith(isCompleted: true, isActionInProgress: false);
    } catch (_) {
      state = state.copyWith(
        actionError: 'errors.generic',
        isActionInProgress: false,
      );
    }
  }
}
