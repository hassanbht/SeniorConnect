import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../data/auth_repository.dart';

class EmailLinkState {
  const EmailLinkState({
    this.codeSent = false,
    this.isLoading = false,
    this.errorKey,
  });

  final bool codeSent;
  final bool isLoading;
  final String? errorKey;

  EmailLinkState copyWith({
    bool? codeSent,
    bool? isLoading,
    Object? errorKey = _sentinel,
  }) {
    return EmailLinkState(
      codeSent: codeSent ?? this.codeSent,
      isLoading: isLoading ?? this.isLoading,
      errorKey: errorKey == _sentinel ? this.errorKey : errorKey as String?,
    );
  }
}

const _sentinel = Object();

class EmailLinkNotifier extends StateNotifier<EmailLinkState> {
  EmailLinkNotifier({required this.authRepository})
    : super(const EmailLinkState());

  final AuthRepository authRepository;

  String errorKeyFor(Object error) =>
      error is DioException ? mapDioError(error).l10nKey : 'errors.generic';

  Future<bool> requestCode(String email) async {
    state = state.copyWith(isLoading: true, errorKey: null);
    try {
      await authRepository.requestEmailMagicLink(email);
      state = state.copyWith(isLoading: false, codeSent: true);
      return true;
    } catch (e) {
      state = state.copyWith(isLoading: false, errorKey: errorKeyFor(e));
      return false;
    }
  }

  Future<bool> verifyCode(String email, String code) async {
    state = state.copyWith(isLoading: true, errorKey: null);
    try {
      await authRepository.verifyEmailMagicLink(email, code);
      state = state.copyWith(isLoading: false);
      return true;
    } catch (e) {
      state = state.copyWith(isLoading: false, errorKey: errorKeyFor(e));
      return false;
    }
  }
}

final emailLinkProvider = StateNotifierProvider.autoDispose
    .family<EmailLinkNotifier, EmailLinkState, ApiClient>((ref, apiClient) {
      return EmailLinkNotifier(authRepository: AuthRepositoryImpl(apiClient));
    });
