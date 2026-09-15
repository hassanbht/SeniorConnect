import 'package:easy_localization/easy_localization.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';

class HelpRequestFeedItem {
  const HelpRequestFeedItem({
    required this.id,
    required this.title,
    required this.categoryName,
    required this.distanceKm,
    required this.scheduledTime,
    required this.durationMinutes,
    required this.rowVersion,
    this.isEligible = true,
    this.ineligibleReason,
  });

  final String id;
  final String title;
  final String categoryName;
  final double distanceKm;
  final String scheduledTime;
  final int durationMinutes;
  final int rowVersion;
  final bool isEligible;
  final String? ineligibleReason;
}

class VolunteerFeedState {
  const VolunteerFeedState({
    this.isLoading = true,
    this.errorMessage,
    this.requests = const [],
    this.maxDistance = 10.0,
    this.acceptingId,
  });

  final bool isLoading;
  final String? errorMessage;
  final List<HelpRequestFeedItem> requests;
  final double maxDistance;
  final String? acceptingId;

  VolunteerFeedState copyWith({
    bool? isLoading,
    Object? errorMessage = _sentinel,
    List<HelpRequestFeedItem>? requests,
    double? maxDistance,
    Object? acceptingId = _sentinel,
  }) {
    return VolunteerFeedState(
      isLoading: isLoading ?? this.isLoading,
      errorMessage:
          errorMessage == _sentinel ? this.errorMessage : errorMessage as String?,
      requests: requests ?? this.requests,
      maxDistance: maxDistance ?? this.maxDistance,
      acceptingId:
          acceptingId == _sentinel ? this.acceptingId : acceptingId as String?,
    );
  }
}

const _sentinel = Object();

class VolunteerFeedNotifier extends StateNotifier<VolunteerFeedState> {
  VolunteerFeedNotifier({required this.apiClient})
      : super(const VolunteerFeedState()) {
    loadFeed();
  }

  final ApiClient apiClient;

  Future<void> setMaxDistance(double distance) async {
    state = state.copyWith(maxDistance: distance);
    await loadFeed();
  }

  Future<void> loadFeed() async {
    state = state.copyWith(isLoading: true, errorMessage: null);

    try {
      final data = await apiClient.get<dynamic>(
        '/api/v1/matching/feed',
        queryParameters: {'radiusKm': state.maxDistance},
      );

      if (data is! List) {
        throw const FormatException('Unexpected feed response shape');
      }

      final items = data.map((item) {
        final m = item as Map<String, dynamic>;
        final startUtc = DateTime.tryParse(
          m['scheduledStartUtc'] as String? ?? '',
        )?.toLocal();

        return HelpRequestFeedItem(
          id: m['helpRequestId'] as String? ?? '',
          title: (m['categoryNameKey'] as String? ?? 'help.category.shopping').tr(),
          categoryName:
              (m['categoryNameKey'] as String? ?? 'help.category.shopping').tr(),
          distanceKm: (m['distanceKm'] as num?)?.toDouble() ?? 0,
          scheduledTime: startUtc == null
              ? ''
              : '${startUtc.day}.${startUtc.month}., ${startUtc.hour.toString().padLeft(2, '0')}:${startUtc.minute.toString().padLeft(2, '0')}',
          durationMinutes: (m['durationMinutes'] as num?)?.toInt() ?? 60,
          rowVersion: (m['rowVersion'] as num?)?.toInt() ?? 0,
          isEligible: m['isEligible'] as bool? ?? false,
          ineligibleReason: m['ineligibilityReason'] as String?,
        );
      }).toList();

      state = state.copyWith(isLoading: false, requests: items);
    } catch (_) {
      state = state.copyWith(
        isLoading: false,
        errorMessage: 'errors.generic'.tr(),
      );
    }
  }

  Future<bool> acceptRequest(HelpRequestFeedItem item) async {
    state = state.copyWith(acceptingId: item.id);
    try {
      await apiClient.post<dynamic>(
        '/api/v1/help-requests/${item.id}:accept',
        data: {'expectedRowVersion': item.rowVersion},
      );
      state = state.copyWith(acceptingId: null);
      return true;
    } catch (_) {
      state = state.copyWith(acceptingId: null);
      return false;
    }
  }
}

final volunteerFeedProvider = StateNotifierProvider.autoDispose
    .family<VolunteerFeedNotifier, VolunteerFeedState, ApiClient>(
  (ref, apiClient) {
    return VolunteerFeedNotifier(apiClient: apiClient);
  },
);
