import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:easy_localization/easy_localization.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../data/intake_form_repository.dart';

class IntakeFormScreenState {
  const IntakeFormScreenState({
    this.isLoading = true,
    this.loadErrorKey,
    this.form,
    this.answers = const {},
    this.criminalClearance = false,
    this.gdprConsent = false,
    this.eventOptIn = false,
    this.isSubmitting = false,
    this.submitErrorKey,
    this.validationErrors = const [],
  });

  final bool isLoading;
  final String? loadErrorKey;
  final IntakeForm? form;
  final Map<String, dynamic> answers;
  final bool criminalClearance;
  final bool gdprConsent;
  final bool eventOptIn;
  final bool isSubmitting;
  final String? submitErrorKey;
  final List<String> validationErrors;

  IntakeFormScreenState copyWith({
    bool? isLoading,
    String? loadErrorKey,
    IntakeForm? form,
    Map<String, dynamic>? answers,
    bool? criminalClearance,
    bool? gdprConsent,
    bool? eventOptIn,
    bool? isSubmitting,
    String? submitErrorKey,
    List<String>? validationErrors,
  }) {
    return IntakeFormScreenState(
      isLoading: isLoading ?? this.isLoading,
      loadErrorKey: loadErrorKey,
      form: form ?? this.form,
      answers: answers ?? this.answers,
      criminalClearance: criminalClearance ?? this.criminalClearance,
      gdprConsent: gdprConsent ?? this.gdprConsent,
      eventOptIn: eventOptIn ?? this.eventOptIn,
      isSubmitting: isSubmitting ?? this.isSubmitting,
      submitErrorKey: submitErrorKey,
      validationErrors: validationErrors ?? this.validationErrors,
    );
  }
}

class IntakeFormNotifier extends StateNotifier<IntakeFormScreenState> {
  IntakeFormNotifier({
    required this.organizationId,
    required this.formType,
    required this.apiClient,
    IntakeFormRepository? repository,
  })  : _repository = repository ?? IntakeFormRepositoryImpl(apiClient),
        super(const IntakeFormScreenState()) {
    load();
  }

  final String organizationId;
  final IntakeFormType formType;
  final ApiClient apiClient;
  final IntakeFormRepository _repository;

  Future<void> load() async {
    state = state.copyWith(isLoading: true, loadErrorKey: null);
    try {
      final form = await _repository.getForm(organizationId, formType);
      final answers = <String, dynamic>{};
      for (final section in form.sections) {
        for (final field in section.fields) {
          switch (field.fieldType) {
            case IntakeFieldType.text:
            case IntakeFieldType.textarea:
              break;
            case IntakeFieldType.multiChoice:
            case IntakeFieldType.timeSlots:
              answers[field.fieldKey] = <String>{};
            case IntakeFieldType.boolean:
              answers[field.fieldKey] = false;
            case IntakeFieldType.singleChoice:
            case IntakeFieldType.date:
              break;
          }
        }
      }

      state = state.copyWith(
        form: form,
        isLoading: false,
        answers: answers,
      );
    } on DioException catch (e) {
      state = state.copyWith(
        isLoading: false,
        loadErrorKey: mapDioError(e).l10nKey,
      );
    } catch (_) {
      state = state.copyWith(
        isLoading: false,
        loadErrorKey: 'errors.generic',
      );
    }
  }

  void setAnswer(String fieldKey, dynamic value) {
    final updated = Map<String, dynamic>.from(state.answers);
    updated[fieldKey] = value;
    state = state.copyWith(answers: updated);
  }

  void setCriminalClearance(bool value) {
    state = state.copyWith(criminalClearance: value);
  }

  void setGdprConsent(bool value) {
    state = state.copyWith(gdprConsent: value);
  }

  void setEventOptIn(bool value) {
    state = state.copyWith(eventOptIn: value);
  }

  bool _isFieldEmpty(
    IntakeFormField field,
    Map<String, String> textValues,
  ) {
    final value = state.answers[field.fieldKey];
    return switch (field.fieldType) {
      IntakeFieldType.text || IntakeFieldType.textarea =>
        (textValues[field.fieldKey] ?? '').trim().isEmpty,
      IntakeFieldType.singleChoice => value == null,
      IntakeFieldType.multiChoice || IntakeFieldType.timeSlots =>
        (value as Set<String>?)?.isEmpty ?? true,
      IntakeFieldType.boolean => value != true,
      IntakeFieldType.date => value == null,
    };
  }

  List<String> _collectValidationErrors(
    IntakeForm form,
    Map<String, String> textValues,
  ) {
    final errors = <String>[];
    for (final section in form.sections) {
      for (final field in section.fields) {
        if (field.isRequired && _isFieldEmpty(field, textValues)) {
          errors.add(field.labelKey.tr());
        }
      }
    }
    if (!state.criminalClearance) {
      errors.add('intake_form.criminal_clearance_required'.tr());
    }
    if (!state.gdprConsent) {
      errors.add('intake_form.gdpr_consent_required'.tr());
    }
    return errors;
  }

  Map<String, dynamic> _buildSubmissionData(
    IntakeForm form,
    Map<String, String> textValues,
  ) {
    final data = <String, dynamic>{};
    for (final section in form.sections) {
      for (final field in section.fields) {
        switch (field.fieldType) {
          case IntakeFieldType.text:
          case IntakeFieldType.textarea:
            final text = (textValues[field.fieldKey] ?? '').trim();
            if (text.isNotEmpty) data[field.fieldKey] = text;
          case IntakeFieldType.singleChoice:
            final value = state.answers[field.fieldKey] as String?;
            if (value != null) data[field.fieldKey] = value;
          case IntakeFieldType.multiChoice:
          case IntakeFieldType.timeSlots:
            final value = state.answers[field.fieldKey] as Set<String>?;
            if (value != null && value.isNotEmpty) {
              data[field.fieldKey] = value.toList();
            }
          case IntakeFieldType.boolean:
            data[field.fieldKey] = state.answers[field.fieldKey] == true;
          case IntakeFieldType.date:
            final value = state.answers[field.fieldKey] as DateTime?;
            if (value != null) {
              data[field.fieldKey] = value.toIso8601String();
            }
        }
      }
    }
    return data;
  }

  Future<bool> submit(Map<String, String> textValues) async {
    final form = state.form;
    if (form == null) return false;

    final errors = _collectValidationErrors(form, textValues);
    if (errors.isNotEmpty) {
      state = state.copyWith(validationErrors: errors);
      return false;
    }

    state = state.copyWith(
      isSubmitting: true,
      submitErrorKey: null,
      validationErrors: const [],
    );

    try {
      await _repository.submit(
        organizationId,
        form.id,
        submissionDataJson:
            jsonEncode(_buildSubmissionData(form, textValues)),
        criminalClearanceDeclared: state.criminalClearance,
        gdprConsentAccepted: state.gdprConsent,
        eventInvitationOptIn: state.eventOptIn,
      );
      state = state.copyWith(isSubmitting: false);
      return true;
    } on DioException catch (e) {
      state = state.copyWith(
        isSubmitting: false,
        submitErrorKey: mapDioError(e).l10nKey,
      );
      return false;
    } catch (_) {
      state = state.copyWith(
        isSubmitting: false,
        submitErrorKey: 'errors.generic',
      );
      return false;
    }
  }
}

class IntakeFormParams {
  const IntakeFormParams({
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
      (other is IntakeFormParams &&
          other.organizationId == organizationId &&
          other.formType == formType &&
          identical(other.apiClient, apiClient));

  @override
  int get hashCode => Object.hash(organizationId, formType, apiClient);
}

final intakeFormProvider = StateNotifierProvider.autoDispose.family<
    IntakeFormNotifier,
    IntakeFormScreenState,
    IntakeFormParams>(
  (ref, params) => IntakeFormNotifier(
    organizationId: params.organizationId,
    formType: params.formType,
    apiClient: params.apiClient,
  ),
);
