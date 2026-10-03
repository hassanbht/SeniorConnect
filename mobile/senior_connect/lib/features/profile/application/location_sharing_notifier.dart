// lib/features/profile/application/location_sharing_notifier.dart
//
// Phase 3: loads and changes the person's own location-sharing choice.
// The server response is authoritative: the state only ever shows what the
// server confirmed, never an optimistic or default value.

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../../../shared/riverpod/async_state.dart';
import '../data/location_sharing_repository.dart';

class LocationSharingNotifier
    extends StateNotifier<AsyncState<LocationSharing>> {
  LocationSharingNotifier(this._repository)
    : super(const AsyncState.loading()) {
    load();
  }

  final LocationSharingRepository _repository;

  /// Localization key of the last failed change, or null. A failed change
  /// keeps the last confirmed level on screen.
  String? saveErrorKey;

  bool _isSaving = false;

  Future<void> load() async {
    state = const AsyncState.loading();
    try {
      state = AsyncState.loaded(await _repository.get());
    } on DioException catch (e, st) {
      // A network failure is an error state, never a logout and never a
      // made-up level.
      state = AsyncState.error(mapDioError(e).l10nKey, e, st);
    } catch (e, st) {
      state = AsyncState.error('errors.generic', e, st);
    }
  }

  /// Returns true when the server confirmed the new level.
  Future<bool> change(LocationSharingLevel level) async {
    final current = state.dataOrNull;
    if (current == null || _isSaving) return false;

    _isSaving = true;
    saveErrorKey = null;
    try {
      state = AsyncState.loaded(
        await _repository.update(level, current.currentConsentVersion),
      );
      return true;
    } on DioException catch (e) {
      final data = e.response?.data;
      if (data is Map && data['code'] == 'CONSENT_VERSION_OUTDATED') {
        // The explanation changed on the server: show the new one first.
        saveErrorKey = 'location_sharing.version_outdated';
        await load();
      } else {
        saveErrorKey = mapDioError(e).l10nKey;
        state = AsyncState.loaded(current); // unchanged, as confirmed before
      }
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

final locationSharingProvider = StateNotifierProvider.autoDispose
    .family<LocationSharingNotifier, AsyncState<LocationSharing>, ApiClient>(
      (ref, apiClient) =>
          LocationSharingNotifier(LocationSharingRepositoryImpl(apiClient)),
    );
