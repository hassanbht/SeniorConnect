// lib/features/profile/application/discovery_settings_notifier.dart
//
// Phase 4: loads the person's discoverability switch and verification facts
// together, and changes the switch. Only server-confirmed values are shown;
// a failed change keeps the last confirmed value.

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../../../shared/riverpod/async_state.dart';
import '../data/discovery_settings_repository.dart';

class DiscoverySettings {
  const DiscoverySettings(this.discoverability, this.verification);

  final Discoverability discoverability;
  final VerificationStatus verification;
}

class DiscoverySettingsNotifier
    extends StateNotifier<AsyncState<DiscoverySettings>> {
  DiscoverySettingsNotifier(this._repository)
    : super(const AsyncState.loading()) {
    load();
  }

  final DiscoverySettingsRepository _repository;

  /// Localization key of the last failed change, or null.
  String? saveErrorKey;

  bool _isSaving = false;

  Future<void> load() async {
    state = const AsyncState.loading();
    try {
      final results = await Future.wait([
        _repository.getDiscoverability(),
        _repository.getVerification(),
      ]);
      state = AsyncState.loaded(
        DiscoverySettings(
          results[0] as Discoverability,
          results[1] as VerificationStatus,
        ),
      );
    } on DioException catch (e, st) {
      state = AsyncState.error(mapDioError(e).l10nKey, e, st);
    } catch (e, st) {
      state = AsyncState.error('errors.generic', e, st);
    }
  }

  /// Returns true when the server confirmed the new value.
  Future<bool> setDiscoverable(bool value) async {
    final current = state.dataOrNull;
    if (current == null || _isSaving) return false;

    _isSaving = true;
    saveErrorKey = null;
    try {
      final confirmed = await _repository.setDiscoverable(value);
      state = AsyncState.loaded(
        DiscoverySettings(confirmed, current.verification),
      );
      return true;
    } on DioException catch (e) {
      saveErrorKey = mapDioError(e).l10nKey;
      state = AsyncState.loaded(current);
      return false;
    } catch (_) {
      saveErrorKey = 'errors.generic';
      state = AsyncState.loaded(current);
      return false;
    } finally {
      _isSaving = false;
    }
  }
}

final discoverySettingsProvider = StateNotifierProvider.autoDispose
    .family<
      DiscoverySettingsNotifier,
      AsyncState<DiscoverySettings>,
      ApiClient
    >(
      (ref, apiClient) =>
          DiscoverySettingsNotifier(DiscoverySettingsRepository(apiClient)),
    );
