import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../data/auth_repository.dart';

class TotpEnrollmentState {
  const TotpEnrollmentState({
    this.enrollment,
    this.isLoading = true,
    this.isConfirming = false,
    this.confirmed = false,
    this.errorKey,
  });

  final TotpEnrollment? enrollment;
  final bool isLoading;
  final bool isConfirming;
  final bool confirmed;
  final String? errorKey;

  TotpEnrollmentState copyWith({
    TotpEnrollment? enrollment,
    bool? isLoading,
    bool? isConfirming,
    bool? confirmed,
    Object? errorKey = _sentinel,
  }) {
    return TotpEnrollmentState(
      enrollment: enrollment ?? this.enrollment,
      isLoading: isLoading ?? this.isLoading,
      isConfirming: isConfirming ?? this.isConfirming,
      confirmed: confirmed ?? this.confirmed,
      errorKey: errorKey == _sentinel ? this.errorKey : errorKey as String?,
    );
  }
}

const _sentinel = Object();

class TotpEnrollmentNotifier extends StateNotifier<TotpEnrollmentState> {
  TotpEnrollmentNotifier({required this.authRepository})
      : super(const TotpEnrollmentState()) {
    startEnrollment();
  }

  final AuthRepository authRepository;

  Future<void> startEnrollment() async {
    state = state.copyWith(isLoading: true, errorKey: null);
    try {
      final enrollment = await authRepository.enrollTotp();
      state = state.copyWith(isLoading: false, enrollment: enrollment);
    } catch (_) {
      state = state.copyWith(isLoading: false, errorKey: 'errors.generic');
    }
  }

  Future<bool> confirm(String code) async {
    state = state.copyWith(isConfirming: true, errorKey: null);
    try {
      await authRepository.confirmTotpEnrollment(code);
      state = state.copyWith(isConfirming: false, confirmed: true);
      return true;
    } catch (_) {
      state = state.copyWith(
        isConfirming: false,
        errorKey: 'auth.totp.invalid_code',
      );
      return false;
    }
  }
}

final totpEnrollmentProvider = StateNotifierProvider.autoDispose
    .family<TotpEnrollmentNotifier, TotpEnrollmentState, ApiClient>(
  (ref, apiClient) {
    return TotpEnrollmentNotifier(authRepository: AuthRepositoryImpl(apiClient));
  },
);
