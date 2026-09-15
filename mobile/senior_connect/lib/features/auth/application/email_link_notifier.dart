// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
part 'email_link_notifier.freezed.dart';
part 'email_link_notifier.g.dart';
@freezed
class EmailLinkState with _ {
  const factory EmailLinkState({
    @Default(false) bool codeSent,
    @Default(false) bool isLoading,
    String? errorKey,
  }) = _EmailLinkState;
}
@riverpod
class EmailLinkNotifier extends _ {
  @override
  EmailLinkState build() => const EmailLinkState();
  void setLoading(bool v) => state = state.copyWith(isLoading: v, errorKey: null);
  void setCodeSent() => state = state.copyWith(codeSent: true, isLoading: false);
  void setError(String key) => state = state.copyWith(errorKey: key, isLoading: false);
  void resetToEmailStep() => state = state.copyWith(codeSent: false, errorKey: null);
}
