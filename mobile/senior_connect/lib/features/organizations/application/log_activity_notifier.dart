import 'package:easy_localization/easy_localization.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../../core/network/api_client.dart';

class LogActivityState {
  const LogActivityState({
    this.durationMinutes = 60,
    this.selectedCategoryKey = 'help.category.shopping',
    this.insuranceContext = 1, // OrganizationPolicy
    this.transportMode = 0, // None
    this.isSubmitting = false,
    this.errorMessage,
  });

  final int durationMinutes;
  final String selectedCategoryKey;
  final int insuranceContext;
  final int transportMode;
  final bool isSubmitting;
  final String? errorMessage;

  LogActivityState copyWith({
    int? durationMinutes,
    String? selectedCategoryKey,
    int? insuranceContext,
    int? transportMode,
    bool? isSubmitting,
    String? errorMessage,
  }) {
    return LogActivityState(
      durationMinutes: durationMinutes ?? this.durationMinutes,
      selectedCategoryKey: selectedCategoryKey ?? this.selectedCategoryKey,
      insuranceContext: insuranceContext ?? this.insuranceContext,
      transportMode: transportMode ?? this.transportMode,
      isSubmitting: isSubmitting ?? this.isSubmitting,
      errorMessage: errorMessage,
    );
  }
}

class LogActivityNotifier extends StateNotifier<LogActivityState> {
  LogActivityNotifier({required this.apiClient})
      : super(const LogActivityState()) {
    loadLastEntryPrefill();
  }

  final ApiClient apiClient;

  static const List<String> categoryKeys = [
    'help.category.shopping',
    'help.category.doctor',
    'help.category.authority',
    'help.category.accompaniment',
    'help.category.home_small',
    'help.category.language_practice',
    'help.category.newcomer_orientation',
    'help.category.mentoring',
  ];

  Future<void> loadLastEntryPrefill() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final lastCat = prefs.getString('last_log_category');
      final lastDuration = prefs.getInt('last_log_duration');
      if (lastCat != null && categoryKeys.contains(lastCat)) {
        state = state.copyWith(
          selectedCategoryKey: lastCat,
          durationMinutes: lastDuration ?? state.durationMinutes,
        );
      }
    } catch (_) {}
  }

  void setCategory(String categoryKey) {
    state = state.copyWith(selectedCategoryKey: categoryKey);
  }

  void setDuration(int durationMinutes) {
    state = state.copyWith(durationMinutes: durationMinutes);
  }

  Future<bool> submit({required String notes}) async {
    state = state.copyWith(isSubmitting: true, errorMessage: null);

    try {
      final now = DateTime.now();
      final dateOnly =
          '${now.year}-${now.month.toString().padLeft(2, '0')}-${now.day.toString().padLeft(2, '0')}';

      final payload = {
        'categoryId': '00000000-0000-0000-0000-000000000001',
        'occurredOn': dateOnly,
        'durationMinutes': state.durationMinutes,
        'locationType': 0,
        'notes': notes.trim().isEmpty ? null : notes.trim(),
        'insuranceContext': state.insuranceContext,
        'transportMode': state.transportMode,
      };

      await apiClient.post<dynamic>(
        '/api/v1/activities',
        data: payload,
      );

      final prefs = await SharedPreferences.getInstance();
      await prefs.setString('last_log_category', state.selectedCategoryKey);
      await prefs.setInt('last_log_duration', state.durationMinutes);

      state = state.copyWith(isSubmitting: false);
      return true;
    } catch (_) {
      state = state.copyWith(
        isSubmitting: false,
        errorMessage: 'errors.generic'.tr(),
      );
      return false;
    }
  }
}

final logActivityProvider = StateNotifierProvider.autoDispose
    .family<LogActivityNotifier, LogActivityState, ApiClient>(
  (ref, apiClient) => LogActivityNotifier(apiClient: apiClient),
);
