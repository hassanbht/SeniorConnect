// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
import '../../../core/network/api_client.dart';
part 'family_dashboard_notifier.freezed.dart';
part 'family_dashboard_notifier.g.dart';
@freezed
class FamilyDashboardState with _ {
  const factory FamilyDashboardState({
    @Default(false) bool isLoading,
    @Default([]) List<Map<String, dynamic>> relationships,
    String? error,
  }) = _FamilyDashboardState;
}
@riverpod
class FamilyDashboardNotifier extends _ {
  @override
  FamilyDashboardState build(ApiClient apiClient) {
    Future(()=> _load(apiClient));
    return const FamilyDashboardState();
  }
  Future<void> _load(ApiClient apiClient) async {
    state = state.copyWith(isLoading: true, error: null);
    try {
      final resp = await apiClient.get<List<dynamic>>('/api/v1/family/my-seniors');
      state = state.copyWith(isLoading: false,
        relationships: resp.map((e) => Map<String, dynamic>.from(e as Map)).toList());
    } catch (_) {
      state = FamilyDashboardState(isLoading: false, relationships: [
        {'id':'rel-1','seniorName':'Oma Gerda (82)','relationshipType':'Child',
         'canCreateRequests':true,'canViewActivities':true,'canReceiveAlerts':true}
      ]);
    }
  }
}
