// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
import '../../../core/network/api_client.dart';
import '../data/intake_form_repository.dart';
part 'org_profile_notifier.freezed.dart';
part 'org_profile_notifier.g.dart';
@freezed
class OrgProfileState with _ {
  const factory OrgProfileState({
    @Default(true) bool isLoading,
    Map<String, dynamic>? organization,
    @Default([]) List<Map<String, dynamic>> newsItems,
    @Default([]) List<Map<String, dynamic>> events,
    @Default(false) bool canManage,
    @Default(false) bool isActivatingTemplate,
  }) = _OrgProfileState;
}
@riverpod
class OrgProfileNotifier extends _ {
  @override
  OrgProfileState build(ApiClient apiClient, String organizationId, IntakeFormRepository intakeFormRepo) {
    Future(()=> _load(apiClient, organizationId));
    return const OrgProfileState();
  }
  Future<void> _load(ApiClient apiClient, String orgId) async {
    state = const OrgProfileState(isLoading: true);
    try {
      final org = await apiClient.get<Map<String, dynamic>>('/api/v1/organizations/');
      final posts = await apiClient.get<List<dynamic>>('/api/v1/community/events', queryParameters: {'organizationId': orgId});
      final maps = posts.map((e) => Map<String, dynamic>.from(e as Map)).toList();
      var canManage = false;
      try {
        final mine = await apiClient.get<List<dynamic>>('/api/v1/me/organizations');
        canManage = mine.any((m) => Map<String,dynamic>.from(m as Map)['organizationId'] == orgId);
      } catch (_) {}
      state = OrgProfileState(isLoading: false, organization: org,
        newsItems: maps.where((p) => p['category'] == 'news').toList(),
        events: maps.where((p) => p['category'] != 'news').toList(), canManage: canManage);
    } catch (_) { state = const OrgProfileState(isLoading: false); }
  }
  void setActivatingTemplate(bool v) => state = state.copyWith(isActivatingTemplate: v);
}
