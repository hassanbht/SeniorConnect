import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';

class CommunityFeedState {
  const CommunityFeedState({
    this.selectedCategory,
    this.isLoading = false,
    this.events = const [],
  });

  final String? selectedCategory;
  final bool isLoading;
  final List<Map<String, dynamic>> events;

  CommunityFeedState copyWith({
    Object? selectedCategory = _sentinel,
    bool? isLoading,
    List<Map<String, dynamic>>? events,
  }) {
    return CommunityFeedState(
      selectedCategory: selectedCategory == _sentinel
          ? this.selectedCategory
          : selectedCategory as String?,
      isLoading: isLoading ?? this.isLoading,
      events: events ?? this.events,
    );
  }
}

const _sentinel = Object();

class CommunityFeedNotifier extends StateNotifier<CommunityFeedState> {
  CommunityFeedNotifier({this.apiClient}) : super(const CommunityFeedState()) {
    loadEvents();
  }

  final ApiClient? apiClient;

  static const categories = [
    'all',
    'sports',
    'general',
    'culture',
    'language_practice',
    'local_orientation',
  ];

  Future<void> loadEvents() async {
    state = state.copyWith(isLoading: true);

    try {
      if (apiClient != null) {
        final cat = state.selectedCategory;
        final query =
            cat != null && cat != 'all' ? '?category=$cat' : '';
        final response =
            await apiClient!.get<List<dynamic>>('/api/v1/community/events$query');
        final events =
            response.map((e) => Map<String, dynamic>.from(e as Map)).toList();
        state = state.copyWith(events: events, isLoading: false);
        return;
      }
    } catch (_) {}

    // Fallback demo items
    var demoEvents = [
      {
        'id': 'ev-1',
        'title': 'Senioren-Schachtreff',
        'category': 'sports',
        'date': 'Dienstag, 15:00 Uhr',
        'location': 'Gemeindezentrum Mitte',
        'spots': '3 Plätze frei',
      },
      {
        'id': 'ev-2',
        'title': 'Gemeinsames Kaffeetrinken & Plaudern',
        'category': 'general',
        'date': 'Donnerstag, 14:30 Uhr',
        'location': 'Café Sonnenschein',
        'spots': 'Ausgebucht (Warteliste)',
      },
      {
        'id': 'ev-3',
        'title': 'Gedächtnistraining & Rätselspaß',
        'category': 'culture',
        'date': 'Samstag, 10:00 Uhr',
        'location': 'Stadtbibliothek',
        'spots': '5 Plätze frei',
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
