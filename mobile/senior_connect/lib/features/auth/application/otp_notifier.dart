// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'dart:async';
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
part 'otp_notifier.freezed.dart';
part 'otp_notifier.g.dart';
@freezed
class OtpState with _ {
  const factory OtpState({
    @Default(false) bool isLoading,
    @Default(5) int remainingAttempts,
    @Default(300) int secondsLeft,
    String? errorKey,
  }) = _OtpState;
}
@riverpod
class OtpNotifier extends _ {
  Timer? _timer;
  @override
  OtpState build() {
    ref.onDispose(() => _timer?.cancel());
    _timer = Timer.periodic(const Duration(seconds: 1), (_) {
      if (state.secondsLeft > 0) {
        state = state.copyWith(secondsLeft: state.secondsLeft - 1);
      } else { _timer?.cancel(); }
    });
    return const OtpState();
  }
  void setLoading(bool v) => state = state.copyWith(isLoading: v, errorKey: null);
  void setError(String key) => state = state.copyWith(errorKey: key, isLoading: false);
  void decrementAttempts() => state = state.copyWith(remainingAttempts: state.remainingAttempts - 1);
}
