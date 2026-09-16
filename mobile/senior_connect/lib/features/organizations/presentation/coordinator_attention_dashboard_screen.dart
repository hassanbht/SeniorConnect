// lib/features/organizations/presentation/coordinator_attention_dashboard_screen.dart
//
// P2-26 / Coordinator Wedge: "Braucht heute Aufmerksamkeit" triage dashboard.
// Displays unconfirmed hours, expiring verifications, silent volunteers, and
// pending applicant forms at the very top for staff.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../core/router/app_router.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_states.dart';
import '../application/coordinator_attention_dashboard_notifier.dart';
import 'annual_report_screen.dart';

class CoordinatorAttentionDashboardScreen extends ConsumerWidget {
  const CoordinatorAttentionDashboardScreen({
    super.key,
    required this.organizationId,
    required this.apiClient,
  });

  final String organizationId;
  final ApiClient apiClient;

  Future<void> _sendMonthlyReminders(
    BuildContext context,
    WidgetRef ref,
    CoordinatorAttentionParams params,
  ) async {
    final dispatched = await ref
        .read(coordinatorAttentionProvider(params).notifier)
        .sendMonthlyReminders();
    if (!context.mounted) return;

    if (dispatched != null) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('coordinator.reminders_sent'.tr(args: ['$dispatched'])),
        ),
      );
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('errors.generic'.tr())),
      );
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final params = CoordinatorAttentionParams(
      organizationId: organizationId,
      apiClient: apiClient,
    );
    final state = ref.watch(coordinatorAttentionProvider(params));
    final notifier = ref.read(coordinatorAttentionProvider(params).notifier);

    return Scaffold(
      appBar: AppBar(
        title: Text('coordinator.attention_title'.tr()),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            tooltip: 'common.retry'.tr(),
            onPressed: () => notifier.loadDashboard(),
          ),
        ],
      ),
      body: SafeArea(
        child: state.isLoading
            ? AppLoading(message: 'common.loading'.tr())
            : state.errorMessage != null
                ? AppErrorView(
                    message: state.errorMessage!,
                    retryLabel: 'common.retry'.tr(),
                    onRetry: () => notifier.loadDashboard(),
                  )
                : RefreshIndicator(
                    onRefresh: () => notifier.loadDashboard(),
                    child: ListView(
                      padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                      children: [
                        _buildQuickActionCards(context, theme, state),
                        const SizedBox(height: AppSpacing.lg),
                        _buildExpiringSection(theme, state),
                        const SizedBox(height: AppSpacing.lg),
                        _buildSilentVolunteersSection(
                          context,
                          theme,
                          state,
                          ref,
                          params,
                        ),
                      ],
                    ),
                  ),
      ),
    );
  }

  Widget _buildQuickActionCards(
    BuildContext context,
    ThemeData theme,
    CoordinatorAttentionState state,
  ) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          'coordinator.overview_subtitle'.tr(),
          style: theme.textTheme.titleMedium?.copyWith(
            fontWeight: FontWeight.bold,
          ),
        ),
        const SizedBox(height: AppSpacing.sm),
        Row(
          children: [
            Expanded(
              child: _MetricCard(
                icon: Icons.pending_actions_outlined,
                color: theme.colorScheme.primary,
                count: state.unconfirmedCount,
                label: 'coordinator.unconfirmed_hours'.tr(),
                onTap: () => context.push(
                  '${AppRoutes.organizations}/$organizationId/coordinator/hours-queue',
                ),
              ),
            ),
            const SizedBox(width: AppSpacing.sm),
            Expanded(
              child: _MetricCard(
                icon: Icons.how_to_reg_outlined,
                color: theme.colorScheme.secondary,
                count: state.pendingAppsCount,
                label: 'coordinator.pending_applications'.tr(),
                onTap: () => context.push(
                  '${AppRoutes.organizations}/$organizationId/forms/volunteer/submissions',
                ),
              ),
            ),
          ],
        ),
        const SizedBox(height: AppSpacing.sm),
        Row(
          children: [
            Expanded(
              child: _MetricCard(
                icon: Icons.warning_amber_outlined,
                color: theme.colorScheme.error,
                count: state.disputedCount,
                label: 'coordinator.disputed_hours'.tr(),
                onTap: () => context.push(
                  '${AppRoutes.organizations}/$organizationId/coordinator/hours-queue',
                ),
              ),
            ),
            const SizedBox(width: AppSpacing.sm),
            Expanded(
              child: _MetricCard(
                icon: Icons.people_outline,
                color: theme.colorScheme.tertiary,
                count: state.silentVolunteers.length,
                label: 'coordinator.silent_volunteers'.tr(),
                onTap: () => context.push(
                  '${AppRoutes.organizations}/$organizationId/coordinator/roster',
                ),
              ),
            ),
          ],
        ),
        const SizedBox(height: AppSpacing.sm),
        Card(
          elevation: 0,
          color: theme.colorScheme.primaryContainer.withValues(alpha: 0.35),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(AppRadius.md),
            side: BorderSide(
              color: theme.colorScheme.primary.withValues(alpha: 0.3),
            ),
          ),
          child: ListTile(
            leading: Icon(Icons.bar_chart_rounded, color: theme.colorScheme.primary),
            title: Text(
              'annual_report.title'.tr(),
              style: const TextStyle(fontWeight: FontWeight.bold),
            ),
            subtitle: Text('annual_report.open_dashboard'.tr()),
            trailing: const Icon(Icons.arrow_forward_ios, size: 16),
            onTap: () {
              Navigator.of(context).push(
                MaterialPageRoute(
                  builder: (_) => AnnualReportScreen(
                    organizationId: organizationId,
                    apiClient: apiClient,
                  ),
                ),
              );
            },
          ),
        ),
      ],
    );
  }

  Widget _buildExpiringSection(
    ThemeData theme,
    CoordinatorAttentionState state,
  ) {
    return Card(
      child: Padding(
        padding: const EdgeInsetsDirectional.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Icon(Icons.timer_outlined, color: theme.colorScheme.error),
                const SizedBox(width: AppSpacing.sm),
                Expanded(
                  child: Text(
                    'coordinator.expiring_verifications'.tr(),
                    style: theme.textTheme.titleMedium?.copyWith(
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
                Text(
                  '${state.expiringVerifications.length}',
                  style: theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                    color: theme.colorScheme.error,
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.sm),
            if (state.expiringVerifications.isEmpty)
              Text(
                'coordinator.no_expiring_verifications'.tr(),
                style: theme.textTheme.bodyMedium?.copyWith(
                  color: theme.colorScheme.onSurfaceVariant,
                ),
              )
            else
              ...state.expiringVerifications.take(5).map((item) {
                final m = item as Map<String, dynamic>;
                final type = m['type'] as String? ?? '';
                final days = m['daysRemaining'] as int? ?? 0;
                return ListTile(
                  dense: true,
                  contentPadding: EdgeInsets.zero,
                  leading: const Icon(Icons.badge_outlined),
                  title: Text(type),
                  subtitle: Text(
                    'coordinator.days_remaining'.tr(args: ['$days']),
                  ),
                  trailing: days <= 7
                      ? Chip(
                          label: Text('coordinator.urgent'.tr()),
                          backgroundColor: theme.colorScheme.errorContainer,
                          labelStyle: TextStyle(
                            color: theme.colorScheme.onErrorContainer,
                          ),
                        )
                      : null,
                );
              }),
          ],
        ),
      ),
    );
  }

  Widget _buildSilentVolunteersSection(
    BuildContext context,
    ThemeData theme,
    CoordinatorAttentionState state,
    WidgetRef ref,
    CoordinatorAttentionParams params,
  ) {
    return Card(
      child: Padding(
        padding: const EdgeInsetsDirectional.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Icon(Icons.bedtime_outlined, color: theme.colorScheme.tertiary),
                const SizedBox(width: AppSpacing.sm),
                Expanded(
                  child: Text(
                    'coordinator.silent_volunteers'.tr(),
                    style: theme.textTheme.titleMedium?.copyWith(
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.sm),
            Text(
              'coordinator.silent_volunteers_desc'.tr(),
              style: theme.textTheme.bodySmall,
            ),
            const SizedBox(height: AppSpacing.md),
            AppButton(
              label: 'coordinator.send_monthly_reminder_action'.tr(),
              icon: Icons.send_outlined,
              variant: AppButtonVariant.tonal,
              isLoading: state.isSendingReminders,
              onPressed:
                  state.silentVolunteers.isEmpty || state.isSendingReminders
                      ? null
                      : () => _sendMonthlyReminders(context, ref, params),
            ),
          ],
        ),
      ),
    );
  }
}

class _MetricCard extends StatelessWidget {
  const _MetricCard({
    required this.icon,
    required this.color,
    required this.count,
    required this.label,
    required this.onTap,
  });

  final IconData icon;
  final Color color;
  final int count;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(AppRadius.md),
      child: Ink(
        padding: const EdgeInsetsDirectional.all(AppSpacing.md),
        decoration: BoxDecoration(
          color: theme.colorScheme.surfaceContainerHighest,
          borderRadius: BorderRadius.circular(AppRadius.md),
          border: Border.all(color: theme.colorScheme.outlineVariant),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Icon(icon, color: color),
                Text(
                  '$count',
                  style: theme.textTheme.headlineMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                    color: color,
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.xs),
            Text(
              label,
              style: theme.textTheme.bodyMedium?.copyWith(
                fontWeight: FontWeight.w600,
              ),
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
            ),
          ],
        ),
      ),
    );
  }
}
