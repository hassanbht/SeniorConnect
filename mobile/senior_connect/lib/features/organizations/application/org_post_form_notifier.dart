import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';

enum OrganizationPostCategory { news, event }

class OrganizationPostFormState {
  const OrganizationPostFormState({
    this.category = OrganizationPostCategory.event,
    this.startsAt,
    this.isSubmitting = false,
    this.hasError = false,
  });

  final OrganizationPostCategory category;
  final DateTime? startsAt;
  final bool isSubmitting;
  final bool hasError;

  OrganizationPostFormState copyWith({
    OrganizationPostCategory? category,
    DateTime? startsAt,
    bool? isSubmitting,
    bool? hasError,
  }) {
    return OrganizationPostFormState(
      category: category ?? this.category,
      startsAt: startsAt ?? this.startsAt,
      isSubmitting: isSubmitting ?? this.isSubmitting,
      hasError: hasError ?? this.hasError,
    );
  }
}

class OrganizationPostFormNotifier
    extends StateNotifier<OrganizationPostFormState> {
  OrganizationPostFormNotifier({
    required this.organizationId,
    required this.apiClient,
    this.existingPost,
  }) : super(OrganizationPostFormState(
          category: existingPost?['category'] == 'news'
              ? OrganizationPostCategory.news
              : OrganizationPostCategory.event,
          startsAt: DateTime.now().add(const Duration(days: 7)),
        ));

  final String organizationId;
  final ApiClient apiClient;
  final Map<String, dynamic>? existingPost;

  void setCategory(OrganizationPostCategory category) {
    state = state.copyWith(category: category);
  }

  void setStartsAt(DateTime startsAt) {
    state = state.copyWith(startsAt: startsAt);
  }

  Future<bool> submit({
    required String title,
    required String description,
    required int? capacity,
  }) async {
    state = state.copyWith(isSubmitting: true, hasError: false);
    try {
      final category =
          state.category == OrganizationPostCategory.news ? 'news' : 'general';

      if (existingPost != null) {
        await apiClient.put<Map<String, dynamic>>(
          '/api/v1/community/events/${existingPost!['id']}',
          data: {
            'title': title.trim(),
            'description': description.trim(),
            'category': category,
            'capacity': capacity,
          },
        );
      } else {
        final startsAt = state.category == OrganizationPostCategory.news
            ? DateTime.now()
            : (state.startsAt ?? DateTime.now());
        await apiClient.post<Map<String, dynamic>>(
          '/api/v1/community/events',
          data: {
            'title': title.trim(),
            'description': description.trim(),
            'category': category,
            'organizationId': organizationId,
            'startsAtUtc': startsAt.toUtc().toIso8601String(),
            'endsAtUtc': startsAt
                .toUtc()
                .add(const Duration(hours: 2))
                .toIso8601String(),
            'capacity': capacity,
          },
        );
      }
      state = state.copyWith(isSubmitting: false);
      return true;
    } catch (_) {
      state = state.copyWith(hasError: true, isSubmitting: false);
      return false;
    }
  }

  Future<bool> cancelPost() async {
    if (existingPost == null) return false;
    state = state.copyWith(isSubmitting: true);
    try {
      await apiClient.post<dynamic>(
        '/api/v1/community/events/${existingPost!['id']}:cancel',
        data: {'reason': 'Cancelled by organization staff'},
      );
      state = state.copyWith(isSubmitting: false);
      return true;
    } catch (_) {
      state = state.copyWith(isSubmitting: false);
      return false;
    }
  }
}

class OrganizationPostFormParams {
  const OrganizationPostFormParams({
    required this.organizationId,
    required this.apiClient,
    this.existingPost,
  });

  final String organizationId;
  final ApiClient apiClient;
  final Map<String, dynamic>? existingPost;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is OrganizationPostFormParams &&
          other.organizationId == organizationId &&
          identical(other.apiClient, apiClient) &&
          other.existingPost == existingPost);

  @override
  int get hashCode => Object.hash(organizationId, apiClient, existingPost);
}

final organizationPostFormProvider = StateNotifierProvider.autoDispose.family<
    OrganizationPostFormNotifier,
    OrganizationPostFormState,
    OrganizationPostFormParams>(
  (ref, params) => OrganizationPostFormNotifier(
    organizationId: params.organizationId,
    apiClient: params.apiClient,
    existingPost: params.existingPost,
  ),
);
