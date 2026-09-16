import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';

enum SeniorRequestStep {
  loadingCategories,
  categoriesFailed,
  selectCategory,
  selectTiming,
  addDetails,
  reviewAndConfirm,
  submittedSuccess,
  blockedReferral,
}

class HelpCategoryItem {
  const HelpCategoryItem({
    required this.id,
    required this.titleKey,
    required this.icon,
  });

  final String id;
  final String titleKey;
  final IconData icon;
}

class SeniorRequestFlowState {
  const SeniorRequestFlowState({
    this.currentStep = SeniorRequestStep.loadingCategories,
    this.selectedCategory,
    this.selectedTiming = 'today',
    this.isSubmitting = false,
    this.errorMessage,
    this.createdRequestId,
    this.categoryIdsByCode = const {},
    this.categoryBlockedByCode = const {},
  });

  final SeniorRequestStep currentStep;
  final HelpCategoryItem? selectedCategory;
  final String selectedTiming;
  final bool isSubmitting;
  final String? errorMessage;
  final String? createdRequestId;
  final Map<String, String> categoryIdsByCode;
  final Map<String, bool> categoryBlockedByCode;

  SeniorRequestFlowState copyWith({
    SeniorRequestStep? currentStep,
    Object? selectedCategory = _sentinel,
    String? selectedTiming,
    bool? isSubmitting,
    Object? errorMessage = _sentinel,
    Object? createdRequestId = _sentinel,
    Map<String, String>? categoryIdsByCode,
    Map<String, bool>? categoryBlockedByCode,
  }) {
    return SeniorRequestFlowState(
      currentStep: currentStep ?? this.currentStep,
      selectedCategory: selectedCategory == _sentinel
          ? this.selectedCategory
          : selectedCategory as HelpCategoryItem?,
      selectedTiming: selectedTiming ?? this.selectedTiming,
      isSubmitting: isSubmitting ?? this.isSubmitting,
      errorMessage:
          errorMessage == _sentinel ? this.errorMessage : errorMessage as String?,
      createdRequestId: createdRequestId == _sentinel
          ? this.createdRequestId
          : createdRequestId as String?,
      categoryIdsByCode: categoryIdsByCode ?? this.categoryIdsByCode,
      categoryBlockedByCode: categoryBlockedByCode ?? this.categoryBlockedByCode,
    );
  }
}

const _sentinel = Object();

class SeniorRequestFlowNotifier
    extends StateNotifier<SeniorRequestFlowState> {
  SeniorRequestFlowNotifier({
    required this.apiClient,
    this.initialCategories,
    this.seniorUserId,
  }) : super(const SeniorRequestFlowState()) {
    loadCategories();
  }

  final ApiClient apiClient;
  final List<Map<String, dynamic>>? initialCategories;
  final String? seniorUserId;

  static const List<HelpCategoryItem> categories = [
    HelpCategoryItem(
      id: 'shopping',
      titleKey: 'help.category.shopping',
      icon: Icons.shopping_cart_outlined,
    ),
    HelpCategoryItem(
      id: 'doctor',
      titleKey: 'help.category.doctor',
      icon: Icons.local_hospital_outlined,
    ),
    HelpCategoryItem(
      id: 'authority',
      titleKey: 'help.category.authority',
      icon: Icons.account_balance_outlined,
    ),
    HelpCategoryItem(
      id: 'accompaniment',
      titleKey: 'help.category.accompaniment',
      icon: Icons.directions_walk_outlined,
    ),
    HelpCategoryItem(
      id: 'home_small',
      titleKey: 'help.category.home_small',
      icon: Icons.home_repair_service_outlined,
    ),
    HelpCategoryItem(
      id: 'language_practice',
      titleKey: 'help.category.language_practice',
      icon: Icons.translate_outlined,
    ),
    HelpCategoryItem(
      id: 'newcomer_orientation',
      titleKey: 'help.category.newcomer_orientation',
      icon: Icons.explore_outlined,
    ),
    HelpCategoryItem(
      id: 'mentoring',
      titleKey: 'help.category.mentoring',
      icon: Icons.school_outlined,
    ),
  ];

  Future<void> loadCategories() async {
    if (initialCategories != null) {
      final ids = <String, String>{};
      final blocked = <String, bool>{};
      for (final item in initialCategories!) {
        final code = item['code'] as String?;
        final id = item['id'] as String?;
        if (code == null || id == null) continue;
        ids[code] = id;
        blocked[code] = item['isBlocked'] as bool? ?? false;
      }
      state = state.copyWith(
        categoryIdsByCode: ids,
        categoryBlockedByCode: blocked,
        currentStep: SeniorRequestStep.selectCategory,
      );
      return;
    }

    state = state.copyWith(currentStep: SeniorRequestStep.loadingCategories);
    try {
      final data = await apiClient.get<dynamic>('/api/v1/activities/categories');
      if (data is! List) throw const FormatException('Bad response shape');

      final ids = <String, String>{};
      final blocked = <String, bool>{};
      for (final item in data) {
        final m = item as Map<String, dynamic>;
        final code = m['code'] as String?;
        final id = m['id'] as String?;
        if (code == null || id == null) continue;
        ids[code] = id;
        blocked[code] = m['isBlocked'] as bool? ?? false;
      }
      state = state.copyWith(
        categoryIdsByCode: ids,
        categoryBlockedByCode: blocked,
        currentStep: SeniorRequestStep.selectCategory,
      );
    } catch (_) {
      state = state.copyWith(currentStep: SeniorRequestStep.categoriesFailed);
    }
  }

  void onCategorySelected(HelpCategoryItem category) {
    if (state.categoryBlockedByCode[category.id] ?? false) {
      state = state.copyWith(
        selectedCategory: category,
        currentStep: SeniorRequestStep.blockedReferral,
      );
      return;
    }
    state = state.copyWith(
      selectedCategory: category,
      currentStep: SeniorRequestStep.selectTiming,
    );
  }

  void onTimingSelected(String timingKey) {
    state = state.copyWith(
      selectedTiming: timingKey,
      currentStep: SeniorRequestStep.addDetails,
    );
  }

  void goToReview() {
    state = state.copyWith(currentStep: SeniorRequestStep.reviewAndConfirm);
  }

  void goToStep(SeniorRequestStep step) {
    state = state.copyWith(currentStep: step);
  }

  Future<void> submitRequest(String details) async {
    final categoryId = state.categoryIdsByCode[state.selectedCategory?.id];
    if (categoryId == null) {
      state = state.copyWith(errorMessage: 'errors.generic'.tr());
      return;
    }

    state = state.copyWith(isSubmitting: true, errorMessage: null);

    final now = DateTime.now().toUtc();
    final startUtc = switch (state.selectedTiming) {
      'tomorrow' => now.add(const Duration(days: 1)),
      'this_week' => now.add(const Duration(days: 3)),
      _ => now.add(const Duration(hours: 2)),
    };

    try {
      final body = <String, dynamic>{
        'categoryId': categoryId,
        'notes': details,
        'scheduledStartUtc': startUtc.toIso8601String(),
        'durationMinutes': 60,
      };
      if (seniorUserId != null) {
        body['seniorUserId'] = seniorUserId;
      }

      final response = await apiClient.post<Map<String, dynamic>>(
        '/api/v1/help-requests',
        data: body,
      );

      final newId = response['id'] as String? ?? '';
      state = state.copyWith(
        isSubmitting: false,
        createdRequestId: newId,
        currentStep: SeniorRequestStep.submittedSuccess,
      );
    } catch (_) {
      state = state.copyWith(
        isSubmitting: false,
        errorMessage: 'errors.generic'.tr(),
      );
    }
  }
}

class SeniorRequestParams {
  const SeniorRequestParams({
    required this.apiClient,
    this.initialCategories,
    this.seniorUserId,
  });

  final ApiClient apiClient;
  final List<Map<String, dynamic>>? initialCategories;
  final String? seniorUserId;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is SeniorRequestParams &&
          identical(other.apiClient, apiClient) &&
          other.initialCategories == initialCategories &&
          other.seniorUserId == seniorUserId);

  @override
  int get hashCode => Object.hash(apiClient, initialCategories, seniorUserId);
}

final seniorRequestFlowProvider = StateNotifierProvider.autoDispose
    .family<SeniorRequestFlowNotifier, SeniorRequestFlowState, SeniorRequestParams>(
  (ref, params) {
    return SeniorRequestFlowNotifier(
      apiClient: params.apiClient,
      initialCategories: params.initialCategories,
      seniorUserId: params.seniorUserId,
    );
  },
);
