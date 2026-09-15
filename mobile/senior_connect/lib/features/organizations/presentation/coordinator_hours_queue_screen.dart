// lib/features/organizations/presentation/coordinator_hours_queue_screen.dart
//
// P2-28: Hours confirmation queue screen for coordinators.
// Displays unconfirmed hours submitted by volunteers, allowing coordinators
// to confirm or dispute individual records in 1 tap or batch-confirm.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_states.dart';
import '../application/coordinator_hours_queue_notifier.dart';

class CoordinatorHoursQueueScreen extends ConsumerWidget {
  const CoordinatorHoursQueueScreen({
    super.key,
    required this.organizationId,
    required this.apiClient,
  });

  final String organizationId;
  final ApiClient apiClient;

  Future<void> _confirmActivity(
    BuildContext context,
    WidgetRef ref,
    CoordinatorHoursQueueParams params,
    String activityId,
  ) async {
    final error = await ref
        .read(coordinatorHoursQueueProvider(params).notifier)
        .confirmActivity(activityId);
    if (!context.mounted) return;

    if (error == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('coordinator.activity_confirmed'.tr())),
      );
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(error.tr())),
      );
    }
  }

  Future<void> _confirmAll(
    BuildContext context,
    WidgetRef ref,
    CoordinatorHoursQueueParams params,
  ) async {
    final successCount = await ref
        .read(coordinatorHoursQueueProvider(params).notifier)
        .confirmAll();
    if (!context.mounted) return;

    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(
          'coordinator.all_activities_confirmed'.tr(args: ['$successCount']),
        ),
      ),
    );
  }

  Future<void> _disputeActivity(
    BuildContext context,
    WidgetRef ref,
    CoordinatorHoursQueueParams params,
    String activityId,
  ) async {
    final reasonController = TextEditingController();

    final shouldDispute = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('coordinator.dispute_title'.tr()),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text('coordinator.dispute_prompt'.tr()),
            const SizedBox(height: AppSpacing.sm),
            TextField(
              controller: reasonController,
              decoration: InputDecoration(
                hintText: 'coordinator.dispute_reason_hint'.tr(),
                border: const OutlineInputBorder(),
              ),
              maxLines: 2,
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(false),
            child: Text('common.cancel'.tr()),
          ),
          FilledButton(
            onPressed: () => Navigator.of(ctx).pop(true),
            child: Text('coordinator.dispute'.tr()),
          ),
        ],
      ),
    );

    if (shouldDispute == true && context.mounted) {
      final error = await ref
          .read(coordinatorHoursQueueProvider(params).notifier)
          .disputeActivity(activityId, reasonController.text.trim());
      if (!context.mounted) return;

      if (error == null) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('coordinator.activity_disputed'.tr())),
        );
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(error.tr())),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final params = CoordinatorHoursQueueParams(
      organizationId: organizationId,
      apiClient: apiClient,
    );
    final state = ref.watch(coordinatorHoursQueueProvider(params));
    final notifier = ref.read(coordinatorHoursQueueProvider(params).notifier);

    return Scaffold(
      appBar: AppBar(
        title: Text('coordinator.hours_queue_title'.tr()),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            tooltip: 'common.retry'.tr(),
            onPressed: () => notifier.loadQueue(),
          ),
        ],
      ),
      bottomNavigationBar: state.unconfirmedActivities.isNotEmpty
          ? SafeArea(
              child: Padding(
                padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                child: AppButton(
                  label: 'coordinator.confirm_all_button'.tr(
                    args: ['${state.unconfirmedActivities.length}'],
                  ),
                  icon: Icons.done_all,
                  isLoading: state.isBatchProcessing,
                  onPressed: state.isBatchProcessing
                      ? null
                      : () => _confirmAll(context, ref, params),
                ),
              ),
            )
          : null,
      body: SafeArea(
        child: state.isLoading
            ? AppLoading(message: 'common.loading'.tr())
            : state.errorMessage != null
                ? AppErrorView(
                    message: state.errorMessage!,
                    retryLabel: 'common.retry'.tr(),
                    onRetry: () => notifier.loadQueue(),
                  )
                : state.unconfirmedActivities.isEmpty
                    ? AppEmptyState(
                        icon: Icons.done_all,
                        message: 'coordinator.no_unconfirmed_hours'.tr(),
                      )
                    : ListView.separated(
                        padding:
                            const EdgeInsetsDirectional.all(AppSpacing.md),
                        itemCount: state.unconfirmedActivities.length,
                        separatorBuilder: (_, _) =>
                            const SizedBox(height: AppSpacing.sm),
                        itemBuilder: (context, index) {
                          final act = state.unconfirmedActivities[index]
                              as Map<String, dynamic>;
                          final id = act['id'] as String? ?? '';
                          final durationMinutes =
                              act['durationMinutes'] as int? ?? 0;
                          final date = act['occurredOn'] as String? ?? '–';
                          final notes = act['notes'] as String?;
                          final status =
                              act['status'] as String? ?? 'Logged';

                          return Card(
                            child: Padding(
                              padding: const EdgeInsetsDirectional.all(
                                  AppSpacing.md),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Row(
                                    mainAxisAlignment:
                                        MainAxisAlignment.spaceBetween,
                                    children: [
                                      Text(
                                        '${(durationMinutes / 60.0).toStringAsFixed(1)} Stunden ($durationMinutes min)',
                                        style: theme.textTheme.titleMedium
                                            ?.copyWith(
                                          fontWeight: FontWeight.bold,
                                        ),
                                      ),
                                      Text(date,
                                          style: theme.textTheme.bodySmall),
                                    ],
                                  ),
                                  const SizedBox(height: AppSpacing.xs),
                                  if (notes != null &&
                                      notes.isNotEmpty) ...[
                                    Text(
                                      notes,
                                      style: theme.textTheme.bodyMedium,
                                    ),
                                    const SizedBox(height: AppSpacing.xs),
                                  ],
                                  Text(
                                    'Status: $status',
                                    style: theme.textTheme.bodySmall?.copyWith(
                                      color: status == 'Disputed'
                                          ? theme.colorScheme.error
                                          : theme.colorScheme.primary,
                                      fontWeight: FontWeight.w600,
                                    ),
                                  ),
                                  const SizedBox(height: AppSpacing.md),
                                  Row(
                                    mainAxisAlignment: MainAxisAlignment.end,
                                    children: [
                                      OutlinedButton.icon(
                                        onPressed: () => _disputeActivity(
                                          context,
                                          ref,
                                          params,
                                          id,
                                        ),
                                        icon: const Icon(Icons.close),
                                        label:
                                            Text('coordinator.dispute'.tr()),
                                        style: OutlinedButton.styleFrom(
                                          foregroundColor:
                                              theme.colorScheme.error,
                                        ),
                                      ),
                                      const SizedBox(width: AppSpacing.sm),
                                      FilledButton.icon(
                                        onPressed: () => _confirmActivity(
                                          context,
                                          ref,
                                          params,
                                          id,
                                        ),
                                        icon: const Icon(Icons.check),
                                        label:
                                            Text('coordinator.confirm'.tr()),
                                      ),
                                    ],
                                  ),
                                ],
                              ),
                            ),
                          );
                        },
                      ),
      ),
    );
  }
}
