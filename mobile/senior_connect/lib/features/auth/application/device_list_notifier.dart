import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../../../shared/riverpod/async_state.dart';
import '../data/auth_repository.dart';

class DeviceListState {
  const DeviceListState({
    this.devicesState = const AsyncState.loading(),
    this.revokingId,
    this.actionErrorKey,
  });

  final AsyncState<List<DeviceSession>> devicesState;
  final String? revokingId;
  final String? actionErrorKey;

  DeviceListState copyWith({
    AsyncState<List<DeviceSession>>? devicesState,
    Object? revokingId = _sentinel,
    Object? actionErrorKey = _sentinel,
  }) {
    return DeviceListState(
      devicesState: devicesState ?? this.devicesState,
      revokingId: revokingId == _sentinel ? this.revokingId : revokingId as String?,
      actionErrorKey:
          actionErrorKey == _sentinel ? this.actionErrorKey : actionErrorKey as String?,
    );
  }
}

const _sentinel = Object();

class DeviceListNotifier extends StateNotifier<DeviceListState> {
  DeviceListNotifier({required this.authRepository})
      : super(const DeviceListState()) {
    load();
  }

  final AuthRepository authRepository;

  Future<void> load() async {
    state = state.copyWith(devicesState: const AsyncState.loading(), actionErrorKey: null);
    try {
      final devices = await authRepository.getDevices();
      if (devices.isEmpty) {
        state = state.copyWith(devicesState: const AsyncState.empty());
      } else {
        state = state.copyWith(devicesState: AsyncState.loaded(devices));
      }
    } catch (e, st) {
      state = state.copyWith(
        devicesState: AsyncState.error('errors.generic', e, st),
      );
    }
  }

  Future<bool> revokeDevice(String deviceId) async {
    state = state.copyWith(revokingId: deviceId, actionErrorKey: null);
    try {
      await authRepository.revokeDevice(deviceId);
      state = state.copyWith(revokingId: null);
      await load();
      return true;
    } catch (_) {
      state = state.copyWith(revokingId: null, actionErrorKey: 'errors.generic');
      return false;
    }
  }
}

final deviceListProvider = StateNotifierProvider.autoDispose
    .family<DeviceListNotifier, DeviceListState, ApiClient>(
  (ref, apiClient) {
    return DeviceListNotifier(authRepository: AuthRepositoryImpl(apiClient));
  },
);
