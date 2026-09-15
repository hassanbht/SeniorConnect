// lib/features/help_requests/presentation/widgets/safeguarding_concern_dialog.dart
//
// P4-10: Safeguarding Concern Reporting Dialog (BR-SG-04).
// Allows reporting a concern in <= 2 taps from any activity/assignment screen.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/design_system/app_tokens.dart';
import '../../../../core/network/api_client.dart';
import '../../../../shared/widgets/app_button.dart';
import '../../application/safeguarding_concern_notifier.dart';

class SafeguardingConcernDialog extends ConsumerStatefulWidget {
  const SafeguardingConcernDialog({
    super.key,
    required this.subjectUserId,
    this.apiClient,
  });

  final String subjectUserId;
  final ApiClient? apiClient;

  static Future<void> show(
    BuildContext context, {
    required String subjectUserId,
    ApiClient? apiClient,
  }) {
    return showDialog<void>(
      context: context,
      builder: (ctx) => SafeguardingConcernDialog(
        subjectUserId: subjectUserId,
        apiClient: apiClient,
      ),
    );
  }

  @override
  ConsumerState<SafeguardingConcernDialog> createState() =>
      _SafeguardingConcernDialogState();
}

class _SafeguardingConcernDialogState
    extends ConsumerState<SafeguardingConcernDialog> {
  final _textController = TextEditingController();

  static const _categories = [
    'general_concern',
    'vulnerability_welfare',
    'safety_risk',
    'boundary_crossing',
  ];

  @override
  void dispose() {
    _textController.dispose();
    super.dispose();
  }

  Future<void> _submitConcern() async {
    final text = _textController.text.trim();
    if (text.isEmpty) return;

    final notifier =
        ref.read(safeguardingConcernProvider(widget.apiClient).notifier);
    await notifier.submitConcern(
      subjectUserId: widget.subjectUserId,
      details: text,
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final state = ref.watch(safeguardingConcernProvider(widget.apiClient));
    final notifier =
        ref.read(safeguardingConcernProvider(widget.apiClient).notifier);

    if (state.submitted) {
      return AlertDialog(
        title: Row(
          children: [
            Icon(Icons.check_circle, color: theme.colorScheme.primary),
            const SizedBox(width: AppSpacing.sm),
            Expanded(child: Text('safeguarding.report'.tr())),
          ],
        ),
        content: Text('safeguarding.thanks'.tr()),
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
              'safeguarding.report'.tr(),
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
                'safeguarding.prompt'.tr(),
                style: theme.textTheme.bodyMedium,
              ),
              const SizedBox(height: AppSpacing.md),
              DropdownButtonFormField<String>(
                value: state.selectedCategory,
                isExpanded: true,
                decoration: InputDecoration(
                  labelText: 'safeguarding.category'.tr(),
                  border: const OutlineInputBorder(),
                ),
                items: _categories.map((c) {
                  return DropdownMenuItem(
                    value: c,
                    child: Text(
                      'safeguarding.cat_$c'.tr(),
                      overflow: TextOverflow.ellipsis,
                    ),
                  );
                }).toList(),
                onChanged: (val) {
                  if (val != null) notifier.setCategory(val);
                },
              ),
              const SizedBox(height: AppSpacing.md),
              TextField(
                controller: _textController,
                maxLines: 3,
                decoration: InputDecoration(
                  hintText: 'safeguarding.hint'.tr(),
                  border: const OutlineInputBorder(),
                ),
              ),
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
          label: 'common.save'.tr(),
          variant: AppButtonVariant.primary,
          isLoading: state.isSubmitting,
          onPressed: _submitConcern,
        ),
      ],
    );
  }
}
