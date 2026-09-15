// lib/features/help_requests/application/senior_request_flow_notifier.dart
// DO NOT use setState in the screen. See AGENTS.md §State Management.

import 'package:flutter/material.dart' show IconData, Icons;
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
import '../../../core/network/api_client.dart';

part 'senior_request_flow_notifier.freezed.dart';
part 'senior_request_flow_notifier.g.dart';

enum RequestStep {
  loadingCategories,
  categoriesFailed,
  selectCategory,
  selectTiming,
  addDetails,
  reviewAndConfirm,
  submittedSuccess,
  blockedReferral,
}

@freezed
class HelpCategoryItem with _$HelpCategoryItem {
  const factory HelpCategoryItem({
    required String id,
    required String titleKey,
    required IconData icon,
  }) = _HelpCategoryItem;
}

@freezed
class SeniorRequestFlowState with _$SeniorRequestFlowState {
  const factory SeniorRequestFlowState({
    @Default(RequestStep.loadingCategories) RequestStep currentStep,
    HelpCategoryItem? selectedCategory,
    @Default('today') String selectedTiming,
    @Default(false) bool isSubmitting,
    String? errorMessage,
    String? createdRequestId,
    @Default({}) Map<String, String> categoryIdsByCode,
    @Default({}) Map<String, bool> categoryBlockedByCode,
    @Default([]) List<HelpCategoryItem> categories,
  }) = _SeniorRequestFlowState;
}

@riverpod
class SeniorRequestFlowNotifier extends _$SeniorRequestFlowNotifier {
  @override
  SeniorRequestFlowState build(
    ApiClient apiClient,
    List<Map<String, dynamic>>? initialCategories,
  ) {
    Future(() => _loadCategories(apiClient, initialCategories));
    return const SeniorRequestFlowState();
  }

  Future<void> _loadCategories(
    ApiClient apiClient,
    List<Map<String, dynamic>>? initialCategories,
  ) async {
    try {
      final raw =
          initialCategories ??
          (await apiClient.get<List<dynamic>>(
            '/api/v1/activities/categories',
          )).map((e) => Map<String, dynamic>.from(e as Map)).toList();

      final idsByCode = <String, String>{};
      final blockedByCode = <String, bool>{};
      final items = <HelpCategoryItem>[];

      for (final m in raw) {
        final code = m['code'] as String? ?? '';
        final id = m['id'] as String? ?? '';
        final blocked = m['requiresOrganization'] as bool? ?? false;
        idsByCode[code] = id;
        blockedByCode[code] = blocked;
        items.add(
          HelpCategoryItem(
            id: id,
            titleKey: 'help.category.$code',
            icon: _iconForCategory(code),
          ),
        );
      }
      state = state.copyWith(
        currentStep: RequestStep.selectCategory,
        categories: items,
        categoryIdsByCode: idsByCode,
        categoryBlockedByCode: blockedByCode,
      );
    } catch (_) {
      state = state.copyWith(currentStep: RequestStep.categoriesFailed);
    }
  }

  static IconData _iconForCategory(String code) {
    const map = <String, IconData>{
      'shopping': Icons.shopping_cart_outlined,
      'doctor': Icons.local_hospital_outlined,
      'authority': Icons.account_balance_outlined,
      'accompaniment': Icons.directions_walk,
      'home_small': Icons.home_repair_service_outlined,
      'language_practice': Icons.translate,
    };
    return map[code] ?? Icons.help_outline;
  }

  void selectCategory(HelpCategoryItem category) {
    final blocked = state.categoryBlockedByCode[category.id] ?? false;
    state = state.copyWith(
      selectedCategory: category,
      currentStep: blocked
          ? RequestStep.blockedReferral
          : RequestStep.selectTiming,
    );
  }

  void selectTiming(String timing) => state = state.copyWith(
    selectedTiming: timing,
    currentStep: RequestStep.addDetails,
  );

  void goToReview() =>
      state = state.copyWith(currentStep: RequestStep.reviewAndConfirm);

  void goBack() {
    final prev = switch (state.currentStep) {
      RequestStep.selectTiming => RequestStep.selectCategory,
      RequestStep.addDetails => RequestStep.selectTiming,
      RequestStep.reviewAndConfirm => RequestStep.addDetails,
      _ => RequestStep.selectCategory,
    };
    state = state.copyWith(currentStep: prev);
  }

  Future<void> submit(String details, ApiClient apiClient) async {
    final cat = state.selectedCategory;
    if (cat == null) return;
    final categoryId = state.categoryIdsByCode[cat.id];
    if (categoryId == null) return;

    state = state.copyWith(isSubmitting: true, errorMessage: null);
    try {
      final resp = await apiClient.post<Map<String, dynamic>>(
        '/api/v1/help-requests',
        data: {
          'categoryId': categoryId,
          'timing': state.selectedTiming,
          'details': details.trim(),
        },
      );
      state = state.copyWith(
        isSubmitting: false,
        currentStep: RequestStep.submittedSuccess,
        createdRequestId: resp['id'] as String?,
      );
    } catch (_) {
      state = state.copyWith(
        isSubmitting: false,
        errorMessage: 'errors.generic',
      );
    }
  }
}
