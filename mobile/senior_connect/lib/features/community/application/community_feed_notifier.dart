import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../../discovery/data/discovery_repository.dart';

class CommunityFeedState {
  const CommunityFeedState({
    this.selectedCategory,
    this.isLoading = false,
    this.events = const [],
    this.isNearbyMode = false,
    this.nearbyUnavailable = false,
    this.error,
  });

  final String? selectedCategory;
  final bool isLoading;
  final List<Map<String, dynamic>> events;
  final bool isNearbyMode;
  // True when the caller has no saved location yet — the UI should point
  // them at their profile instead of retrying.
  final bool nearbyUnavailable;

  /// Localization key; set when events could not be loaded.
  final String? error;

  CommunityFeedState copyWith({
    Object? selectedCategory = _sentinel,
    bool? isLoading,
    List<Map<String, dynamic>>? events,
    bool? isNearbyMode,
    bool? nearbyUnavailable,
    Object? error = _sentinel,
  }) {
    return CommunityFeedState(
      selectedCategory: selectedCategory == _sentinel
          ? this.selectedCategory
          : selectedCategory as String?,
      isLoading: isLoading ?? this.isLoading,
      events: events ?? this.events,
      isNearbyMode: isNearbyMode ?? this.isNearbyMode,
      nearbyUnavailable: nearbyUnavailable ?? this.nearbyUnavailable,
      error: error == _sentinel ? this.error : error as String?,
    );
  }
}

const _sentinel = Object();

String _errorKey(Object e) =>
    e is DioException ? mapDioError(e).l10nKey : 'errors.generic';

class CommunityFeedNotifier extends StateNotifier<CommunityFeedState> {
  CommunityFeedNotifier({
    this.apiClient,
    DiscoveryRepository? discoveryRepository,
  }) : _discoveryRepository = apiClient != null
           ? (discoveryRepository ?? DiscoveryRepositoryImpl(apiClient))
           : null,
       super(const CommunityFeedState()) {
    loadEvents();
  }

  final ApiClient? apiClient;
  final DiscoveryRepository? _discoveryRepository;

  static const categories = [
    'all',
    'sports',
    'general',
    'culture',
    'language_practice',
    'local_orientation',
  ];

  Future<void> toggleNearbyMode() async {
    if (state.isNearbyMode) {
      state = state.copyWith(isNearbyMode: false, nearbyUnavailable: false);
      await loadEvents();
      return;
    }
    state = state.copyWith(isNearbyMode: true);
    await _loadNearby();
  }

  /// Retries whichever list is currently shown.
  Future<void> retry() => state.isNearbyMode ? _loadNearby() : loadEvents();

  Future<void> _loadNearby() async {
    state = state.copyWith(
      isLoading: true,
      nearbyUnavailable: false,
      error: null,
    );

    final repository = _discoveryRepository;
    if (repository == null) {
      state = state.copyWith(isLoading: false, nearbyUnavailable: true);
      return;
    }

    try {
      final myLocation = await repository.getMyLocation();
      if (myLocation == null) {
        state = state.copyWith(isLoading: false, nearbyUnavailable: true);
        return;
      }

      final nearby = await repository.getNearbyCommunityEvents(
        myLocation.latitude,
        myLocation.longitude,
        25,
      );
      final events = nearby
          .map(
            (e) => {
              'id': e.eventId,
              'title': e.title,
              'category': e.category,
              'startsAtUtc': e.startsAtUtc.toIso8601String(),
              'distanceKm': e.distanceKm,
            },
          )
          .toList();
      state = state.copyWith(events: events, isLoading: false);
    } catch (e) {
      state = state.copyWith(
        isLoading: false,
        events: const [],
        error: _errorKey(e),
      );
    }
  }

  Future<void> loadEvents() async {
    final client = apiClient;
    if (client == null) {
      state = state.copyWith(isLoading: false, events: const [], error: null);
      return;
    }

    state = state.copyWith(isLoading: true, error: null);
    try {
      final cat = state.selectedCategory;
      final query = cat != null && cat != 'all' ? '?category=$cat' : '';
      final response = await client.get<List<dynamic>>(
        '/api/v1/community/events$query',
      );
      final events = response
          .map((e) => Map<String, dynamic>.from(e as Map))
          .toList();
      state = state.copyWith(events: events, isLoading: false);
    } catch (e) {
      state = state.copyWith(
        isLoading: false,
        events: const [],
        error: _errorKey(e),
      );
    }
  }

  void setCategory(String? category) {
    state = state.copyWith(selectedCategory: category);
    loadEvents();
  }
}

final communityFeedProvider = StateNotifierProvider.autoDispose
    .family<CommunityFeedNotifier, CommunityFeedState, ApiClient?>(
      (ref, apiClient) => CommunityFeedNotifier(apiClient: apiClient),
    );
