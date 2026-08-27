// lib/features/help_requests/presentation/widgets/first_meeting_protocol_dialog.dart
//
// P4-08: First Meeting Protocol Checklist (docs/architecture/trust-safety.md §6).
// Displayed to both parties before a first Level-3+ meeting.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';

import '../../../../core/design_system/app_tokens.dart';
import '../../../../shared/widgets/app_button.dart';

class FirstMeetingProtocolDialog extends StatelessWidget {
  const FirstMeetingProtocolDialog({super.key});

  static Future<void> show(BuildContext context) {
    return showDialog<void>(
      context: context,
      builder: (ctx) => const FirstMeetingProtocolDialog(),
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return AlertDialog(
      title: Row(
        children: [
          Icon(Icons.checklist, color: theme.colorScheme.primary),
          const SizedBox(width: AppSpacing.sm),
          Expanded(
            child: Text(
              'protocol.title'.tr(),
              style: theme.textTheme.titleLarge?.copyWith(fontWeight: FontWeight.bold),
            ),
          ),
        ],
      ),
      content: SingleChildScrollView(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 480),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              _SectionHeader(title: 'protocol.before_title'.tr(), theme: theme),
              _ChecklistRow(text: 'protocol.before.confirm_name_and_time'.tr()),
              _ChecklistRow(text: 'protocol.before.agree_meeting_place'.tr()),
              _ChecklistRow(text: 'protocol.before.notify_trusted_person'.tr()),
              const SizedBox(height: AppSpacing.md),

              _SectionHeader(title: 'protocol.during_title'.tr(), theme: theme),
              _ChecklistRow(text: 'protocol.during.check_in_app'.tr()),
              const SizedBox(height: AppSpacing.md),

              _SectionHeader(title: 'protocol.after_title'.tr(), theme: theme),
              _ChecklistRow(text: 'protocol.after.check_out_app'.tr()),
              _ChecklistRow(text: 'protocol.after.confirm_all_in_order_or_report'.tr()),
            ],
          ),
        ),
      ),
      actions: [
        AppButton(
          label: 'protocol.understood'.tr(),
          icon: Icons.check,
          onPressed: () => Navigator.of(context).pop(),
        ),
      ],
    );
  }
}

class _SectionHeader extends StatelessWidget {
  const _SectionHeader({required this.title, required this.theme});
  final String title;
  final ThemeData theme;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsetsDirectional.only(bottom: AppSpacing.xs),
      child: Text(
        title,
        style: theme.textTheme.titleSmall?.copyWith(
          fontWeight: FontWeight.bold,
          color: theme.colorScheme.primary,
        ),
      ),
    );
  }
}

class _ChecklistRow extends StatelessWidget {
  const _ChecklistRow({required this.text});
  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsetsDirectional.only(bottom: AppSpacing.xs),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Icon(Icons.check_circle_outline, size: 18),
          const SizedBox(width: AppSpacing.sm),
          Expanded(child: Text(text)),
        ],
      ),
    );
  }
}
