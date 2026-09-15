// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
import '../data/auth_repository.dart';
part 'totp_enrollment_notifier.freezed.dart';
part 'totp_enrollment_notifier.g.dart';
@freezed
class TotpEnrollmentState with _ {
  const factory TotpEnrollmentState({
    @Default(true) bool isLoading,
    @Default(false) bool isConfirming,
    @Default(false) bool confirmed,
    TotpEnrollment? enrollment,
    String? errorKey,
  }) = _TotpEnrollmentState;
}
@riverpod
class TotpEnrollmentNotifier extends _ {
  @override
  TotpEnrollmentState build(AuthRepository repo) {
    Future(()=> _start(repo));
    return const TotpEnrollmentState();
  }
  Future<void> _start(AuthRepository repo) async {
    state = state.copyWith(isLoading: true, errorKey: null);
    try {
      final e = await repo.enrollTotp();
      state = state.copyWith(enrollment: e, isLoading: false);
    } catch (_) {
      state = state.copyWith(errorKey: 'errors.generic', isLoading: false);
    }
  }
  Future<void> confirm(String code, AuthRepository repo) async {
    state = state.copyWith(isConfirming: true, errorKey: null);
    try {
      await repo.confirmTotpEnrollment(code);
      state = state.copyWith(confirmed: true);
    } catch (_) {
      state = state.copyWith(errorKey: 'errors.generic');
    } finally {
      state = state.copyWith(isConfirming: false);
    }
  }
}
