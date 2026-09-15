// lib/features/help_requests/presentation/active_assignment_screen.dart
//
// P3-23: Active assignment and Check-in / Check-out screens.
//
// Constraints and Rules:
// - Contact details (Name, Address, Phone) revealed only after assignment (BR-COMM-04).
// - Time-bounded Check-in / Check-out without ANY background location tracking (BR-HELP-06).
// - Direct 2-tap safeguarding concern reporting access (BR-SG-04, P4-10).
// - Accessible large touch targets in Senior Mode.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../core/router/app_router.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_states.dart';
import '../application/active_assignment_notifier.dart';
import 'widgets/safeguarding_concern_dialog.dart';

class ActiveAssignmentScreen extends ConsumerWidget {
  const ActiveAssignmentScreen({
    super.key,
    required this.apiClient,
    this.assignmentId,
  });

  final ApiClient apiClient;
  final String? assignmentId;

  Future<void> _makeCall(String phoneNumber) async {
    final uri = Uri(scheme: 'tel', path: phoneNumber);
    if (await canLaunchUrl(uri)) {
      await launchUrl(uri);
    }
  }

  Future<void> _handleCheckIn(
    BuildContext context,
    WidgetRef ref,
    ActiveAssignmentParams params,
  ) async {
    final success =
        await ref.read(activeAssignmentProvider(params).notifier).handleCheckIn();
    if (success && context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('${'help.checkin'.tr()} ✓'),
          backgroundColor: Theme.of(context).colorScheme.primary,
        ),
      );
    }
  }

  Future<void> _handleComplete(
    BuildContext context,
    WidgetRef ref,
    ActiveAssignmentParams params,
  ) async {
    await ref.read(activeAssignmentProvider(params).notifier).handleComplete();
  }

  void _reportSafeguardingConcern(BuildContext context) {
    SafeguardingConcernDialog.show(
      context,
      subjectUserId: assignmentId ?? 'unknown',
      apiClient: apiClient,
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final params = ActiveAssignmentParams(
      apiClient: apiClient,
      assignmentId: assignmentId,
    );
    final state = ref.watch(activeAssignmentProvider(params));

    if (state.isLoading) {
      return Scaffold(
        body: SafeArea(child: AppLoading(message: 'common.loading'.tr())),
      );
    }

    if (state.loadError != null || state.details == null) {
      return Scaffold(
        body: SafeArea(
          child: AppErrorView(
            message: state.loadError ?? 'errors.generic'.tr(),
            retryLabel: 'common.retry'.tr(),
            onRetry: () => ref
                .read(activeAssignmentProvider(params).notifier)
                .loadAssignment(),
          ),
        ),
      );
    }

    final details = state.details!;

    if (state.isCompleted) {
      return Scaffold(
        body: SafeArea(
          child: Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 560),
              child: Padding(
                padding: const EdgeInsetsDirectional.all(AppSpacing.xl),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Icon(Icons.task_alt, size: 80, color: theme.colorScheme.primary),
                    const SizedBox(height: AppSpacing.lg),
                    Text(
                      'help.status.completed'.tr(),
                      style: theme.textTheme.headlineMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                      textAlign: TextAlign.center,
                    ),
                    const SizedBox(height: AppSpacing.md),
                    Text(
                      'Vielen Dank für Ihren Einsatz! Die Stunden wurden für den Monatsbericht erfasst.',
                      style: theme.textTheme.bodyLarge?.copyWith(
                        color: theme.colorScheme.onSurfaceVariant,
                      ),
                      textAlign: TextAlign.center,
                    ),
                    const SizedBox(height: AppSpacing.xxl),
                    AppButton(
                      label: 'common.close'.tr(),
                      onPressed: () => context.go(AppRoutes.home),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      );
    }

    return Scaffold(
      appBar: AppBar(
        title: Text('help.status.assigned'.tr()),
        actions: [
          IconButton(
            icon: const Icon(Icons.security),
            tooltip: 'safeguarding.report'.tr(),
            onPressed: () => _reportSafeguardingConcern(context),
          ),
        ],
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
                  // Status Banner
                  Container(
                    padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                    decoration: BoxDecoration(
                      color: state.isCheckedIn
                          ? theme.colorScheme.tertiaryContainer
                          : theme.colorScheme.primaryContainer,
                      borderRadius: AppRadius.card,
                    ),
                    child: Row(
                      children: [
                        Icon(
                          state.isCheckedIn ? Icons.timelapse : Icons.calendar_today,
                          color: state.isCheckedIn
                              ? theme.colorScheme.onTertiaryContainer
                              : theme.colorScheme.onPrimaryContainer,
                        ),
                        const SizedBox(width: AppSpacing.md),
                        Expanded(
                          child: Text(
                            state.isCheckedIn
                                ? 'help.status.in_progress'.tr()
                                : 'help.status.assigned'.tr(),
                            style: theme.textTheme.titleMedium?.copyWith(
                              fontWeight: FontWeight.bold,
                              color: state.isCheckedIn
                                  ? theme.colorScheme.onTertiaryContainer
                                  : theme.colorScheme.onPrimaryContainer,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: AppSpacing.lg),

                  // Senior Contact Details Card (Revealed only after assignment)
                  Card(
                    elevation: 0,
                    shape: RoundedRectangleBorder(
                      borderRadius: AppRadius.card,
                      side: BorderSide(color: theme.colorScheme.outlineVariant),
                    ),
                    child: Padding(
                      padding: const EdgeInsetsDirectional.all(AppSpacing.lg),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              CircleAvatar(
                                radius: 24,
                                backgroundColor: theme.colorScheme.primaryContainer,
                                child: Text(
                                  (details.seniorDisplayName?.isNotEmpty ?? false)
                                      ? details.seniorDisplayName![0]
                                      : 'S',
                                  style: const TextStyle(
                                    fontSize: 20,
                                    fontWeight: FontWeight.bold,
                                  ),
                                ),
                              ),
                              const SizedBox(width: AppSpacing.md),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      details.seniorDisplayName ?? 'help.status.assigned'.tr(),
                                      style: theme.textTheme.titleLarge?.copyWith(
                                        fontWeight: FontWeight.bold,
                                      ),
                                    ),
                                    Text(
                                      details.categoryNameKey.tr(),
                                      style: theme.textTheme.bodyMedium?.copyWith(
                                        color: theme.colorScheme.onSurfaceVariant,
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            ],
                          ),
                          const Divider(height: AppSpacing.xl),

                          // Phone Call Action — only shown once the backend
                          // actually revealed a number (BR-COMM-04).
                          if (details.seniorPhone != null) ...[
                            ListTile(
                              contentPadding: EdgeInsets.zero,
                              leading: Icon(Icons.phone, color: theme.colorScheme.primary),
                              title: Text(details.seniorPhone!),
                              trailing: IconButton(
                                icon: const Icon(Icons.call),
                                onPressed: () => _makeCall(details.seniorPhone!),
                              ),
                            ),
                            const SizedBox(height: AppSpacing.xs),
                          ],

                          // Address
                          if (details.address != null)
                            ListTile(
                              contentPadding: EdgeInsets.zero,
                              leading: Icon(Icons.place, color: theme.colorScheme.primary),
                              title: Text(details.address!),
                            ),

                          if ((details.notes ?? '').isNotEmpty) ...[
                            const SizedBox(height: AppSpacing.sm),
                            Container(
                              padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                              decoration: BoxDecoration(
                                color: theme.colorScheme.surfaceContainerHighest,
                                borderRadius: AppRadius.card,
                              ),
                              child: Text(
                                details.notes!,
                                style: theme.textTheme.bodyMedium,
                              ),
                            ),
                          ],
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: AppSpacing.xl),

                  if (state.actionError != null) ...[
                    Text(
                      state.actionError!,
                      style: TextStyle(color: theme.colorScheme.error),
                    ),
                    const SizedBox(height: AppSpacing.md),
                  ],

                  // Check-in or Complete Action Button
                  if (!state.isCheckedIn)
                    AppButton(
                      label: '${'help.checkin'.tr()} (Angekommen)',
                      icon: Icons.login,
                      isLoading: state.isActionInProgress,
                      onPressed: () => _handleCheckIn(context, ref, params),
                    )
                  else
                    AppButton(
                      label: '${'help.checkout'.tr()} (Abschließen)',
                      icon: Icons.check_circle,
                      isLoading: state.isActionInProgress,
                      variant: AppButtonVariant.primary,
                      onPressed: () => _handleComplete(context, ref, params),
                    ),

                  const SizedBox(height: AppSpacing.lg),

                  // Report Concern (2-tap safety, BR-SG-04)
                  TextButton.icon(
                    icon: Icon(Icons.flag_outlined, color: theme.colorScheme.error),
                    label: Text(
                      'safeguarding.report'.tr(),
                      style: TextStyle(color: theme.colorScheme.error),
                    ),
                    onPressed: () => _reportSafeguardingConcern(context),
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
