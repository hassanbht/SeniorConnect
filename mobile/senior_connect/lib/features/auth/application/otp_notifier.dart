import 'dart:async';
import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../data/auth_repository.dart';
import '../presentation/otp_verify_screen.dart';

class OtpState {
  const OtpState({
    this.isLoading = false,
    this.isResending = false,
    this.errorKey,
    this.remainingAttempts = 5,
    this.secondsLeft = 300,
  });

  final bool isLoading;
  final bool isResending;
  final String? errorKey;
  final int remainingAttempts;
  final int secondsLeft;

  OtpState copyWith({
    bool? isLoading,
    bool? isResending,
    Object? errorKey = _sentinel,
    int? remainingAttempts,
    int? secondsLeft,
  }) {
    return OtpState(
      isLoading: isLoading ?? this.isLoading,
      isResending: isResending ?? this.isResending,
      errorKey: errorKey == _sentinel ? this.errorKey : errorKey as String?,
      remainingAttempts: remainingAttempts ?? this.remainingAttempts,
      secondsLeft: secondsLeft ?? this.secondsLeft,
    );
  }
}

const _sentinel = Object();

class OtpNotifier extends StateNotifier<OtpState> {
  OtpNotifier({
    required this.authRepository,
    required this.apiClient,
    required this.phone,
    required this.purpose,
  }) : super(const OtpState()) {
    _startTimer();
  }

  final AuthRepository authRepository;
  final ApiClient apiClient;
  final String phone;
  final OtpPurpose purpose;

  Timer? _timer;

  void _startTimer() {
    _timer?.cancel();
    _timer = Timer.periodic(const Duration(seconds: 1), (_) {
      if (state.secondsLeft > 0) {
        state = state.copyWith(secondsLeft: state.secondsLeft - 1);
      } else {
        _timer?.cancel();
      }
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<bool> verify(String code) async {
    if (code.length != 6 || state.isLoading) return false;
    state = state.copyWith(isLoading: true, errorKey: null);

    try {
      switch (purpose) {
        case OtpPurpose.login:
          await authRepository.verifyPhoneOtp(phone, code);
        case OtpPurpose.phoneVerification:
          await authRepository.verifyProfilePhone(phone, code);
        case OtpPurpose.phoneChange:
          await authRepository.verifyPhoneChange(phone, code);
          await apiClient.clearTokens();
      }
      state = state.copyWith(isLoading: false);
      return true;
    } catch (e) {
      String key = 'errors.generic';
      int attempts = state.remainingAttempts;
      if (e is DioException) {
        final status = e.response?.statusCode;
        if (status == 400 || status == 401) {
          attempts = (attempts - 1).clamp(0, 5);
          key = attempts == 0 ? 'auth.too_many_attempts' : 'auth.invalid_code';
        } else if (status == 429) {
          key = 'auth.rate_limited';
        }
      }
      state = state.copyWith(
        isLoading: false,
        errorKey: key,
        remainingAttempts: attempts,
      );
      return false;
    }
  }

  Future<void> resend() async {
    state = state.copyWith(isResending: true, errorKey: null);
    try {
      await authRepository.requestPhoneOtp(phone);
      state = state.copyWith(
        isResending: false,
        secondsLeft: 300,
        remainingAttempts: 5,
      );
      _startTimer();
    } catch (_) {
      state = state.copyWith(isResending: false, errorKey: 'errors.generic');
    }
  }
}

class OtpParams {
  const OtpParams({
    required this.apiClient,
    required this.phone,
    required this.purpose,
  });
  final ApiClient apiClient;
  final String phone;
  final OtpPurpose purpose;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is OtpParams &&
          other.phone == phone &&
          other.purpose == purpose &&
          identical(other.apiClient, apiClient));

  @override
  int get hashCode => Object.hash(apiClient, phone, purpose);
}

final otpProvider =
    StateNotifierProvider.autoDispose.family<OtpNotifier, OtpState, OtpParams>(
  (ref, params) {
    return OtpNotifier(
      authRepository: AuthRepositoryImpl(params.apiClient),
      apiClient: params.apiClient,
      phone: params.phone,
      purpose: params.purpose,
    );
  },
);
