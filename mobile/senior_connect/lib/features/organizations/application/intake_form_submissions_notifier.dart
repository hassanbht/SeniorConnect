// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
import '../data/intake_form_repository.dart';
part 'intake_form_submissions_notifier.freezed.dart';
part 'intake_form_submissions_notifier.g.dart';
@freezed
class IntakeFormSubmissionsState with _ {
  const factory IntakeFormSubmissionsState({
    @Default(true) bool isLoading,
    IntakeForm? form,
    @Default([]) List<IntakeFormSubmission> submissions,
    String? errorKey,
  }) = _IntakeFormSubmissionsState;
}
@riverpod
class IntakeFormSubmissionsNotifier extends _ {
  @override
  IntakeFormSubmissionsState build(IntakeFormRepository repo, String organizationId, IntakeFormType formType) {
    Future(()=> _load(repo, organizationId, formType));
    return const IntakeFormSubmissionsState();
  }
  Future<void> _load(IntakeFormRepository repo, String orgId, IntakeFormType formType) async {
    state = state.copyWith(isLoading: true, errorKey: null);
    try {
      final form = await repo.getForm(orgId, formType);
      final subs = await repo.getSubmissions(orgId, form.id);
      state = state.copyWith(form: form, submissions: subs, isLoading: false);
    } catch (_) { state = state.copyWith(isLoading: false, errorKey: 'errors.generic'); }
  }
  Future<void> approve(String submissionId, IntakeFormRepository repo, String orgId, IntakeFormType ft) async {
    try { await repo.approveSubmission(submissionId); await _load(repo, orgId, ft); }
    catch (_) { state = state.copyWith(errorKey: 'errors.generic'); }
  }
  Future<void> reject(String submissionId, String reason, IntakeFormRepository repo, String orgId, IntakeFormType ft) async {
    try { await repo.rejectSubmission(submissionId, reason); await _load(repo, orgId, ft); }
    catch (_) { state = state.copyWith(errorKey: 'errors.generic'); }
  }
}
