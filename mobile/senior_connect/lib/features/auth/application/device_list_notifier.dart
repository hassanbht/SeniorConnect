// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
import '../data/auth_repository.dart';
part 'device_list_notifier.freezed.dart';
part 'device_list_notifier.g.dart';
@freezed
class DeviceListState with _ {
  const factory DeviceListState({
    @Default(true) bool isLoading,
    @Default([]) List<DeviceSession> devices,
    String? errorKey,
    String? revokingId,
  }) = _DeviceListState;
}
@riverpod
class DeviceListNotifier extends _ {
  @override
  DeviceListState build(AuthRepository repo) {
    Future(()=> load(repo));
    return const DeviceListState();
  }
  Future<void> load(AuthRepository repo) async {
    state = state.copyWith(isLoading: true, errorKey: null);
    try {
      final devices = await repo.getDevices();
      state = state.copyWith(devices: devices, isLoading: false);
    } catch (_) {
      state = state.copyWith(errorKey: 'errors.generic', isLoading: false);
    }
  }
  Future<void> revoke(String deviceId, AuthRepository repo) async {
    state = state.copyWith(revokingId: deviceId);
    try {
      await repo.revokeDevice(deviceId);
      await load(repo);
    } catch (_) {
      state = state.copyWith(errorKey: 'errors.generic');
    } finally {
      state = state.copyWith(revokingId: null);
    }
  }
}
