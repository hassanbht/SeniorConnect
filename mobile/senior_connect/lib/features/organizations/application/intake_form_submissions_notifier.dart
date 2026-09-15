import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../data/intake_form_repository.dart';

class IntakeFormSubmissionsState {
  const IntakeFormSubmissionsState({
    this.isLoading = true,
    this.errorKey,
    this.form,
    this.submissions = const [],
    this.isDeciding = false,
    this.decisionErrorKey,
  });

  final bool isLoading;
  final String? errorKey;
  final IntakeForm? form;
  final List<IntakeFormSubmission> submissions;
  final bool isDeciding;
  final String? decisionErrorKey;

  Map<String, String> get fieldLabels => {
        for (final section in form?.sections ?? const <IntakeFormSection>[])
          for (final field in section.fields) field.fieldKey: field.labelKey,
      };

  IntakeFormSubmissionsState copyWith({
    bool? isLoading,
    String? errorKey,
    IntakeForm? form,
    List<IntakeFormSubmission>? submissions,
    bool? isDeciding,
    String? decisionErrorKey,
  }) {
    return IntakeFormSubmissionsState(
      isLoading: isLoading ?? this.isLoading,
      errorKey: errorKey,
      form: form ?? this.form,
      submissions: submissions ?? this.submissions,
      isDeciding: isDeciding ?? this.isDeciding,
      decisionErrorKey: decisionErrorKey,
    );
  }
}

class IntakeFormSubmissionsNotifier
    extends StateNotifier<IntakeFormSubmissionsState> {
  IntakeFormSubmissionsNotifier({
    required this.organizationId,
    required this.formType,
    required this.apiClient,
    IntakeFormRepository? repository,
  })  : _repository = repository ?? IntakeFormRepositoryImpl(apiClient),
        super(const IntakeFormSubmissionsState()) {
    load();
  }

  final String organizationId;
  final IntakeFormType formType;
  final ApiClient apiClient;
  final IntakeFormRepository _repository;

  Future<void> load() async {
    state = state.copyWith(isLoading: true, errorKey: null);
    try {
      final form = await _repository.getForm(organizationId, formType);
      final submissions =
          await _repository.getSubmissions(organizationId, form.id);
      state = state.copyWith(
        form: form,
        submissions: submissions,
        isLoading: false,
      );
    } on DioException catch (e) {
      state = state.copyWith(
        isLoading: false,
        errorKey: mapDioError(e).l10nKey,
      );
    } catch (_) {
      state = state.copyWith(
        isLoading: false,
        errorKey: 'errors.generic',
      );
    }
  }

  Future<bool> decide({
    required String submissionId,
    required bool approve,
    String? reviewNotes,
  }) async {
    final form = state.form;
    if (form == null) return false;

    state = state.copyWith(isDeciding: true, decisionErrorKey: null);
    try {
      await _repository.decide(
        organizationId,
        form.id,
        submissionId,
        approve: approve,
        reviewNotes: reviewNotes,
      );
      state = state.copyWith(isDeciding: false);
      await load();
      return true;
    } on DioException catch (e) {
      state = state.copyWith(
        isDeciding: false,
        decisionErrorKey: mapDioError(e).l10nKey,
      );
      return false;
    } catch (_) {
      state = state.copyWith(
        isDeciding: false,
        decisionErrorKey: 'errors.generic',
      );
      return false;
    }
  }
}

class IntakeFormSubmissionsParams {
  const IntakeFormSubmissionsParams({
    required this.organizationId,
    required this.formType,
    required this.apiClient,
  });

  final String organizationId;
  final IntakeFormType formType;
  final ApiClient apiClient;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is IntakeFormSubmissionsParams &&
          other.organizationId == organizationId &&
          other.formType == formType &&
          identical(other.apiClient, apiClient));

  @override
  int get hashCode => Object.hash(organizationId, formType, apiClient);
}

final intakeFormSubmissionsProvider = StateNotifierProvider.autoDispose.family<
    IntakeFormSubmissionsNotifier,
    IntakeFormSubmissionsState,
    IntakeFormSubmissionsParams>(
  (ref, params) => IntakeFormSubmissionsNotifier(
    organizationId: params.organizationId,
    formType: params.formType,
    apiClient: params.apiClient,
  ),
);
