// lib/features/organizations/presentation/intake_form_screen.dart
//
// P2-40: Applicant-facing dynamic renderer for an organization's intake
// form. Renders each field by FieldType, then always appends the two
// mandatory declarations (criminal clearance, GDPR consent) plus the
// optional event-invitation opt-in, regardless of what the dynamic fields
// contain — those three map directly to SubmitIntakeFormRequest.

import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_states.dart';
import '../data/intake_form_repository.dart';

class IntakeFormScreen extends StatefulWidget {
  const IntakeFormScreen({
    super.key,
    required this.organizationId,
    required this.formType,
    required this.apiClient,
  });

  final String organizationId;
  final IntakeFormType formType;
  final ApiClient apiClient;

  @override
  State<IntakeFormScreen> createState() => _IntakeFormScreenState();
}

class _IntakeFormScreenState extends State<IntakeFormScreen> {
  late final IntakeFormRepository _repository =
      IntakeFormRepositoryImpl(widget.apiClient);

  bool _isLoading = true;
  String? _loadErrorKey;
  IntakeForm? _form;

  final Map<String, dynamic> _answers = {};
  final Map<String, TextEditingController> _textControllers = {};

  bool _criminalClearance = false;
  bool _gdprConsent = false;
  bool _eventOptIn = false;

  bool _isSubmitting = false;
  String? _submitErrorKey;
  List<String> _validationErrors = const [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    for (final controller in _textControllers.values) {
      controller.dispose();
    }
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _isLoading = true;
      _loadErrorKey = null;
    });
    try {
      final form = await _repository.getForm(widget.organizationId, widget.formType);
      if (!mounted) return;
      setState(() {
        _form = form;
        _isLoading = false;
        for (final section in form.sections) {
          for (final field in section.fields) {
            switch (field.fieldType) {
              case IntakeFieldType.text:
              case IntakeFieldType.textarea:
                _textControllers[field.fieldKey] = TextEditingController();
              case IntakeFieldType.multiChoice:
              case IntakeFieldType.timeSlots:
                _answers[field.fieldKey] = <String>{};
              case IntakeFieldType.boolean:
                _answers[field.fieldKey] = false;
              case IntakeFieldType.singleChoice:
              case IntakeFieldType.date:
                break; // left null until answered
            }
          }
        }
      });
    } on DioException catch (e) {
      if (mounted) {
        setState(() {
          _isLoading = false;
          _loadErrorKey = mapDioError(e).l10nKey;
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _isLoading = false;
          _loadErrorKey = 'errors.generic';
        });
      }
    }
  }

  bool _isFieldEmpty(IntakeFormField field) {
    final value = _answers[field.fieldKey];
    return switch (field.fieldType) {
      IntakeFieldType.text || IntakeFieldType.textarea =>
        _textControllers[field.fieldKey]?.text.trim().isEmpty ?? true,
      IntakeFieldType.singleChoice => value == null,
      IntakeFieldType.multiChoice || IntakeFieldType.timeSlots =>
        (value as Set<String>?)?.isEmpty ?? true,
      IntakeFieldType.boolean => value != true,
      IntakeFieldType.date => value == null,
    };
  }

  List<String> _collectValidationErrors(IntakeForm form) {
    final errors = <String>[];
    for (final section in form.sections) {
      for (final field in section.fields) {
        if (field.isRequired && _isFieldEmpty(field)) {
          errors.add(field.labelKey.tr());
        }
      }
    }
    if (!_criminalClearance) {
      errors.add('intake_form.criminal_clearance_required'.tr());
    }
    if (!_gdprConsent) {
      errors.add('intake_form.gdpr_consent_required'.tr());
    }
    return errors;
  }

  Map<String, dynamic> _buildSubmissionData(IntakeForm form) {
    final data = <String, dynamic>{};
    for (final section in form.sections) {
      for (final field in section.fields) {
        switch (field.fieldType) {
          case IntakeFieldType.text:
          case IntakeFieldType.textarea:
            final text = _textControllers[field.fieldKey]?.text.trim() ?? '';
            if (text.isNotEmpty) data[field.fieldKey] = text;
          case IntakeFieldType.singleChoice:
            final value = _answers[field.fieldKey] as String?;
            if (value != null) data[field.fieldKey] = value;
          case IntakeFieldType.multiChoice:
          case IntakeFieldType.timeSlots:
            final value = _answers[field.fieldKey] as Set<String>?;
            if (value != null && value.isNotEmpty) {
              data[field.fieldKey] = value.toList();
            }
          case IntakeFieldType.boolean:
            data[field.fieldKey] = _answers[field.fieldKey] == true;
          case IntakeFieldType.date:
            final value = _answers[field.fieldKey] as DateTime?;
            if (value != null) {
              data[field.fieldKey] = value.toIso8601String();
            }
        }
      }
    }
    return data;
  }

  Future<void> _submit() async {
    final form = _form;
    if (form == null) return;

    final errors = _collectValidationErrors(form);
    if (errors.isNotEmpty) {
      setState(() => _validationErrors = errors);
      return;
    }

    setState(() {
      _isSubmitting = true;
      _submitErrorKey = null;
      _validationErrors = const [];
    });

    try {
      await _repository.submit(
        widget.organizationId,
        form.id,
        submissionDataJson: jsonEncode(_buildSubmissionData(form)),
        criminalClearanceDeclared: _criminalClearance,
        gdprConsentAccepted: _gdprConsent,
        eventInvitationOptIn: _eventOptIn,
      );
      if (mounted) Navigator.of(context).pop(true);
    } on DioException catch (e) {
      setState(() => _submitErrorKey = mapDioError(e).l10nKey);
    } catch (_) {
      setState(() => _submitErrorKey = 'errors.generic');
    } finally {
      if (mounted) setState(() => _isSubmitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(_form?.title ?? 'intake_form.title'.tr())),
      body: SafeArea(
        child: _isLoading
            ? AppLoading(message: 'common.loading'.tr())
            : _loadErrorKey != null
                ? AppErrorView(
                    message: _loadErrorKey!.tr(),
                    retryLabel: 'common.retry'.tr(),
                    onRetry: _load,
                  )
                : _buildForm(context, _form!),
      ),
    );
  }

  Widget _buildForm(BuildContext context, IntakeForm form) {
    final theme = Theme.of(context);
    return SingleChildScrollView(
      padding: const EdgeInsetsDirectional.all(AppSpacing.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (form.description != null && form.description!.isNotEmpty) ...[
            Text(form.description!, style: theme.textTheme.bodyLarge),
            const SizedBox(height: AppSpacing.lg),
          ],
          for (final section in form.sections) _buildSection(context, section),
          _buildDeclarationsSection(context),
          if (_validationErrors.isNotEmpty) ...[
            const SizedBox(height: AppSpacing.md),
            _ValidationSummary(errors: _validationErrors),
          ],
          if (_submitErrorKey != null) ...[
            const SizedBox(height: AppSpacing.md),
            Text(
              _submitErrorKey!.tr(),
              style: TextStyle(color: theme.colorScheme.error),
              textAlign: TextAlign.center,
            ),
          ],
          const SizedBox(height: AppSpacing.lg),
          AppButton(
            label: 'intake_form.submit'.tr(),
            onPressed: _isSubmitting ? null : _submit,
            isLoading: _isSubmitting,
          ),
        ],
      ),
    );
  }

  Widget _buildSection(BuildContext context, IntakeFormSection section) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsetsDirectional.only(bottom: AppSpacing.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(section.title, style: theme.textTheme.titleLarge),
          if (section.description != null && section.description!.isNotEmpty) ...[
            const SizedBox(height: AppSpacing.xs),
            Text(section.description!, style: theme.textTheme.bodyMedium),
          ],
          const SizedBox(height: AppSpacing.md),
          for (final field in section.fields) ...[
            _buildFieldInput(context, field),
            const SizedBox(height: AppSpacing.md),
          ],
        ],
      ),
    );
  }

  Widget _buildFieldInput(BuildContext context, IntakeFormField field) {
    return switch (field.fieldType) {
      IntakeFieldType.text => _TextInput(
          label: _fieldLabel(field),
          controller: _textControllers[field.fieldKey]!,
        ),
      IntakeFieldType.textarea => _TextInput(
          label: _fieldLabel(field),
          controller: _textControllers[field.fieldKey]!,
          maxLines: 4,
        ),
      IntakeFieldType.singleChoice => _SingleChoiceInput(
          label: _fieldLabel(field),
          options: field.options,
          selected: _answers[field.fieldKey] as String?,
          onChanged: (value) => setState(() => _answers[field.fieldKey] = value),
        ),
      IntakeFieldType.multiChoice || IntakeFieldType.timeSlots => _MultiChoiceInput(
          label: _fieldLabel(field),
          options: field.options,
          selected: _answers[field.fieldKey] as Set<String>,
          onChanged: (value) => setState(() => _answers[field.fieldKey] = value),
        ),
      IntakeFieldType.boolean => _BooleanInput(
          label: _fieldLabel(field),
          value: _answers[field.fieldKey] as bool? ?? false,
          onChanged: (value) => setState(() => _answers[field.fieldKey] = value),
        ),
      IntakeFieldType.date => _DateInput(
          label: _fieldLabel(field),
          value: _answers[field.fieldKey] as DateTime?,
          onChanged: (value) => setState(() => _answers[field.fieldKey] = value),
        ),
    };
  }

  String _fieldLabel(IntakeFormField field) =>
      field.labelKey.tr() + (field.isRequired ? ' *' : '');

  Widget _buildDeclarationsSection(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('intake_form.declarations_title'.tr(), style: theme.textTheme.titleLarge),
        const SizedBox(height: AppSpacing.sm),
        CheckboxListTile(
          contentPadding: EdgeInsets.zero,
          controlAffinity: ListTileControlAffinity.leading,
          value: _criminalClearance,
          onChanged: (value) => setState(() => _criminalClearance = value ?? false),
          title: Text('intake_form.criminal_clearance_label'.tr()),
        ),
        CheckboxListTile(
          contentPadding: EdgeInsets.zero,
          controlAffinity: ListTileControlAffinity.leading,
          value: _gdprConsent,
          onChanged: (value) => setState(() => _gdprConsent = value ?? false),
          title: Text('intake_form.gdpr_consent_label'.tr()),
        ),
        CheckboxListTile(
          contentPadding: EdgeInsets.zero,
          controlAffinity: ListTileControlAffinity.leading,
          value: _eventOptIn,
          onChanged: (value) => setState(() => _eventOptIn = value ?? false),
          title: Text('intake_form.event_invitation_label'.tr()),
        ),
      ],
    );
  }
}

class _ValidationSummary extends StatelessWidget {
  const _ValidationSummary({required this.errors});

  final List<String> errors;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Semantics(
      liveRegion: true,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'intake_form.validation_summary'.tr(),
            style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.error),
          ),
          for (final error in errors)
            Text('• $error', style: TextStyle(color: theme.colorScheme.error)),
        ],
      ),
    );
  }
}

class _TextInput extends StatelessWidget {
  const _TextInput({
    required this.label,
    required this.controller,
    this.maxLines = 1,
  });

  final String label;
  final TextEditingController controller;
  final int maxLines;

  @override
  Widget build(BuildContext context) {
    return TextField(
      controller: controller,
      maxLines: maxLines,
      decoration: InputDecoration(labelText: label),
    );
  }
}

class _SingleChoiceInput extends StatelessWidget {
  const _SingleChoiceInput({
    required this.label,
    required this.options,
    required this.selected,
    required this.onChanged,
  });

  final String label;
  final List<String> options;
  final String? selected;
  final ValueChanged<String?> onChanged;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: theme.textTheme.titleMedium),
        for (final option in options)
          RadioListTile<String>(
            contentPadding: EdgeInsets.zero,
            value: option,
            groupValue: selected,
            onChanged: onChanged,
            title: Text(option),
          ),
      ],
    );
  }
}

class _MultiChoiceInput extends StatelessWidget {
  const _MultiChoiceInput({
    required this.label,
    required this.options,
    required this.selected,
    required this.onChanged,
  });

  final String label;
  final List<String> options;
  final Set<String> selected;
  final ValueChanged<Set<String>> onChanged;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: theme.textTheme.titleMedium),
        for (final option in options)
          CheckboxListTile(
            contentPadding: EdgeInsets.zero,
            controlAffinity: ListTileControlAffinity.leading,
            value: selected.contains(option),
            onChanged: (checked) {
              final next = Set<String>.from(selected);
              if (checked ?? false) {
                next.add(option);
              } else {
                next.remove(option);
              }
              onChanged(next);
            },
            title: Text(option),
          ),
      ],
    );
  }
}

class _BooleanInput extends StatelessWidget {
  const _BooleanInput({
    required this.label,
    required this.value,
    required this.onChanged,
  });

  final String label;
  final bool value;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    return CheckboxListTile(
      contentPadding: EdgeInsets.zero,
      controlAffinity: ListTileControlAffinity.leading,
      value: value,
      onChanged: (checked) => onChanged(checked ?? false),
      title: Text(label),
    );
  }
}

class _DateInput extends StatelessWidget {
  const _DateInput({
    required this.label,
    required this.value,
    required this.onChanged,
  });

  final String label;
  final DateTime? value;
  final ValueChanged<DateTime?> onChanged;

  @override
  Widget build(BuildContext context) {
    return ListTile(
      contentPadding: EdgeInsets.zero,
      title: Text(label),
      subtitle: Text(value == null ? 'intake_form.date_not_set'.tr() : value!.toString().split(' ').first),
      trailing: const Icon(Icons.calendar_today),
      onTap: () async {
        final now = DateTime.now();
        final picked = await showDatePicker(
          context: context,
          initialDate: value ?? now,
          firstDate: now.subtract(const Duration(days: 365 * 2)),
          lastDate: now.add(const Duration(days: 365 * 2)),
        );
        if (picked != null) onChanged(picked);
      },
    );
  }
}
