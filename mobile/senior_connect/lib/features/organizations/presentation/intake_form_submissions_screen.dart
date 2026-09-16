// lib/features/organizations/presentation/intake_form_submissions_screen.dart
//
// P2-40: Coordinator-facing review queue for an organization's intake form
// submissions. Field keys in each submission's answers aren't human-readable
// on their own, so every submission is cross-referenced against the form's
// sections/fields (fetched once) to show each answer next to its LabelKey.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_states.dart';
import '../application/intake_form_submissions_notifier.dart';
import '../data/intake_form_repository.dart';

class IntakeFormSubmissionsScreen extends ConsumerWidget {
  const IntakeFormSubmissionsScreen({
    super.key,
    required this.organizationId,
    required this.formType,
    required this.apiClient,
  });

  final String organizationId;
  final IntakeFormType formType;
  final ApiClient apiClient;

  Future<void> _openDetail(
    BuildContext context,
    IntakeFormSubmission submission,
    Map<String, String> fieldLabels,
    IntakeFormSubmissionsParams params,
  ) async {
    await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (_) => _SubmissionDetailSheet(
        submission: submission,
        fieldLabels: fieldLabels,
        params: params,
      ),
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final params = IntakeFormSubmissionsParams(
      organizationId: organizationId,
      formType: formType,
      apiClient: apiClient,
    );
    final state = ref.watch(intakeFormSubmissionsProvider(params));
    final notifier = ref.read(intakeFormSubmissionsProvider(params).notifier);

    return Scaffold(
      appBar: AppBar(title: Text('intake_form.submissions_title'.tr())),
      body: SafeArea(
        child: state.isLoading
            ? AppLoading(message: 'common.loading'.tr())
            : state.errorKey != null
                ? AppErrorView(
                    message: state.errorKey!.tr(),
                    retryLabel: 'common.retry'.tr(),
                    onRetry: () => notifier.load(),
                  )
                : state.submissions.isEmpty
                    ? AppEmptyState(
                        icon: Icons.inbox_outlined,
                        message: 'intake_form.no_submissions'.tr(),
                      )
                    : RefreshIndicator(
                        onRefresh: () => notifier.load(),
                        child: ListView.builder(
                          padding:
                              const EdgeInsetsDirectional.all(AppSpacing.md),
                          itemCount: state.submissions.length,
                          itemBuilder: (context, index) => _SubmissionCard(
                            submission: state.submissions[index],
                            onTap: () => _openDetail(
                              context,
                              state.submissions[index],
                              state.fieldLabels,
                              params,
                            ),
                          ),
                        ),
                      ),
      ),
    );
  }
}

class _SubmissionCard extends StatelessWidget {
  const _SubmissionCard({required this.submission, required this.onTap});

  final IntakeFormSubmission submission;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      margin: const EdgeInsetsDirectional.only(bottom: AppSpacing.sm),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.md),
      ),
      child: ListTile(
        onTap: onTap,
        title: Row(
          children: [
            _StatusChip(status: submission.status),
            const SizedBox(width: AppSpacing.sm),
            if (submission.submittedAtUtc != null)
              Expanded(
                child: Text(
                  submission.submittedAtUtc!
                      .toLocal()
                      .toString()
                      .split('.')
                      .first,
                  style: theme.textTheme.bodyMedium,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
          ],
        ),
        subtitle: Padding(
          padding: const EdgeInsetsDirectional.only(top: AppSpacing.xs),
          child: Row(
            children: [
              Icon(
                submission.criminalClearanceDeclared
                    ? Icons.check_circle_outline
                    : Icons.cancel_outlined,
                size: 16,
                color: submission.criminalClearanceDeclared
                    ? Colors.green
                    : theme.colorScheme.error,
              ),
              const SizedBox(width: AppSpacing.xxs),
              Text('intake_form.criminal_clearance_short'.tr(),
                  style: theme.textTheme.bodySmall),
              const SizedBox(width: AppSpacing.md),
              Icon(
                submission.gdprConsentAccepted
                    ? Icons.check_circle_outline
                    : Icons.cancel_outlined,
                size: 16,
                color: submission.gdprConsentAccepted
                    ? Colors.green
                    : theme.colorScheme.error,
              ),
              const SizedBox(width: AppSpacing.xxs),
              Text('intake_form.gdpr_consent_short'.tr(),
                  style: theme.textTheme.bodySmall),
            ],
          ),
        ),
      ),
    );
  }
}

class _StatusChip extends StatelessWidget {
  const _StatusChip({required this.status});

  final IntakeSubmissionStatus status;

  @override
  Widget build(BuildContext context) {
    final key = switch (status) {
      IntakeSubmissionStatus.draft => 'intake_form.status.draft',
      IntakeSubmissionStatus.submitted => 'intake_form.status.submitted',
      IntakeSubmissionStatus.approved => 'intake_form.status.approved',
      IntakeSubmissionStatus.declined => 'intake_form.status.declined',
    };
    final color = switch (status) {
      IntakeSubmissionStatus.approved => Theme.of(context).colorScheme.primary,
      IntakeSubmissionStatus.declined => Theme.of(context).colorScheme.error,
      _ => Theme.of(context).colorScheme.secondary,
    };
    return Chip(
      label: Text(key.tr(), style: TextStyle(color: color)),
      visualDensity: VisualDensity.compact,
      materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
    );
  }
}

class _SubmissionDetailSheet extends ConsumerStatefulWidget {
  const _SubmissionDetailSheet({
    required this.submission,
    required this.fieldLabels,
    required this.params,
  });

  final IntakeFormSubmission submission;
  final Map<String, String> fieldLabels;
  final IntakeFormSubmissionsParams params;

  @override
  ConsumerState<_SubmissionDetailSheet> createState() =>
      _SubmissionDetailSheetState();
}

class _SubmissionDetailSheetState
    extends ConsumerState<_SubmissionDetailSheet> {
  final _reviewNotesController = TextEditingController();

  bool get _isDecided =>
      widget.submission.status == IntakeSubmissionStatus.approved ||
      widget.submission.status == IntakeSubmissionStatus.declined;

  @override
  void dispose() {
    _reviewNotesController.dispose();
    super.dispose();
  }

  Future<void> _decide(bool approve) async {
    final success = await ref
        .read(intakeFormSubmissionsProvider(widget.params).notifier)
        .decide(
          submissionId: widget.submission.id,
          approve: approve,
          reviewNotes: _reviewNotesController.text.trim().isEmpty
              ? null
              : _reviewNotesController.text.trim(),
        );
    if (success && mounted) {
      Navigator.of(context).pop(true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final state = ref.watch(intakeFormSubmissionsProvider(widget.params));
    final answers = widget.submission.submissionData;

    return SafeArea(
      child: Padding(
        padding: EdgeInsetsDirectional.only(
          start: AppSpacing.lg,
          end: AppSpacing.lg,
          top: AppSpacing.lg,
          bottom: AppSpacing.lg + MediaQuery.viewInsetsOf(context).bottom,
        ),
        child: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            mainAxisSize: MainAxisSize.min,
            children: [
              Text('intake_form.answers_title'.tr(),
                  style: theme.textTheme.titleLarge),
              const SizedBox(height: AppSpacing.md),
              for (final entry in answers.entries) ...[
                Text(
                  widget.fieldLabels[entry.key]?.tr() ?? entry.key,
                  style: theme.textTheme.titleSmall,
                ),
                Text(_formatAnswer(entry.value),
                    style: theme.textTheme.bodyMedium),
                const SizedBox(height: AppSpacing.sm),
              ],
              const Divider(),
              _boolRow(
                theme,
                'intake_form.criminal_clearance_label'.tr(),
                widget.submission.criminalClearanceDeclared,
              ),
              _boolRow(
                theme,
                'intake_form.gdpr_consent_label'.tr(),
                widget.submission.gdprConsentAccepted,
              ),
              _boolRow(
                theme,
                'intake_form.event_invitation_label'.tr(),
                widget.submission.eventInvitationOptIn,
              ),
              if (widget.submission.reviewNotes != null &&
                  widget.submission.reviewNotes!.isNotEmpty) ...[
                const SizedBox(height: AppSpacing.md),
                Text('intake_form.review_notes_label'.tr(),
                    style: theme.textTheme.titleSmall),
                Text(widget.submission.reviewNotes!,
                    style: theme.textTheme.bodyMedium),
              ],
              if (!_isDecided) ...[
                const SizedBox(height: AppSpacing.lg),
                TextField(
                  controller: _reviewNotesController,
                  maxLines: 3,
                  decoration: InputDecoration(
                    labelText: 'intake_form.review_notes_label'.tr(),
                  ),
                ),
                if (state.decisionErrorKey != null) ...[
                  const SizedBox(height: AppSpacing.sm),
                  Text(
                    state.decisionErrorKey!.tr(),
                    style: TextStyle(color: theme.colorScheme.error),
                  ),
                ],
                const SizedBox(height: AppSpacing.md),
                AppButton(
                  label: 'intake_form.approve'.tr(),
                  onPressed: state.isDeciding ? null : () => _decide(true),
                  isLoading: state.isDeciding,
                ),
                const SizedBox(height: AppSpacing.sm),
                AppButton(
                  label: 'intake_form.decline'.tr(),
                  variant: AppButtonVariant.destructive,
                  confirmationText: 'intake_form.decline_confirm'.tr(),
                  onPressed: state.isDeciding ? null : () => _decide(false),
                  isLoading: state.isDeciding,
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }

  Widget _boolRow(ThemeData theme, String label, bool value) => Padding(
        padding: const EdgeInsetsDirectional.only(bottom: AppSpacing.xs),
        child: Row(
          children: [
            Icon(
              value ? Icons.check_circle_outline : Icons.cancel_outlined,
              size: 18,
              color: value ? Colors.green : theme.colorScheme.error,
            ),
            const SizedBox(width: AppSpacing.sm),
            Expanded(child: Text(label, style: theme.textTheme.bodyMedium)),
          ],
        ),
      );

  String _formatAnswer(dynamic value) {
    if (value is List) return value.join(', ');
    if (value is bool) return value ? 'common.yes'.tr() : 'common.no'.tr();
    return value.toString();
  }
}
