import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../../core/network/api_client.dart';
import '../../../core/cache/reference_data_repository.dart';

const _kLastCategoryId = 'last_log_category_id';
const _kLastDuration = 'last_log_duration';

/// One loggable activity category from GET /api/v1/activities/categories.
class LogActivityCategory {
  const LogActivityCategory({required this.id, required this.nameKey});

  final String id;

  /// Localization key sent by the server (e.g. `help.category.shopping`).
  final String nameKey;
}

class LogActivityState {
  const LogActivityState({
    this.isLoadingCategories = true,
    this.categoriesError,
    this.categories = const [],
    this.selectedCategoryId,
    this.durationMinutes = 60,
    this.insuranceContext = 1, // OrganizationPolicy
    this.transportMode = 0, // None
    this.isSubmitting = false,
    this.errorMessage,
  });

  final bool isLoadingCategories;

  /// Localization key; set when the categories could not be loaded.
  final String? categoriesError;
  final List<LogActivityCategory> categories;

  /// Required: submit is refused until a real category is picked.
  final String? selectedCategoryId;
  final int durationMinutes;
  final int insuranceContext;
  final int transportMode;
  final bool isSubmitting;

  /// Localization key for a submit/validation error.
  final String? errorMessage;

  LogActivityState copyWith({
    bool? isLoadingCategories,
    Object? categoriesError = _sentinel,
    List<LogActivityCategory>? categories,
    Object? selectedCategoryId = _sentinel,
    int? durationMinutes,
    int? insuranceContext,
    int? transportMode,
    bool? isSubmitting,
    String? errorMessage,
  }) {
    return LogActivityState(
      isLoadingCategories: isLoadingCategories ?? this.isLoadingCategories,
      categoriesError: categoriesError == _sentinel
          ? this.categoriesError
          : categoriesError as String?,
      categories: categories ?? this.categories,
      selectedCategoryId: selectedCategoryId == _sentinel
          ? this.selectedCategoryId
          : selectedCategoryId as String?,
      durationMinutes: durationMinutes ?? this.durationMinutes,
      insuranceContext: insuranceContext ?? this.insuranceContext,
      transportMode: transportMode ?? this.transportMode,
      isSubmitting: isSubmitting ?? this.isSubmitting,
      errorMessage: errorMessage,
    );
  }
}

const _sentinel = Object();

String _errorKey(Object e) =>
    e is DioException ? mapDioError(e).l10nKey : 'errors.generic';

class LogActivityNotifier extends StateNotifier<LogActivityState> {
  LogActivityNotifier({required this.apiClient})
    : super(const LogActivityState()) {
    loadCategories();
  }

  final ApiClient apiClient;

  /// Loads the server's categories (blocked/inactive ones cannot be logged),
  /// then applies the last entry's category and duration as a prefill.
  Future<void> loadCategories() async {
    state = state.copyWith(isLoadingCategories: true, categoriesError: null);
    try {
      final data = await ReferenceDataRepository(
        apiClient,
      ).activityCategories();
      final categories = <LogActivityCategory>[
        for (final item in data)
          if (item['id'] is String &&
              item['isBlocked'] != true &&
              item['isActive'] != false)
            LogActivityCategory(
              id: item['id'] as String,
              nameKey: item['nameKey'] as String? ?? '',
            ),
      ];
      state = state.copyWith(
        isLoadingCategories: false,
        categories: categories,
      );
      await _applyPrefill();
    } catch (e) {
      state = state.copyWith(
        isLoadingCategories: false,
        categoriesError: _errorKey(e),
      );
    }
  }

  Future<void> _applyPrefill() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final lastId = prefs.getString(_kLastCategoryId);
      final lastDuration = prefs.getInt(_kLastDuration);
      final known = state.categories.any((c) => c.id == lastId);
      state = state.copyWith(
        selectedCategoryId: known ? lastId : state.selectedCategoryId,
        durationMinutes: lastDuration ?? state.durationMinutes,
      );
    } catch (_) {
      // Prefill is a convenience; the form works without it.
    }
  }

  void setCategory(String categoryId) {
    state = state.copyWith(selectedCategoryId: categoryId);
  }

  void setDuration(int durationMinutes) {
    state = state.copyWith(durationMinutes: durationMinutes);
  }

  Future<bool> submit({required String notes}) async {
    final categoryId = state.selectedCategoryId;
    if (categoryId == null) {
      state = state.copyWith(
        errorMessage: 'help.log_activity.category_required',
      );
      return false;
    }

    state = state.copyWith(isSubmitting: true);

    try {
      final now = DateTime.now();
      final dateOnly =
          '${now.year}-${now.month.toString().padLeft(2, '0')}-${now.day.toString().padLeft(2, '0')}';

      await apiClient.post<dynamic>(
        '/api/v1/activities',
        data: {
          'categoryId': categoryId,
          'occurredOn': dateOnly,
          'durationMinutes': state.durationMinutes,
          'locationType': 0,
          'notes': notes.trim().isEmpty ? null : notes.trim(),
          'insuranceContext': state.insuranceContext,
          'transportMode': state.transportMode,
        },
      );

      try {
        final prefs = await SharedPreferences.getInstance();
        await prefs.setString(_kLastCategoryId, categoryId);
        await prefs.setInt(_kLastDuration, state.durationMinutes);
      } catch (_) {
        // The activity is logged; losing the prefill is harmless.
      }

      state = state.copyWith(isSubmitting: false);
      return true;
    } catch (e) {
      state = state.copyWith(isSubmitting: false, errorMessage: _errorKey(e));
      return false;
    }
  }
}

final logActivityProvider = StateNotifierProvider.autoDispose
    .family<LogActivityNotifier, LogActivityState, ApiClient>(
      (ref, apiClient) => LogActivityNotifier(apiClient: apiClient),
    );
