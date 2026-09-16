import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';

class SafeguardingConcernState {
  const SafeguardingConcernState({
    this.selectedCategory = 'general_concern',
    this.isSubmitting = false,
    this.submitted = false,
    this.errorMessage,
  });

  final String selectedCategory;
  final bool isSubmitting;
  final bool submitted;
  final String? errorMessage;

  SafeguardingConcernState copyWith({
    String? selectedCategory,
    bool? isSubmitting,
    bool? submitted,
    Object? errorMessage = _sentinel,
  }) {
    return SafeguardingConcernState(
      selectedCategory: selectedCategory ?? this.selectedCategory,
      isSubmitting: isSubmitting ?? this.isSubmitting,
      submitted: submitted ?? this.submitted,
      errorMessage:
          errorMessage == _sentinel ? this.errorMessage : errorMessage as String?,
    );
  }
}

const _sentinel = Object();

class SafeguardingConcernNotifier
    extends StateNotifier<SafeguardingConcernState> {
  SafeguardingConcernNotifier({this.apiClient})
      : super(const SafeguardingConcernState());

  final ApiClient? apiClient;

  void setCategory(String category) {
    state = state.copyWith(selectedCategory: category);
  }

  Future<bool> submitConcern({
    required String subjectUserId,
    required String details,
  }) async {
    if (details.trim().isEmpty) return false;
    state = state.copyWith(isSubmitting: true, errorMessage: null);

    try {
      if (apiClient != null) {
        await apiClient!.post<dynamic>(
          '/api/v1/safeguarding/concerns',
          data: {
            'subjectUserId': subjectUserId,
            'category': state.selectedCategory,
            'details': details.trim(),
          },
        );
      }
      state = state.copyWith(isSubmitting: false, submitted: true);
      return true;
    } catch (_) {
      state = state.copyWith(
        isSubmitting: false,
        errorMessage: 'errors.generic',
      );
      return false;
    }
  }
}

final safeguardingConcernProvider = StateNotifierProvider.autoDispose
    .family<SafeguardingConcernNotifier, SafeguardingConcernState, ApiClient?>(
  (ref, apiClient) {
    return SafeguardingConcernNotifier(apiClient: apiClient);
  },
);
