// lib/features/auth/application/auth_notifier.dart
// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
part 'auth_notifier.freezed.dart';
part 'auth_notifier.g.dart';
@freezed
class AuthState with _ {
  const factory AuthState({
    @Default(false) bool isPhoneLoading,
    @Default(false) bool isEmailLoading,
    @Default(false) bool isGoogleLoading,
    @Default(false) bool isIdAustriaLoading,
    @Default(true) bool obscurePassword,
    @Default(true) bool obscureConfirmPassword,
    @Default(false) bool passwordsMatch,
    @Default(false) bool isLoginMode,
    String? phoneErrorKey,
    String? emailErrorKey,
  }) = _AuthState;
}
@riverpod
class AuthNotifier extends _ {
  @override
  AuthState build() => const AuthState();
  void setPhoneLoading(bool v) => state = state.copyWith(isPhoneLoading: v, phoneErrorKey: null);
  void setEmailLoading(bool v) => state = state.copyWith(isEmailLoading: v, emailErrorKey: null);
  void setGoogleLoading(bool v) => state = state.copyWith(isGoogleLoading: v);
  void setIdAustriaLoading(bool v) => state = state.copyWith(isIdAustriaLoading: v);
  void toggleObscurePassword() => state = state.copyWith(obscurePassword: !state.obscurePassword);
  void toggleObscureConfirmPassword() => state = state.copyWith(obscureConfirmPassword: !state.obscureConfirmPassword);
  void setPasswordsMatch(bool v) => state = state.copyWith(passwordsMatch: v);
  void setLoginMode(bool v) => state = state.copyWith(isLoginMode: v);
  void setPhoneError(String? key) => state = state.copyWith(phoneErrorKey: key);
  void setEmailError(String? key) => state = state.copyWith(emailErrorKey: key);
}
