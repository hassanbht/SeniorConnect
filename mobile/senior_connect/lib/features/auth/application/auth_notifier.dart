import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../data/auth_repository.dart';

class AuthState {
  const AuthState({
    this.isPhoneLoading = false,
    this.isEmailLoading = false,
    this.isGoogleLoading = false,
    this.isIdAustriaLoading = false,
    this.phoneErrorKey,
    this.emailErrorKey,
    this.isLoginMode = false,
  });

  final bool isPhoneLoading;
  final bool isEmailLoading;
  final bool isGoogleLoading;
  final bool isIdAustriaLoading;
  final String? phoneErrorKey;
  final String? emailErrorKey;
  final bool isLoginMode;

  AuthState copyWith({
    bool? isPhoneLoading,
    bool? isEmailLoading,
    bool? isGoogleLoading,
    bool? isIdAustriaLoading,
    Object? phoneErrorKey = _sentinel,
    Object? emailErrorKey = _sentinel,
    bool? isLoginMode,
  }) {
    return AuthState(
      isPhoneLoading: isPhoneLoading ?? this.isPhoneLoading,
      isEmailLoading: isEmailLoading ?? this.isEmailLoading,
      isGoogleLoading: isGoogleLoading ?? this.isGoogleLoading,
      isIdAustriaLoading: isIdAustriaLoading ?? this.isIdAustriaLoading,
      phoneErrorKey:
          phoneErrorKey == _sentinel ? this.phoneErrorKey : phoneErrorKey as String?,
      emailErrorKey:
          emailErrorKey == _sentinel ? this.emailErrorKey : emailErrorKey as String?,
      isLoginMode: isLoginMode ?? this.isLoginMode,
    );
  }
}

const _sentinel = Object();

class AuthNotifier extends StateNotifier<AuthState> {
  AuthNotifier({required this.authRepository}) : super(const AuthState());

  final AuthRepository authRepository;

  void toggleEmailMode() {
    state = state.copyWith(
      isLoginMode: !state.isLoginMode,
      emailErrorKey: null,
    );
  }

  void setEmailErrorKey(String? key) {
    state = state.copyWith(emailErrorKey: key);
  }

  void setPhoneErrorKey(String? key) {
    state = state.copyWith(phoneErrorKey: key);
  }

  String errorKeyFor(Object error) {
    if (error is DioException) return mapDioError(error).l10nKey;
    return 'errors.generic';
  }

  Future<bool> submitPhone(String phone) async {
    state = state.copyWith(isPhoneLoading: true, phoneErrorKey: null);
    try {
      await authRepository.requestPhoneOtp(phone);
      state = state.copyWith(isPhoneLoading: false);
      return true;
    } catch (e) {
      state = state.copyWith(isPhoneLoading: false, phoneErrorKey: errorKeyFor(e));
      return false;
    }
  }

  Future<bool> submitEmailRegistration({
    required String email,
    required String password,
    required String confirmPassword,
  }) async {
    state = state.copyWith(isEmailLoading: true, emailErrorKey: null);
    try {
      await authRepository.registerEmailPassword(
        email: email,
        password: password,
        confirmPassword: confirmPassword,
      );
      state = state.copyWith(isEmailLoading: false);
      return true;
    } catch (e) {
      state = state.copyWith(isEmailLoading: false, emailErrorKey: errorKeyFor(e));
      return false;
    }
  }

  Future<bool> submitEmailLogin({
    required String email,
    required String password,
  }) async {
    state = state.copyWith(isEmailLoading: true, emailErrorKey: null);
    try {
      await authRepository.emailPasswordLogin(
        email: email,
        password: password,
      );
      state = state.copyWith(isEmailLoading: false);
      return true;
    } catch (e) {
      state = state.copyWith(isEmailLoading: false, emailErrorKey: errorKeyFor(e));
      return false;
    }
  }

  Future<bool> submitGoogleSignIn(String idToken) async {
    state = state.copyWith(isGoogleLoading: true, emailErrorKey: null);
    try {
      await authRepository.googleSignIn(idToken);
      state = state.copyWith(isGoogleLoading: false);
      return true;
    } catch (e) {
      state = state.copyWith(isGoogleLoading: false, emailErrorKey: errorKeyFor(e));
      return false;
    }
  }

  Future<bool> submitIdAustriaSignIn({required String code, String? oauthState}) async {
    state = state.copyWith(isIdAustriaLoading: true, emailErrorKey: null);
    try {
      await authRepository.idAustriaSignIn(code: code, state: oauthState);
      state = state.copyWith(isIdAustriaLoading: false);
      return true;
    } catch (e) {
      state = state.copyWith(isIdAustriaLoading: false, emailErrorKey: errorKeyFor(e));
      return false;
    }
  }
}

final authProvider = StateNotifierProvider.autoDispose
    .family<AuthNotifier, AuthState, ApiClient>(
  (ref, apiClient) {
    return AuthNotifier(authRepository: AuthRepositoryImpl(apiClient));
  },
);
