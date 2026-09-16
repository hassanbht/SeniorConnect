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
  });

  final String? selectedCategory;
  final bool isLoading;
  final List<Map<String, dynamic>> events;
  final bool isNearbyMode;
  // True when the caller has no saved location yet — the UI should point
  // them at their profile instead of retrying.
  final bool nearbyUnavailable;

  CommunityFeedState copyWith({
    Object? selectedCategory = _sentinel,
    bool? isLoading,
    List<Map<String, dynamic>>? events,
    bool? isNearbyMode,
    bool? nearbyUnavailable,
  }) {
    return CommunityFeedState(
      selectedCategory: selectedCategory == _sentinel
          ? this.selectedCategory
          : selectedCategory as String?,
      isLoading: isLoading ?? this.isLoading,
      events: events ?? this.events,
      isNearbyMode: isNearbyMode ?? this.isNearbyMode,
      nearbyUnavailable: nearbyUnavailable ?? this.nearbyUnavailable,
    );
  }
}

const _sentinel = Object();

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

    state = state.copyWith(
      isNearbyMode: true,
      isLoading: true,
      nearbyUnavailable: false,
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
    } catch (_) {
      state = state.copyWith(isLoading: false, events: const []);
    }
  }

  Future<void> loadEvents() async {
    state = state.copyWith(isLoading: true);

    try {
      if (apiClient != null) {
        final cat = state.selectedCategory;
        final query = cat != null && cat != 'all' ? '?category=$cat' : '';
        final response = await apiClient!.get<List<dynamic>>(
          '/api/v1/community/events$query',
        );
        final events = response
            .map((e) => Map<String, dynamic>.from(e as Map))
            .toList();
        state = state.copyWith(events: events, isLoading: false);
        return;
      }
    } catch (_) {}

    // Fallback demo items — same field shape the real API returns
    // (CommunityEventDto), so the screen renders both identically.
    var demoEvents = [
      {
        'id': 'ev-1',
        'title': 'Senioren-Schachtreff',
        'category': 'sports',
        'startsAtUtc': DateTime.now()
            .add(const Duration(days: 2, hours: 15))
            .toIso8601String(),
        'locationAddress': 'Gemeindezentrum Mitte',
        'capacity': 8,
        'goingCount': 5,
      },
      {
        'id': 'ev-2',
        'title': 'Gemeinsames Kaffeetrinken & Plaudern',
        'category': 'general',
        'startsAtUtc': DateTime.now()
            .add(const Duration(days: 4, hours: 14))
            .toIso8601String(),
        'locationAddress': 'Café Sonnenschein',
        'capacity': 6,
        'goingCount': 6,
        'waitlistCount': 2,
      },
      {
        'id': 'ev-3',
        'title': 'Gedächtnistraining & Rätselspaß',
        'category': 'culture',
        'startsAtUtc': DateTime.now()
            .add(const Duration(days: 6, hours: 10))
            .toIso8601String(),
        'locationAddress': 'Stadtbibliothek',
        'capacity': 10,
        'goingCount': 5,
      },
    ];
    if (state.selectedCategory != null && state.selectedCategory != 'all') {
      demoEvents = demoEvents
          .where((e) => e['category'] == state.selectedCategory)
          .toList();
    }
    state = state.copyWith(events: demoEvents, isLoading: false);
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
