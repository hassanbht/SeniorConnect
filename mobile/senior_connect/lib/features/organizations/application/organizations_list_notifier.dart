import 'package:flutter_riverpod/flutter_riverpod.dart'
    hide AsyncLoading, AsyncError;

import '../../../core/network/api_client.dart';
import '../../../shared/riverpod/async_state.dart';

class OrganizationsListNotifier
    extends StateNotifier<AsyncState<List<Map<String, dynamic>>>> {
  OrganizationsListNotifier({required this.apiClient})
      : super(const AsyncLoading()) {
    load();
  }

  final ApiClient apiClient;

  Future<void> load() async {
    state = const AsyncLoading();
    try {
      final response =
          await apiClient.get<List<dynamic>>('/api/v1/organizations');
      final list = response
          .map((e) => Map<String, dynamic>.from(e as Map))
          .toList();
      if (list.isEmpty) {
        state = const AsyncEmpty();
      } else {
        state = AsyncLoaded(list);
      }
    } catch (e) {
      state = AsyncError(e.toString());
    }
  }
}

final organizationsListProvider = StateNotifierProvider.autoDispose.family<
    OrganizationsListNotifier,
    AsyncState<List<Map<String, dynamic>>>,
    ApiClient>(
  (ref, apiClient) => OrganizationsListNotifier(apiClient: apiClient),
);
