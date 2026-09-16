// lib/features/profile/application/privacy_settings_notifier.dart
//
// P7-05, P7-06, P7-07: Notifier managing consents, export and deletion state.

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../../../shared/riverpod/async_state.dart';
import '../data/privacy_repository.dart';

class PrivacySettingsData {
  const PrivacySettingsData({
    required this.consents,
    this.lastExport,
    this.pendingDeletion,
  });

  final List<ConsentItem> consents;
  final UserDataExportSummary? lastExport;
  final DeletionRequestSummary? pendingDeletion;

  PrivacySettingsData copyWith({
    List<ConsentItem>? consents,
    UserDataExportSummary? lastExport,
    DeletionRequestSummary? pendingDeletion,
  }) {
    return PrivacySettingsData(
      consents: consents ?? this.consents,
      lastExport: lastExport ?? this.lastExport,
      pendingDeletion: pendingDeletion ?? this.pendingDeletion,
    );
  }
}

class PrivacySettingsNotifier
    extends StateNotifier<AsyncState<PrivacySettingsData>> {
  PrivacySettingsNotifier(this._repository)
      : super(const AsyncState.loading()) {
    load();
  }

  final PrivacyRepository _repository;

  Future<void> load() async {
    state = const AsyncState.loading();
    try {
      final consents = await _repository.getConsents();
      state = AsyncState.loaded(PrivacySettingsData(consents: consents));
    } catch (e, st) {
      state = AsyncState.error('errors.generic', e, st);
    }
  }

  Future<bool> withdrawConsent(String consentType) async {
    try {
      await _repository.withdrawConsent(consentType);
      await load();
      return true;
    } catch (_) {
      return false;
    }
  }

  Future<UserDataExportSummary?> exportUserData() async {
    try {
      final export = await _repository.exportUserData();
      final current = state.dataOrNull;
      if (current != null) {
        state = AsyncState.loaded(current.copyWith(lastExport: export));
      }
      return export;
    } catch (_) {
      return null;
    }
  }

  Future<DeletionRequestSummary?> requestDeletion(String? reason) async {
    try {
      final req = await _repository.requestAccountDeletion(reason);
      final current = state.dataOrNull;
      if (current != null) {
        state = AsyncState.loaded(current.copyWith(pendingDeletion: req));
      }
      return req;
    } catch (_) {
      return null;
    }
  }

  Future<bool> confirmDeletion(String token) async {
    try {
      await _repository.confirmAccountDeletion(token);
      return true;
    } catch (_) {
      return false;
    }
  }
}

final privacySettingsProvider = StateNotifierProvider.autoDispose
    .family<PrivacySettingsNotifier, AsyncState<PrivacySettingsData>, ApiClient>(
  (ref, apiClient) {
    return PrivacySettingsNotifier(PrivacyRepositoryImpl(apiClient));
  },
);
