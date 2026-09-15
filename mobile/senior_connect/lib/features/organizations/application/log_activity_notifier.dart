// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
part 'log_activity_notifier.freezed.dart';
part 'log_activity_notifier.g.dart';
@freezed
class LogActivityState with _ {
  const factory LogActivityState({
    @Default(60) int durationMinutes,
    @Default('help.category.shopping') String selectedCategoryKey,
    @Default(1) int insuranceContext,
    @Default(0) int transportMode,
    @Default(false) bool isSubmitting,
    String? errorMessage,
  }) = _LogActivityState;
}
@riverpod
class LogActivityNotifier extends _ {
  @override
  LogActivityState build() => const LogActivityState();
  void setDuration(int m) => state = state.copyWith(durationMinutes: m);
  void setCategory(String k) => state = state.copyWith(selectedCategoryKey: k);
  void setInsuranceContext(int c) => state = state.copyWith(insuranceContext: c);
  void setTransportMode(int m) => state = state.copyWith(transportMode: m);
  void setSubmitting(bool v) => state = state.copyWith(isSubmitting: v, errorMessage: null);
  void setError(String msg) => state = state.copyWith(errorMessage: msg, isSubmitting: false);
  void prefill(String catKey, int duration) => state = state.copyWith(selectedCategoryKey: catKey, durationMinutes: duration);
}
