// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
import '../../../core/network/api_client.dart';
part 'community_feed_notifier.freezed.dart';
part 'community_feed_notifier.g.dart';

@freezed
class CommunityFeedState with _ {
  const factory CommunityFeedState({
    String? selectedCategory,
    @Default(false) bool isLoading,
    @Default([]) List<Map<String, dynamic>> events,
  }) = _CommunityFeedState;
}

@riverpod
class CommunityFeedNotifier extends _ {
  @override
  CommunityFeedState build(ApiClient? apiClient) {
    Future(() => _loadEvents(apiClient, null));
    return const CommunityFeedState();
  }

  Future<void> _loadEvents(ApiClient? apiClient, String? category) async {
    state = state.copyWith(isLoading: true);
    try {
      if (apiClient != null) {
        final q = category != null && category != 'all' ? '?category=' : '';
        final resp = await apiClient.get<List<dynamic>>(
          '/api/v1/community/events',
        );
        state = state.copyWith(
          events: resp.map((e) => Map<String, dynamic>.from(e as Map)).toList(),
          isLoading: false,
        );
        return;
      }
    } catch (_) {}
    state = state.copyWith(
      isLoading: false,
      events: const [
        {'id': 'ev-1', 'title': 'Senioren-Schachtreff', 'category': 'sports'},
        {
          'id': 'ev-2',
          'title': 'Gemeinsames Kaffeetrinken',
          'category': 'general',
        },
      ],
    );
  }

  void setCategory(String? cat, ApiClient? apiClient) {
    state = state.copyWith(selectedCategory: cat);
    _loadEvents(apiClient, cat);
  }
}
