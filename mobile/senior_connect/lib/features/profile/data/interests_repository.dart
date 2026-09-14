// lib/features/profile/data/interests_repository.dart
//
// P1-28: interest catalog (GET /api/v1/reference/interests) + the current
// user's selected interests (GET/PUT /api/v1/me/interests).

import '../../../core/network/api_client.dart';

class InterestOption {
  const InterestOption({
    required this.id,
    required this.key,
    required this.nameKey,
  });

  factory InterestOption.fromJson(Map<String, dynamic> json) => InterestOption(
        id: json['id'] as String,
        key: json['key'] as String,
        nameKey: json['name'] as String,
      );

  final String id;
  final String key;

  /// Translation key (e.g. "interest.walking"), not a display string — the
  /// backend seeds this as an easy_localization key (ADR-012).
  final String nameKey;
}

abstract class InterestsRepository {
  Future<List<InterestOption>> getCatalog();
  Future<List<InterestOption>> getSelected();
  Future<List<InterestOption>> updateSelected(List<String> interestIds);
}

class InterestsRepositoryImpl implements InterestsRepository {
  InterestsRepositoryImpl(this._apiClient);

  final ApiClient _apiClient;

  @override
  Future<List<InterestOption>> getCatalog() async {
    final response = await _apiClient.get<List<dynamic>>('/api/v1/reference/interests');
    return response
        .map((e) => InterestOption.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  @override
  Future<List<InterestOption>> getSelected() async {
    final response = await _apiClient.get<List<dynamic>>('/api/v1/me/interests');
    return response
        .map((e) => InterestOption.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  @override
  Future<List<InterestOption>> updateSelected(List<String> interestIds) async {
    final response = await _apiClient.put<List<dynamic>>(
      '/api/v1/me/interests',
      data: {'interestIds': interestIds},
    );
    return response
        .map((e) => InterestOption.fromJson(e as Map<String, dynamic>))
        .toList();
  }
}
