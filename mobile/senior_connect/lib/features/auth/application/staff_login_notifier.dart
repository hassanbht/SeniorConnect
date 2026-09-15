import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../data/auth_repository.dart';

class StaffLoginState {
  const StaffLoginState({
    this.isLoading = false,
    this.totpRequired = false,
    this.errorKey,
  });

  final bool isLoading;
  final bool totpRequired;
  final String? errorKey;

  StaffLoginState copyWith({
    bool? isLoading,
    bool? totpRequired,
    Object? errorKey = _sentinel,
  }) {
    return StaffLoginState(
      isLoading: isLoading ?? this.isLoading,
      totpRequired: totpRequired ?? this.totpRequired,
      errorKey: errorKey == _sentinel ? this.errorKey : errorKey as String?,
    );
  }
}

const _sentinel = Object();

class StaffLoginNotifier extends StateNotifier<StaffLoginState> {
  StaffLoginNotifier({required this.authRepository})
      : super(const StaffLoginState());

  final AuthRepository authRepository;

  Future<bool> submit({
    required String email,
    required String password,
    String? totpCode,
  }) async {
    state = state.copyWith(isLoading: true, errorKey: null);
    try {
      await authRepository.staffLogin(
        email: email,
        password: password,
        totpCode: totpCode,
      );
      state = state.copyWith(isLoading: false);
      return true;
    } on DioException catch (e) {
      if (e.response?.statusCode == 428) {
        state = state.copyWith(
          isLoading: false,
          totpRequired: true,
          errorKey: 'auth.staff.totp_required',
        );
      } else {
        state = state.copyWith(
          isLoading: false,
          errorKey: mapDioError(e).l10nKey,
        );
      }
      return false;
    } catch (_) {
      state = state.copyWith(isLoading: false, errorKey: 'errors.generic');
      return false;
    }
  }
}

final staffLoginProvider = StateNotifierProvider.autoDispose
    .family<StaffLoginNotifier, StaffLoginState, ApiClient>(
  (ref, apiClient) {
    return StaffLoginNotifier(authRepository: AuthRepositoryImpl(apiClient));
  },
);
