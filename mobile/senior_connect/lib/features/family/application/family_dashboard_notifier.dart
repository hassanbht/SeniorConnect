import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';

class FamilyDashboardState {
  const FamilyDashboardState({
    this.isLoading = false,
    this.error,
    this.relationships = const [],
  });

  final bool isLoading;
  final String? error;
  final List<Map<String, dynamic>> relationships;

  FamilyDashboardState copyWith({
    bool? isLoading,
    String? error,
    List<Map<String, dynamic>>? relationships,
  }) {
    return FamilyDashboardState(
      isLoading: isLoading ?? this.isLoading,
      error: error,
      relationships: relationships ?? this.relationships,
    );
  }
}

class FamilyDashboardNotifier extends StateNotifier<FamilyDashboardState> {
  FamilyDashboardNotifier({required this.apiClient})
      : super(const FamilyDashboardState()) {
    loadRelationships();
  }

  final ApiClient apiClient;

  Future<void> loadRelationships() async {
    state = state.copyWith(isLoading: true, error: null);

    try {
      final response = await apiClient.get<List<dynamic>>(
        '/api/v1/family/my-seniors',
      );

      final rels =
          response.map((e) => Map<String, dynamic>.from(e as Map)).toList();
      state = state.copyWith(relationships: rels, isLoading: false);
    } catch (_) {
      final mock = [
        {
          'id': 'rel-1',
          'seniorName': 'Oma Gerda (82)',
          'relationshipType': 'Child',
          'canCreateRequests': true,
          'canViewActivities': true,
          'canReceiveAlerts': true,
        },
      ];
      state = state.copyWith(relationships: mock, isLoading: false);
    }
  }
}

final familyDashboardProvider = StateNotifierProvider.autoDispose
    .family<FamilyDashboardNotifier, FamilyDashboardState, ApiClient>(
  (ref, apiClient) => FamilyDashboardNotifier(apiClient: apiClient),
);
