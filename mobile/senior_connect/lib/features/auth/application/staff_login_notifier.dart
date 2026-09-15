// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
part 'staff_login_notifier.freezed.dart';
part 'staff_login_notifier.g.dart';
@freezed
class StaffLoginState with _ {
  const factory StaffLoginState({
    @Default(true) bool obscurePassword,
    @Default(false) bool isLoading,
    @Default(false) bool totpRequired,
    String? errorKey,
  }) = _StaffLoginState;
}
@riverpod
class StaffLoginNotifier extends _ {
  @override
  StaffLoginState build() => const StaffLoginState();
  void toggleObscurePassword() => state = state.copyWith(obscurePassword: !state.obscurePassword);
  void setLoading(bool v) => state = state.copyWith(isLoading: v, errorKey: null);
  void setTotpRequired(String? errorKey) => state = state.copyWith(totpRequired: true, errorKey: errorKey, isLoading: false);
  void setError(String key) => state = state.copyWith(errorKey: key, isLoading: false);
}
