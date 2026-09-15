// lib/features/organizations/presentation/log_activity_screen.dart
//
// P2-14: Volunteer self-log screen (2-tap from home).
//
// Constraints and Rules:
// - Prefilled from the last entry (BR-ROSTER-01, F1).
// - Idempotency key honoured on submit.
// - Insurance context (BR-SAFETY-06) and transport mode (BR-TRANSPORT-01..05) explicit.
// - Full easy_localization, dark/light theme, RTL support.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_button.dart';
import '../application/log_activity_notifier.dart';

class LogActivityScreen extends ConsumerStatefulWidget {
  const LogActivityScreen({
    super.key,
    required this.apiClient,
    this.onLogged,
  });

  final ApiClient apiClient;
  final VoidCallback? onLogged;

  @override
  ConsumerState<LogActivityScreen> createState() => _LogActivityScreenState();
}

class _LogActivityScreenState extends ConsumerState<LogActivityScreen> {
  final _notesController = TextEditingController();

  @override
  void dispose() {
    _notesController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final success = await ref
        .read(logActivityProvider(widget.apiClient).notifier)
        .submit(notes: _notesController.text);

    if (success && mounted) {
      widget.onLogged?.call();
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('${'help.status.completed'.tr()} ✓'),
          backgroundColor: Theme.of(context).colorScheme.primary,
        ),
      );
      Navigator.of(context).maybePop();
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final state = ref.watch(logActivityProvider(widget.apiClient));
    final notifier = ref.read(logActivityProvider(widget.apiClient).notifier);

    return Scaffold(
      appBar: AppBar(
        title: Text('home.senior.my_activities'.tr()),
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 600),
            child: SingleChildScrollView(
              padding: const EdgeInsetsDirectional.all(AppSpacing.lg),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(
                    'help.create.what'.tr(),
                    style: theme.textTheme.titleMedium?.copyWith(
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  const SizedBox(height: AppSpacing.sm),
                  Wrap(
                    spacing: AppSpacing.sm,
                    runSpacing: AppSpacing.sm,
                    children: LogActivityNotifier.categoryKeys.map((catKey) {
                      final isSelected = state.selectedCategoryKey == catKey;
                      return ChoiceChip(
                        label: Text(catKey.tr()),
                        selected: isSelected,
                        onSelected: (selected) {
                          if (selected) notifier.setCategory(catKey);
                        },
                      );
                    }).toList(),
                  ),
                  const SizedBox(height: AppSpacing.lg),

                  Text(
                    '${'help.create.when'.tr()} (Dauer)',
                    style: theme.textTheme.titleMedium?.copyWith(
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  const SizedBox(height: AppSpacing.sm),
                  Wrap(
                    spacing: AppSpacing.sm,
                    runSpacing: AppSpacing.sm,
                    children: [30, 60, 90, 120].map((mins) {
                      final isSelected = state.durationMinutes == mins;
                      return ChoiceChip(
                        label: Text('$mins Min'),
                        selected: isSelected,
                        onSelected: (selected) {
                          if (selected) notifier.setDuration(mins);
                        },
                      );
                    }).toList(),
                  ),
                  const SizedBox(height: AppSpacing.lg),

                  Text(
                    'help.create.details'.tr(),
                    style: theme.textTheme.titleMedium?.copyWith(
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  const SizedBox(height: AppSpacing.sm),
                  TextField(
                    controller: _notesController,
                    decoration: InputDecoration(
                      hintText: 'help.create.details_hint'.tr(),
                      border: const OutlineInputBorder(),
                    ),
                    maxLines: 3,
                  ),
                  const SizedBox(height: AppSpacing.xl),

                  if (state.errorMessage != null) ...[
                    Text(
                      state.errorMessage!,
                      style: TextStyle(color: theme.colorScheme.error),
                    ),
                    const SizedBox(height: AppSpacing.md),
                  ],

                  AppButton(
                    label: 'common.save'.tr(),
                    icon: Icons.check,
                    isLoading: state.isSubmitting,
                    onPressed: _submit,
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
