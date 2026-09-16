// lib/features/profile/application/notification_preferences_notifier.dart
//
// P7-03: Notifier managing quiet hours and notification category preferences.

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../../../shared/riverpod/async_state.dart';
import '../data/notification_preferences_repository.dart';

class NotificationPreferencesNotifier
    extends StateNotifier<AsyncState<NotificationPreferencesModel>> {
  NotificationPreferencesNotifier(this._repository)
      : super(const AsyncState.loading()) {
    load();
  }

  final NotificationPreferencesRepository _repository;

  Future<void> load() async {
    state = const AsyncState.loading();
    try {
      final prefs = await _repository.getPreferences();
      state = AsyncState.loaded(prefs);
    } catch (e, st) {
      state = AsyncState.error('errors.generic', e, st);
    }
  }

  Future<bool> updateQuietHours({
    required bool enabled,
    required String start,
    required String end,
  }) async {
    final current = state.dataOrNull;
    if (current == null) return false;

    final updated = current.copyWith(
      quietHoursEnabled: enabled,
      quietHoursStart: start,
      quietHoursEnd: end,
    );

    try {
      await _repository.updatePreferences(updated);
      state = AsyncState.loaded(updated);
      return true;
    } catch (_) {
      return false;
    }
  }

  Future<bool> toggleCategory({
    required String categoryKey,
    required bool enabled,
  }) async {
    final current = state.dataOrNull;
    if (current == null) return false;

    NotificationPreferencesModel updated;
    switch (categoryKey) {
      case 'help_requests':
        updated = current.copyWith(helpRequestsCategoryEnabled: enabled);
        break;
      case 'community':
        updated = current.copyWith(communityCategoryEnabled: enabled);
        break;
      case 'family_welfare':
        updated = current.copyWith(familyWelfareCategoryEnabled: enabled);
        break;
      case 'system_account':
        updated = current.copyWith(systemAccountCategoryEnabled: enabled);
        break;
      default:
        return false;
    }

    try {
      await _repository.updatePreferences(updated);
      state = AsyncState.loaded(updated);
      return true;
    } catch (_) {
      return false;
    }
  }
}

final notificationPreferencesProvider = StateNotifierProvider.autoDispose.family<
    NotificationPreferencesNotifier,
    AsyncState<NotificationPreferencesModel>,
    ApiClient>(
  (ref, apiClient) {
    return NotificationPreferencesNotifier(
      NotificationPreferencesRepositoryImpl(apiClient),
    );
  },
);
