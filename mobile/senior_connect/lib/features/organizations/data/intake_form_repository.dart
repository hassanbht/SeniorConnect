// lib/features/organizations/data/intake_form_repository.dart
//
// P2-40: Dynamic organization intake forms & review — wraps
// /api/v1/organizations/{id}/forms/* (ADR-021, BR-ORG-FORM).
//
// Backend enums (FormType, FieldType, SubmissionStatus) serialize as plain
// JSON integers (no JsonStringEnumConverter is registered on the API), so
// these Dart enums must be declared in the exact same order as their C#
// counterparts — see backend/modules/Organizations/Domain/IntakeForms.cs.

import 'dart:convert';

import '../../../core/network/api_client.dart';

enum IntakeFormType { volunteer, helpSeeker }

enum IntakeFieldType {
  text,
  textarea,
  singleChoice,
  multiChoice,
  boolean,
  date,
  timeSlots,
}

enum IntakeSubmissionStatus { draft, submitted, approved, declined }

String _formTypePathSegment(IntakeFormType type) =>
    type == IntakeFormType.volunteer ? 'volunteer' : 'help_seeker';

/// A single question within a section. Matches `IntakeFormFieldDto` — also
/// used as-is for the nested shape returned inside a form's sections.
class IntakeFormField {
  const IntakeFormField({
    required this.id,
    required this.sectionId,
    required this.fieldKey,
    required this.labelKey,
    required this.fieldType,
    required this.isRequired,
    required this.options,
    required this.sortOrder,
  });

  factory IntakeFormField.fromJson(Map<String, dynamic> json) {
    final optionsJson = json['optionsJson'] as String?;
    return IntakeFormField(
      id: json['id'] as String,
      sectionId: json['sectionId'] as String,
      fieldKey: json['fieldKey'] as String,
      labelKey: json['labelKey'] as String,
      fieldType: IntakeFieldType.values[json['fieldType'] as int],
      isRequired: json['isRequired'] as bool,
      options: optionsJson == null || optionsJson.isEmpty
          ? const <String>[]
          : (jsonDecode(optionsJson) as List<dynamic>).cast<String>(),
      sortOrder: json['sortOrder'] as int,
    );
  }

  final String id;
  final String sectionId;
  final String fieldKey;
  final String labelKey;
  final IntakeFieldType fieldType;
  final bool isRequired;
  final List<String> options;
  final int sortOrder;
}

/// A group of fields. Matches `IntakeFormSectionDetailDto`; `fields` is empty
/// when this is decoded from a flat (non-nested) section response.
class IntakeFormSection {
  const IntakeFormSection({
    required this.id,
    required this.formId,
    required this.title,
    required this.description,
    required this.sortOrder,
    required this.fields,
  });

  factory IntakeFormSection.fromJson(Map<String, dynamic> json) =>
      IntakeFormSection(
        id: json['id'] as String,
        formId: json['formId'] as String,
        title: json['title'] as String,
        description: json['description'] as String?,
        sortOrder: json['sortOrder'] as int,
        fields: (json['fields'] as List<dynamic>?)
                ?.map((e) => IntakeFormField.fromJson(e as Map<String, dynamic>))
                .toList() ??
            const <IntakeFormField>[],
      );

  final String id;
  final String formId;
  final String title;
  final String? description;
  final int sortOrder;
  final List<IntakeFormField> fields;
}

/// Matches `OrganizationIntakeFormDetailDto`; `sections` is empty when this
/// is decoded from a flat (create/update) form response.
class IntakeForm {
  const IntakeForm({
    required this.id,
    required this.organizationId,
    required this.formType,
    required this.title,
    required this.description,
    required this.isActive,
    required this.version,
    required this.sections,
  });

  factory IntakeForm.fromJson(Map<String, dynamic> json) => IntakeForm(
        id: json['id'] as String,
        organizationId: json['organizationId'] as String,
        formType: IntakeFormType.values[json['formType'] as int],
        title: json['title'] as String,
        description: json['description'] as String?,
        isActive: json['isActive'] as bool,
        version: json['version'] as int,
        sections: (json['sections'] as List<dynamic>?)
                ?.map((e) =>
                    IntakeFormSection.fromJson(e as Map<String, dynamic>))
                .toList() ??
            const <IntakeFormSection>[],
      );

  final String id;
  final String organizationId;
  final IntakeFormType formType;
  final String title;
  final String? description;
  final bool isActive;
  final int version;
  final List<IntakeFormSection> sections;
}

/// Matches `IntakeFormSubmissionDto`. `submissionData` is the decoded
/// `submissionDataJson`, keyed by each field's `fieldKey`.
class IntakeFormSubmission {
  const IntakeFormSubmission({
    required this.id,
    required this.formId,
    required this.organizationId,
    required this.userId,
    required this.status,
    required this.submissionData,
    required this.criminalClearanceDeclared,
    required this.gdprConsentAccepted,
    required this.eventInvitationOptIn,
    required this.submittedAtUtc,
    required this.decidedAtUtc,
    required this.reviewNotes,
  });

  factory IntakeFormSubmission.fromJson(Map<String, dynamic> json) {
    final dataJson = json['submissionDataJson'] as String?;
    return IntakeFormSubmission(
      id: json['id'] as String,
      formId: json['formId'] as String,
      organizationId: json['organizationId'] as String?,
      userId: json['userId'] as String,
      status: IntakeSubmissionStatus.values[json['status'] as int],
      submissionData: dataJson == null || dataJson.isEmpty
          ? const <String, dynamic>{}
          : Map<String, dynamic>.from(jsonDecode(dataJson) as Map),
      criminalClearanceDeclared: json['criminalClearanceDeclared'] as bool,
      gdprConsentAccepted: json['gdprConsentAccepted'] as bool,
      eventInvitationOptIn: json['eventInvitationOptIn'] as bool,
      submittedAtUtc: json['submittedAtUtc'] == null
          ? null
          : DateTime.parse(json['submittedAtUtc'] as String),
      decidedAtUtc: json['decidedAtUtc'] == null
          ? null
          : DateTime.parse(json['decidedAtUtc'] as String),
      reviewNotes: json['reviewNotes'] as String?,
    );
  }

  final String id;
  final String formId;
  final String? organizationId;
  final String userId;
  final IntakeSubmissionStatus status;
  final Map<String, dynamic> submissionData;
  final bool criminalClearanceDeclared;
  final bool gdprConsentAccepted;
  final bool eventInvitationOptIn;
  final DateTime? submittedAtUtc;
  final DateTime? decidedAtUtc;
  final String? reviewNotes;
}

abstract class IntakeFormRepository {
  Future<IntakeForm> createForm(
    String organizationId, {
    required IntakeFormType formType,
    required String title,
    String? description,
  });

  /// The nested detail shape — sections and each section's fields.
  Future<IntakeForm> getForm(String organizationId, IntakeFormType formType);

  Future<IntakeForm> updateForm(
    String organizationId,
    String formId, {
    required String title,
    String? description,
  });

  Future<IntakeFormSection> addSection(
    String organizationId,
    String formId, {
    required String title,
    String? description,
    int sortOrder = 0,
  });

  Future<IntakeFormSection> updateSection(
    String organizationId,
    String formId,
    String sectionId, {
    required String title,
    String? description,
    int sortOrder = 0,
  });

  Future<void> deleteSection(
    String organizationId,
    String formId,
    String sectionId,
  );

  Future<IntakeFormField> addField(
    String organizationId,
    String formId,
    String sectionId, {
    required String fieldKey,
    required String labelKey,
    required IntakeFieldType fieldType,
    bool isRequired = false,
    String? optionsJson,
    int sortOrder = 0,
  });

  Future<IntakeFormField> updateField(
    String organizationId,
    String formId,
    String sectionId,
    String fieldId, {
    required String fieldKey,
    required String labelKey,
    required IntakeFieldType fieldType,
    bool isRequired = false,
    String? optionsJson,
    int sortOrder = 0,
  });

  Future<void> deleteField(
    String organizationId,
    String formId,
    String sectionId,
    String fieldId,
  );

  /// The applicant submits their answers here.
  Future<IntakeFormSubmission> submit(
    String organizationId,
    String formId, {
    required String submissionDataJson,
    required bool criminalClearanceDeclared,
    required bool gdprConsentAccepted,
    required bool eventInvitationOptIn,
  });

  /// The coordinator's review queue.
  Future<List<IntakeFormSubmission>> getSubmissions(
    String organizationId,
    String formId,
  );

  Future<IntakeFormSubmission> decide(
    String organizationId,
    String formId,
    String submissionId, {
    required bool approve,
    String? reviewNotes,
  });

  /// Seeds the FWZ Innsbruck-Land reference template for first-time setup.
  Future<void> activateFwzTemplate(
    String organizationId,
    IntakeFormType formType,
  );
}

class IntakeFormRepositoryImpl implements IntakeFormRepository {
  IntakeFormRepositoryImpl(this._apiClient);

  final ApiClient _apiClient;

  String _formsPath(String organizationId) =>
      '/api/v1/organizations/$organizationId/forms';

  @override
  Future<IntakeForm> createForm(
    String organizationId, {
    required IntakeFormType formType,
    required String title,
    String? description,
  }) async {
    final response = await _apiClient.post<Map<String, dynamic>>(
      _formsPath(organizationId),
      data: {
        'formType': formType.index,
        'title': title,
        'description': description,
      },
    );
    return IntakeForm.fromJson(response);
  }

  @override
  Future<IntakeForm> getForm(
    String organizationId,
    IntakeFormType formType,
  ) async {
    final response = await _apiClient.get<Map<String, dynamic>>(
      '${_formsPath(organizationId)}/${_formTypePathSegment(formType)}',
    );
    return IntakeForm.fromJson(response);
  }

  @override
  Future<IntakeForm> updateForm(
    String organizationId,
    String formId, {
    required String title,
    String? description,
  }) async {
    final response = await _apiClient.put<Map<String, dynamic>>(
      '${_formsPath(organizationId)}/$formId',
      data: {'title': title, 'description': description},
    );
    return IntakeForm.fromJson(response);
  }

  @override
  Future<IntakeFormSection> addSection(
    String organizationId,
    String formId, {
    required String title,
    String? description,
    int sortOrder = 0,
  }) async {
    final response = await _apiClient.post<Map<String, dynamic>>(
      '${_formsPath(organizationId)}/$formId/sections',
      data: {
        'title': title,
        'description': description,
        'sortOrder': sortOrder,
      },
    );
    return IntakeFormSection.fromJson(response);
  }

  @override
  Future<IntakeFormSection> updateSection(
    String organizationId,
    String formId,
    String sectionId, {
    required String title,
    String? description,
    int sortOrder = 0,
  }) async {
    final response = await _apiClient.put<Map<String, dynamic>>(
      '${_formsPath(organizationId)}/$formId/sections/$sectionId',
      data: {
        'title': title,
        'description': description,
        'sortOrder': sortOrder,
      },
    );
    return IntakeFormSection.fromJson(response);
  }

  @override
  Future<void> deleteSection(
    String organizationId,
    String formId,
    String sectionId,
  ) async {
    await _apiClient.delete<void>(
      '${_formsPath(organizationId)}/$formId/sections/$sectionId',
    );
  }

  @override
  Future<IntakeFormField> addField(
    String organizationId,
    String formId,
    String sectionId, {
    required String fieldKey,
    required String labelKey,
    required IntakeFieldType fieldType,
    bool isRequired = false,
    String? optionsJson,
    int sortOrder = 0,
  }) async {
    final response = await _apiClient.post<Map<String, dynamic>>(
      '${_formsPath(organizationId)}/$formId/sections/$sectionId/fields',
      data: {
        'fieldKey': fieldKey,
        'labelKey': labelKey,
        'fieldType': fieldType.index,
        'isRequired': isRequired,
        'optionsJson': optionsJson,
        'sortOrder': sortOrder,
      },
    );
    return IntakeFormField.fromJson(response);
  }

  @override
  Future<IntakeFormField> updateField(
    String organizationId,
    String formId,
    String sectionId,
    String fieldId, {
    required String fieldKey,
    required String labelKey,
    required IntakeFieldType fieldType,
    bool isRequired = false,
    String? optionsJson,
    int sortOrder = 0,
  }) async {
    final response = await _apiClient.put<Map<String, dynamic>>(
      '${_formsPath(organizationId)}/$formId/sections/$sectionId/fields/$fieldId',
      data: {
        'fieldKey': fieldKey,
        'labelKey': labelKey,
        'fieldType': fieldType.index,
        'isRequired': isRequired,
        'optionsJson': optionsJson,
        'sortOrder': sortOrder,
      },
    );
    return IntakeFormField.fromJson(response);
  }

  @override
  Future<void> deleteField(
    String organizationId,
    String formId,
    String sectionId,
    String fieldId,
  ) async {
    await _apiClient.delete<void>(
      '${_formsPath(organizationId)}/$formId/sections/$sectionId/fields/$fieldId',
    );
  }

  @override
  Future<IntakeFormSubmission> submit(
    String organizationId,
    String formId, {
    required String submissionDataJson,
    required bool criminalClearanceDeclared,
    required bool gdprConsentAccepted,
    required bool eventInvitationOptIn,
  }) async {
    final response = await _apiClient.post<Map<String, dynamic>>(
      '${_formsPath(organizationId)}/$formId/submissions',
      data: {
        'submissionDataJson': submissionDataJson,
        'criminalClearanceDeclared': criminalClearanceDeclared,
        'gdprConsentAccepted': gdprConsentAccepted,
        'eventInvitationOptIn': eventInvitationOptIn,
      },
    );
    return IntakeFormSubmission.fromJson(response);
  }

  @override
  Future<List<IntakeFormSubmission>> getSubmissions(
    String organizationId,
    String formId,
  ) async {
    final response = await _apiClient.get<List<dynamic>>(
      '${_formsPath(organizationId)}/$formId/submissions',
    );
    return response
        .map((e) => IntakeFormSubmission.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  @override
  Future<IntakeFormSubmission> decide(
    String organizationId,
    String formId,
    String submissionId, {
    required bool approve,
    String? reviewNotes,
  }) async {
    final response = await _apiClient.put<Map<String, dynamic>>(
      '${_formsPath(organizationId)}/$formId/submissions/$submissionId:decide',
      data: {'approve': approve, 'reviewNotes': reviewNotes},
    );
    return IntakeFormSubmission.fromJson(response);
  }

  @override
  Future<void> activateFwzTemplate(
    String organizationId,
    IntakeFormType formType,
  ) async {
    // The route requires a {formId:guid} segment, but the handler never
    // reads it (it always (re)creates the active form for `organizationId` +
    // `formType`) — see OrganizationEndpoints.cs's ":activate-template" route
    // and OrganizationService.ActivateFwzTemplateAsync. Guid.empty is a
    // harmless placeholder.
    await _apiClient.post<void>(
      '${_formsPath(organizationId)}/00000000-0000-0000-0000-000000000000:activate-template',
      data: {'formType': formType.index},
    );
  }
}
