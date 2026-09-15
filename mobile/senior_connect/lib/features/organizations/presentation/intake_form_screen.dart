// lib/features/organizations/presentation/intake_form_screen.dart
//
// P2-40: Applicant-facing dynamic renderer for an organization's intake
// form. Renders each field by FieldType, then always appends the two
// mandatory declarations (criminal clearance, GDPR consent) plus the
// optional event-invitation opt-in, regardless of what the dynamic fields
// contain — those three map directly to SubmitIntakeFormRequest.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_states.dart';
import '../application/intake_form_notifier.dart';
import '../data/intake_form_repository.dart';

class IntakeFormScreen extends ConsumerStatefulWidget {
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
  ConsumerState<IntakeFormScreen> createState() => _IntakeFormScreenState();
}

class _IntakeFormScreenState extends ConsumerState<IntakeFormScreen> {
  final Map<String, TextEditingController> _textControllers = {};

  IntakeFormParams get _params => IntakeFormParams(
        organizationId: widget.organizationId,
        formType: widget.formType,
        apiClient: widget.apiClient,
      );

  @override
  void dispose() {
    for (final controller in _textControllers.values) {
      controller.dispose();
    }
    super.dispose();
  }

  TextEditingController _getOrCreateController(String key) {
    return _textControllers.putIfAbsent(key, () => TextEditingController());
  }

  Future<void> _submit() async {
    final textValues = <String, String>{
      for (final entry in _textControllers.entries) entry.key: entry.value.text,
    };
    final success = await ref
        .read(intakeFormProvider(_params).notifier)
        .submit(textValues);

    if (success && mounted) {
      Navigator.of(context).pop(true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(intakeFormProvider(_params));
    final notifier = ref.read(intakeFormProvider(_params).notifier);

    return Scaffold(
      appBar: AppBar(title: Text(state.form?.title ?? 'intake_form.title'.tr())),
      body: SafeArea(
        child: state.isLoading
            ? AppLoading(message: 'common.loading'.tr())
            : state.loadErrorKey != null
                ? AppErrorView(
                    message: state.loadErrorKey!.tr(),
                    retryLabel: 'common.retry'.tr(),
                    onRetry: () => notifier.load(),
                  )
                : _buildForm(context, state, notifier),
      ),
    );
  }

  Widget _buildForm(
    BuildContext context,
    IntakeFormScreenState state,
    IntakeFormNotifier notifier,
  ) {
    final theme = Theme.of(context);
    final form = state.form!;

    return SingleChildScrollView(
      padding: const EdgeInsetsDirectional.all(AppSpacing.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (form.description != null && form.description!.isNotEmpty) ...[
            Text(form.description!, style: theme.textTheme.bodyLarge),
            const SizedBox(height: AppSpacing.lg),
          ],
          for (final section in form.sections)
            _buildSection(context, section, state, notifier),
          _buildDeclarationsSection(context, state, notifier),
          if (state.validationErrors.isNotEmpty) ...[
            const SizedBox(height: AppSpacing.md),
            _ValidationSummary(errors: state.validationErrors),
          ],
          if (state.submitErrorKey != null) ...[
            const SizedBox(height: AppSpacing.md),
            Text(
              state.submitErrorKey!.tr(),
              style: TextStyle(color: theme.colorScheme.error),
              textAlign: TextAlign.center,
            ),
          ],
          const SizedBox(height: AppSpacing.lg),
          AppButton(
            label: 'intake_form.submit'.tr(),
            onPressed: state.isSubmitting ? null : _submit,
            isLoading: state.isSubmitting,
          ),
        ],
      ),
    );
  }

  Widget _buildSection(
    BuildContext context,
    IntakeFormSection section,
    IntakeFormScreenState state,
    IntakeFormNotifier notifier,
  ) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsetsDirectional.only(bottom: AppSpacing.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(section.title, style: theme.textTheme.titleLarge),
          if (section.description != null &&
              section.description!.isNotEmpty) ...[
            const SizedBox(height: AppSpacing.xs),
            Text(section.description!, style: theme.textTheme.bodyMedium),
          ],
          const SizedBox(height: AppSpacing.md),
          for (final field in section.fields) ...[
            _buildFieldInput(context, field, state, notifier),
            const SizedBox(height: AppSpacing.md),
          ],
        ],
      ),
    );
  }

  Widget _buildFieldInput(
    BuildContext context,
    IntakeFormField field,
    IntakeFormScreenState state,
    IntakeFormNotifier notifier,
  ) {
    return switch (field.fieldType) {
      IntakeFieldType.text => _TextInput(
          label: _fieldLabel(field),
          controller: _getOrCreateController(field.fieldKey),
        ),
      IntakeFieldType.textarea => _TextInput(
          label: _fieldLabel(field),
          controller: _getOrCreateController(field.fieldKey),
          maxLines: 4,
        ),
      IntakeFieldType.singleChoice => _SingleChoiceInput(
          label: _fieldLabel(field),
          options: field.options,
          selected: state.answers[field.fieldKey] as String?,
          onChanged: (value) => notifier.setAnswer(field.fieldKey, value),
        ),
      IntakeFieldType.multiChoice ||
      IntakeFieldType.timeSlots =>
        _MultiChoiceInput(
          label: _fieldLabel(field),
          options: field.options,
          selected: (state.answers[field.fieldKey] as Set<String>?) ??
              <String>{},
          onChanged: (value) => notifier.setAnswer(field.fieldKey, value),
        ),
      IntakeFieldType.boolean => _BooleanInput(
          label: _fieldLabel(field),
          value: state.answers[field.fieldKey] as bool? ?? false,
          onChanged: (value) => notifier.setAnswer(field.fieldKey, value),
        ),
      IntakeFieldType.date => _DateInput(
          label: _fieldLabel(field),
          value: state.answers[field.fieldKey] as DateTime?,
          onChanged: (value) => notifier.setAnswer(field.fieldKey, value),
        ),
    };
  }

  String _fieldLabel(IntakeFormField field) =>
      field.labelKey.tr() + (field.isRequired ? ' *' : '');

  Widget _buildDeclarationsSection(
    BuildContext context,
    IntakeFormScreenState state,
    IntakeFormNotifier notifier,
  ) {
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('intake_form.declarations_title'.tr(),
            style: theme.textTheme.titleLarge),
        const SizedBox(height: AppSpacing.sm),
        CheckboxListTile(
          contentPadding: EdgeInsets.zero,
          controlAffinity: ListTileControlAffinity.leading,
          value: state.criminalClearance,
          onChanged: (value) =>
              notifier.setCriminalClearance(value ?? false),
          title: Text('intake_form.criminal_clearance_label'.tr()),
        ),
        CheckboxListTile(
          contentPadding: EdgeInsets.zero,
          controlAffinity: ListTileControlAffinity.leading,
          value: state.gdprConsent,
          onChanged: (value) => notifier.setGdprConsent(value ?? false),
          title: Text('intake_form.gdpr_consent_label'.tr()),
        ),
        CheckboxListTile(
          contentPadding: EdgeInsets.zero,
          controlAffinity: ListTileControlAffinity.leading,
          value: state.eventOptIn,
          onChanged: (value) => notifier.setEventOptIn(value ?? false),
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
            style: theme.textTheme.bodyMedium?.copyWith(
              color: theme.colorScheme.error,
            ),
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
      subtitle: Text(value == null
          ? 'intake_form.date_not_set'.tr()
          : value!.toString().split(' ').first),
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
