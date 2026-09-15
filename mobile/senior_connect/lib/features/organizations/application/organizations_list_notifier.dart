// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
import '../../../core/network/api_client.dart';
part 'organizations_list_notifier.freezed.dart';
part 'organizations_list_notifier.g.dart';
@freezed
class OrganizationsListState with _ {
  const factory OrganizationsListState({
    @Default(true) bool isLoading,
    @Default([]) List<Map<String, dynamic>> organizations,
  }) = _OrganizationsListState;
}
@riverpod
class OrganizationsListNotifier extends _ {
  @override
  OrganizationsListState build(ApiClient apiClient) {
    Future(()=> _load(apiClient));
    return const OrganizationsListState();
  }
  Future<void> _load(ApiClient apiClient) async {
    state = const OrganizationsListState(isLoading: true);
    try {
      final resp = await apiClient.get<List<dynamic>>('/api/v1/organizations');
      state = OrganizationsListState(isLoading: false,
        organizations: resp.map((e) => Map<String, dynamic>.from(e as Map)).toList());
    } catch (_) { state = const OrganizationsListState(isLoading: false); }
  }
}
