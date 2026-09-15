// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
import '../data/intake_form_repository.dart';
part 'intake_form_notifier.freezed.dart';
part 'intake_form_notifier.g.dart';
@freezed
class IntakeFormState with _ {
  const factory IntakeFormState({
    @Default(true) bool isLoading,
    IntakeForm? form,
    String? loadErrorKey,
    @Default(false) bool criminalClearance,
    @Default(false) bool gdprConsent,
    @Default(false) bool eventOptIn,
    @Default(false) bool isSubmitting,
    String? submitErrorKey,
    @Default([]) List<String> validationErrors,
  }) = _IntakeFormState;
}
@riverpod
class IntakeFormNotifier extends _ {
  @override
  IntakeFormState build(IntakeFormRepository repo, String organizationId, IntakeFormType formType) {
    Future(()=> _load(repo, organizationId, formType));
    return const IntakeFormState();
  }
  Future<void> _load(IntakeFormRepository repo, String orgId, IntakeFormType formType) async {
    state = state.copyWith(isLoading: true, loadErrorKey: null);
    try {
      final form = await repo.getForm(orgId, formType);
      state = state.copyWith(form: form, isLoading: false);
    } catch (_) { state = state.copyWith(isLoading: false, loadErrorKey: 'errors.generic'); }
  }
  void setCriminalClearance(bool v) => state = state.copyWith(criminalClearance: v);
  void setGdprConsent(bool v) => state = state.copyWith(gdprConsent: v);
  void setEventOptIn(bool v) => state = state.copyWith(eventOptIn: v);
  void setSubmitting(bool v) => state = state.copyWith(isSubmitting: v, submitErrorKey: null);
  void setSubmitError(String key) => state = state.copyWith(submitErrorKey: key, isSubmitting: false);
  void setValidationErrors(List<String> errors) => state = state.copyWith(validationErrors: errors);
}
