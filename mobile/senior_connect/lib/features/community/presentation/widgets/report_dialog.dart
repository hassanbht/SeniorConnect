// lib/features/community/presentation/widgets/report_dialog.dart
//
// Reusable report dialog — reporting a group or a post both funnel through
// POST /api/v1/community/reports (CreateCommunityReportRequest).

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/design_system/app_tokens.dart';
import '../../../../core/network/api_client.dart';
import '../../../../shared/widgets/app_button.dart';
import '../../application/report_notifier.dart';
import '../../domain/group_enums.dart';

class ReportDialog extends ConsumerStatefulWidget {
  const ReportDialog({
    super.key,
    required this.targetType,
    required this.targetId,
    required this.apiClient,
    this.groupId,
  });

  final CommunityReportTargetType targetType;
  final String targetId;
  final ApiClient apiClient;

  /// The group this target belongs to, when applicable — required by the
  /// backend to route the report to that group's moderators.
  final String? groupId;

  static Future<void> show(
    BuildContext context, {
    required CommunityReportTargetType targetType,
    required String targetId,
    required ApiClient apiClient,
    String? groupId,
  }) {
    return showDialog<void>(
      context: context,
      builder: (_) => ReportDialog(
        targetType: targetType,
        targetId: targetId,
        apiClient: apiClient,
        groupId: groupId,
      ),
    );
  }

  @override
  ConsumerState<ReportDialog> createState() => _ReportDialogState();
}

class _ReportDialogState extends ConsumerState<ReportDialog> {
  final _descriptionController = TextEditingController();

  @override
  void dispose() {
    _descriptionController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final description = _descriptionController.text.trim();
    if (description.isEmpty) return;

    await ref
        .read(reportProvider(widget.apiClient).notifier)
        .submit(
          targetType: widget.targetType,
          targetId: widget.targetId,
          description: description,
          groupId: widget.groupId,
        );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final state = ref.watch(reportProvider(widget.apiClient));
    final notifier = ref.read(reportProvider(widget.apiClient).notifier);

    if (state.submitted) {
      return AlertDialog(
        title: Row(
          children: [
            Icon(Icons.check_circle, color: theme.colorScheme.primary),
            const SizedBox(width: AppSpacing.sm),
            Expanded(child: Text('community.report.title'.tr())),
          ],
        ),
        content: Text('community.report.thanks'.tr()),
        actions: [
          AppButton(
            label: 'common.close'.tr(),
            onPressed: () => Navigator.of(context).pop(),
          ),
        ],
      );
    }

    return AlertDialog(
      title: Row(
        children: [
          Icon(Icons.flag_outlined, color: theme.colorScheme.error),
          const SizedBox(width: AppSpacing.sm),
          Expanded(
            child: Text(
              'community.report.title'.tr(),
              style: theme.textTheme.titleLarge?.copyWith(
                fontWeight: FontWeight.bold,
              ),
            ),
          ),
        ],
      ),
      content: SingleChildScrollView(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 480),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                'community.report.prompt'.tr(),
                style: theme.textTheme.bodyMedium,
              ),
              const SizedBox(height: AppSpacing.md),
              DropdownButtonFormField<CommunityReportCategory>(
                initialValue: state.category,
                isExpanded: true,
                decoration: InputDecoration(
                  labelText: 'community.report.category'.tr(),
                  border: const OutlineInputBorder(),
                ),
                items: CommunityReportCategory.values.map((c) {
                  return DropdownMenuItem(
                    value: c,
                    child: Text(
                      'community.report.cat_${c.name}'.tr(),
                      overflow: TextOverflow.ellipsis,
                    ),
                  );
                }).toList(),
                onChanged: (value) {
                  if (value != null) notifier.setCategory(value);
                },
              ),
              const SizedBox(height: AppSpacing.md),
              TextField(
                controller: _descriptionController,
                maxLines: 3,
                decoration: InputDecoration(
                  hintText: 'community.report.description_hint'.tr(),
                  border: const OutlineInputBorder(),
                ),
              ),
              if (state.error != null) ...[
                const SizedBox(height: AppSpacing.sm),
                Text(
                  state.error!.tr(),
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: theme.colorScheme.error,
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: Text('common.cancel'.tr()),
        ),
        AppButton(
          label: 'community.report.submit'.tr(),
          variant: AppButtonVariant.primary,
          isLoading: state.isSubmitting,
          onPressed: _submit,
        ),
      ],
    );
  }
}
