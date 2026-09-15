// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
import '../../../core/network/api_client.dart';
part 'my_request_status_notifier.freezed.dart';
part 'my_request_status_notifier.g.dart';

@freezed
class RequestStatusDetails with _ {
  const factory RequestStatusDetails({
    required String status,
    required String categoryNameKey,
    DateTime? scheduledStartUtc,
    String? notes,
    String? volunteerDisplayName,
    String? volunteerPhone,
  }) = _RequestStatusDetails;
}

@freezed
class MyRequestStatusState with _ {
  const factory MyRequestStatusState({
    @Default(true) bool isLoading,
    RequestStatusDetails? details,
    String? loadError,
  }) = _MyRequestStatusState;
}

@riverpod
class MyRequestStatusNotifier extends _ {
  @override
  MyRequestStatusState build(ApiClient apiClient, String? requestId) {
    Future(() => _load(apiClient, requestId));
    return const MyRequestStatusState();
  }

  Future<void> _load(ApiClient apiClient, String? requestId) async {
    if (requestId == null) {
      state = const MyRequestStatusState(
        isLoading: false,
        loadError: 'errors.generic',
      );
      return;
    }
    state = const MyRequestStatusState(isLoading: true);
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
      state = MyRequestStatusState(
        isLoading: false,
        details: RequestStatusDetails(
          status: m['status'] as String? ?? 'Pending',
          categoryNameKey: catKey,
          scheduledStartUtc: m['scheduledStartUtc'] != null
              ? DateTime.tryParse(m['scheduledStartUtc'] as String)
              : null,
          notes: m['notes'] as String?,
          volunteerDisplayName: m['volunteerDisplayName'] as String?,
          volunteerPhone: m['volunteerPhone'] as String?,
        ),
      );
    } catch (_) {
      state = const MyRequestStatusState(
        isLoading: false,
        loadError: 'errors.generic',
      );
    }
  }
}
