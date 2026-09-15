// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
import '../../../core/network/api_client.dart';
part 'safeguarding_concern_notifier.freezed.dart';
part 'safeguarding_concern_notifier.g.dart';

@freezed
class SafeguardingConcernState with _ {
  const factory SafeguardingConcernState({
    @Default('general_concern') String selectedCategory,
    @Default(false) bool isSubmitting,
    @Default(false) bool submitted,
    String? errorKey,
  }) = _SafeguardingConcernState;
}

@riverpod
class SafeguardingConcernNotifier extends _ {
  @override
  SafeguardingConcernState build() => const SafeguardingConcernState();
  void setCategory(String cat) => state = state.copyWith(selectedCategory: cat);
  Future<void> submit(
    String summary,
    String subjectUserId,
    ApiClient? apiClient,
  ) async {
    if (summary.trim().isEmpty) return;
    state = state.copyWith(isSubmitting: true, errorKey: null);
    try {
      if (apiClient != null) {
        await apiClient.post<dynamic>(
          '/api/v1/safeguarding/concerns',
          data: {
            'subjectUserId': subjectUserId,
            'summary': summary.trim(),
            'category': state.selectedCategory,
            'severity': 'Medium',
          },
        );
      }
      state = state.copyWith(isSubmitting: false, submitted: true);
    } catch (_) {
      state = state.copyWith(isSubmitting: false, errorKey: 'errors.generic');
    }
  }
}
