// lib/features/help_requests/application/volunteer_feed_notifier.dart
// DO NOT use setState in the screen. See AGENTS.md §State Management.

import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
import '../../../core/network/api_client.dart';

part 'volunteer_feed_notifier.freezed.dart';
part 'volunteer_feed_notifier.g.dart';

@freezed
class VolunteerFeedItem with _$VolunteerFeedItem {
  const factory VolunteerFeedItem({
    required String id,
    required String title,
    required String categoryName,
    required double distanceKm,
    required String scheduledTime,
    required int durationMinutes,
    required int rowVersion,
    @Default(true) bool isEligible,
    String? ineligibleReason,
  }) = _VolunteerFeedItem;
}

@freezed
class VolunteerFeedState with _$VolunteerFeedState {
  const factory VolunteerFeedState({
    @Default(true) bool isLoading,
    @Default([]) List<VolunteerFeedItem> requests,
    @Default(10.0) double maxDistance,
    String? acceptingId,
    String? errorMessage,
  }) = _VolunteerFeedState;
}

@riverpod
class VolunteerFeedNotifier extends _$VolunteerFeedNotifier {
  @override
  VolunteerFeedState build(ApiClient apiClient) {
    Future(() => loadFeed(apiClient));
    return const VolunteerFeedState();
  }

  Future<void> loadFeed(ApiClient apiClient) async {
    state = state.copyWith(isLoading: true, errorMessage: null);
    try {
      final resp = await apiClient.get<List<dynamic>>(
        '/api/v1/volunteer/feed',
        queryParameters: {'maxDistanceKm': state.maxDistance},
      );
      final items = resp.map((e) {
        final m = Map<String, dynamic>.from(e as Map);
        return VolunteerFeedItem(
          id: m['id'] as String,
          title: m['title'] as String? ?? '',
          categoryName: m['categoryName'] as String? ?? '',
          distanceKm: (m['distanceKm'] as num?)?.toDouble() ?? 0,
          scheduledTime: m['scheduledTime'] as String? ?? '',
          durationMinutes: m['durationMinutes'] as int? ?? 60,
          rowVersion: m['rowVersion'] as int? ?? 0,
          isEligible: m['isEligible'] as bool? ?? true,
          ineligibleReason: m['ineligibleReason'] as String?,
        );
      }).toList();
      state = state.copyWith(requests: items, isLoading: false);
    } catch (_) {
      state = state.copyWith(isLoading: false, errorMessage: 'errors.generic');
    }
  }

  void setMaxDistance(double km, ApiClient apiClient) {
    state = state.copyWith(maxDistance: km);
    loadFeed(apiClient);
  }

  void setAccepting(String? id) => state = state.copyWith(acceptingId: id);
}
