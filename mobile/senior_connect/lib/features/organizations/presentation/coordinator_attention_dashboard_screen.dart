// lib/features/organizations/presentation/coordinator_attention_dashboard_screen.dart
//
// P2-26 / Coordinator Wedge: "Braucht heute Aufmerksamkeit" triage dashboard.
// Displays unconfirmed hours, expiring verifications, silent volunteers, and
// pending applicant forms at the very top for staff.

import 'package:dio/dio.dart';
import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../core/router/app_router.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_states.dart';

class CoordinatorAttentionDashboardScreen extends StatefulWidget {
  const CoordinatorAttentionDashboardScreen({
    super.key,
    required this.organizationId,
    required this.apiClient,
  });

  final String organizationId;
  final ApiClient apiClient;

  @override
  State<CoordinatorAttentionDashboardScreen> createState() =>
      _CoordinatorAttentionDashboardScreenState();
}

class _CoordinatorAttentionDashboardScreenState
    extends State<CoordinatorAttentionDashboardScreen> {
  bool _isLoading = true;
  bool _isSendingReminders = false;
  String? _errorMessage;

  int _unconfirmedCount = 0;
  int _disputedCount = 0;
  int _pendingAppsCount = 0;
  List<dynamic> _expiringVerifications = [];
  List<dynamic> _silentVolunteers = [];

  @override
  void initState() {
    super.initState();
    _loadDashboard();
  }

  Future<void> _loadDashboard() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final triageResp = await widget.apiClient.get<dynamic>(
        '/api/v1/coordinator/triage?organizationId=${widget.organizationId}',
      );

      if (triageResp is Map<String, dynamic>) {
        _unconfirmedCount = triageResp['unconfirmedActivitiesCount'] as int? ?? 0;
        _disputedCount = triageResp['disputedActivitiesCount'] as int? ?? 0;
        _pendingAppsCount = triageResp['pendingApplicationsCount'] as int? ?? 0;
      }

      // Fetch expiring verifications
      try {
        final expResp = await widget.apiClient.get<dynamic>(
          '/api/v1/coordinator/attention/expiring-verifications',
        );
        if (expResp is List) {
          _expiringVerifications = expResp;
        }
      } catch (_) {}

      // Fetch silent volunteers
      try {
        final silentResp = await widget.apiClient.get<dynamic>(
          '/api/v1/coordinator/attention/silent-volunteers?organizationId=${widget.organizationId}',
        );
        if (silentResp is List) {
          _silentVolunteers = silentResp;
        }
      } catch (_) {}

      if (mounted) {
        setState(() => _isLoading = false);
      }
    } on DioException catch (e) {
      if (mounted) {
        setState(() {
          _isLoading = false;
          _errorMessage = mapDioError(e).l10nKey.tr();
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _isLoading = false;
          _errorMessage = 'errors.generic'.tr();
        });
      }
    }
  }

  Future<void> _sendMonthlyReminders() async {
    setState(() => _isSendingReminders = true);
    try {
      final resp = await widget.apiClient.post<dynamic>(
        '/api/v1/coordinator/volunteers/reminders:send-monthly?organizationId=${widget.organizationId}',
      );
      final dispatched = resp is Map<String, dynamic> ? resp['remindersDispatched'] ?? 0 : 0;

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('coordinator.reminders_sent'.tr(args: ['$dispatched'])),
          ),
        );
        _loadDashboard();
      }
    } on DioException catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(mapDioError(e).l10nKey.tr())),
        );
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('errors.generic'.tr())),
        );
      }
    } finally {
      if (mounted) setState(() => _isSendingReminders = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: Text('coordinator.attention_title'.tr()),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            tooltip: 'common.retry'.tr(),
            onPressed: _loadDashboard,
          ),
        ],
      ),
      body: SafeArea(
        child: _isLoading
            ? AppLoading(message: 'common.loading'.tr())
            : _errorMessage != null
                ? AppErrorView(
                    message: _errorMessage!,
                    retryLabel: 'common.retry'.tr(),
                    onRetry: _loadDashboard,
                  )
                : RefreshIndicator(
                    onRefresh: _loadDashboard,
                    child: ListView(
                      padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                      children: [
                        _buildQuickActionCards(theme),
                        const SizedBox(height: AppSpacing.lg),
                        _buildExpiringSection(theme),
                        const SizedBox(height: AppSpacing.lg),
                        _buildSilentVolunteersSection(theme),
                      ],
                    ),
                  ),
      ),
    );
  }

  Widget _buildQuickActionCards(ThemeData theme) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          'coordinator.overview_subtitle'.tr(),
          style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: AppSpacing.sm),
        Row(
          children: [
            Expanded(
              child: _MetricCard(
                icon: Icons.pending_actions_outlined,
                color: theme.colorScheme.primary,
                count: _unconfirmedCount,
                label: 'coordinator.unconfirmed_hours'.tr(),
                onTap: () => context.push(
                  '${AppRoutes.organizations}/${widget.organizationId}/coordinator/hours-queue',
                ),
              ),
            ),
            const SizedBox(width: AppSpacing.sm),
            Expanded(
              child: _MetricCard(
                icon: Icons.how_to_reg_outlined,
                color: theme.colorScheme.secondary,
                count: _pendingAppsCount,
                label: 'coordinator.pending_applications'.tr(),
                onTap: () => context.push(
                  '${AppRoutes.organizations}/${widget.organizationId}/forms/volunteer/submissions',
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
                count: _disputedCount,
                label: 'coordinator.disputed_hours'.tr(),
                onTap: () => context.push(
                  '${AppRoutes.organizations}/${widget.organizationId}/coordinator/hours-queue',
                ),
              ),
            ),
            const SizedBox(width: AppSpacing.sm),
            Expanded(
              child: _MetricCard(
                icon: Icons.people_outline,
                color: theme.colorScheme.tertiary,
                count: _silentVolunteers.length,
                label: 'coordinator.silent_volunteers'.tr(),
                onTap: () => context.push(
                  '${AppRoutes.organizations}/${widget.organizationId}/coordinator/roster',
                ),
              ),
            ),
          ],
        ),
      ],
    );
  }

  Widget _buildExpiringSection(ThemeData theme) {
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
                    style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold),
                  ),
                ),
                Text(
                  '${_expiringVerifications.length}',
                  style: theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                    color: theme.colorScheme.error,
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.sm),
            if (_expiringVerifications.isEmpty)
              Text(
                'coordinator.no_expiring_verifications'.tr(),
                style: theme.textTheme.bodyMedium?.copyWith(
                  color: theme.colorScheme.onSurfaceVariant,
                ),
              )
            else
              ..._expiringVerifications.take(5).map((item) {
                final m = item as Map<String, dynamic>;
                final type = m['type'] as String? ?? '';
                final days = m['daysRemaining'] as int? ?? 0;
                return ListTile(
                  dense: true,
                  contentPadding: EdgeInsets.zero,
                  leading: const Icon(Icons.badge_outlined),
                  title: Text(type),
                  subtitle: Text('coordinator.days_remaining'.tr(args: ['$days'])),
                  trailing: days <= 7
                      ? Chip(
                          label: Text('coordinator.urgent'.tr()),
                          backgroundColor: theme.colorScheme.errorContainer,
                          labelStyle: TextStyle(color: theme.colorScheme.onErrorContainer),
                        )
                      : null,
                );
              }),
          ],
        ),
      ),
    );
  }

  Widget _buildSilentVolunteersSection(ThemeData theme) {
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
                    style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold),
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
              isLoading: _isSendingReminders,
              onPressed: _silentVolunteers.isEmpty || _isSendingReminders
                  ? null
                  : _sendMonthlyReminders,
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
              style: theme.textTheme.bodyMedium?.copyWith(fontWeight: FontWeight.w600),
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
            ),
          ],
        ),
      ),
    );
  }
}
