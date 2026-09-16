// lib/features/organizations/presentation/coordinator_roster_screen.dart
//
// P2-27 / BR-ROSTER-01: Volunteer Roster with behavioral status classification
// (Active, Dormant, Inactive, NeverActivated) and one-tap reactivation (P2-19).

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_states.dart';
import '../../../shared/widgets/app_status.dart';
import '../application/coordinator_roster_notifier.dart';

class CoordinatorRosterScreen extends ConsumerWidget {
  const CoordinatorRosterScreen({
    super.key,
    required this.organizationId,
    required this.apiClient,
  });

  final String organizationId;
  final ApiClient apiClient;

  Future<void> _reactivate(
    BuildContext context,
    WidgetRef ref,
    CoordinatorRosterParams params,
    String volunteerUserId,
  ) async {
    final error = await ref
        .read(coordinatorRosterProvider(params).notifier)
        .reactivate(volunteerUserId);
    if (!context.mounted) return;

    if (error == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('coordinator.volunteer_reactivated'.tr())),
      );
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(error.tr())),
      );
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final params = CoordinatorRosterParams(
      organizationId: organizationId,
      apiClient: apiClient,
    );
    final state = ref.watch(coordinatorRosterProvider(params));
    final notifier = ref.read(coordinatorRosterProvider(params).notifier);

    return Scaffold(
      appBar: AppBar(
        title: Text('coordinator.roster_title'.tr()),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            tooltip: 'common.retry'.tr(),
            onPressed: () => notifier.loadRoster(),
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
                    onRetry: () => notifier.loadRoster(),
                  )
                : Column(
                    children: [
                      Padding(
                        padding:
                            const EdgeInsetsDirectional.all(AppSpacing.md),
                        child: Column(
                          children: [
                            TextField(
                              decoration: InputDecoration(
                                prefixIcon: const Icon(Icons.search),
                                hintText:
                                    'coordinator.search_volunteers_hint'.tr(),
                                border: OutlineInputBorder(
                                  borderRadius:
                                      BorderRadius.circular(AppRadius.md),
                                ),
                                isDense: true,
                              ),
                              onChanged: (val) =>
                                  notifier.setSearchQuery(val.trim()),
                            ),
                            const SizedBox(height: AppSpacing.sm),
                            SingleChildScrollView(
                              scrollDirection: Axis.horizontal,
                              child: Row(
                                children: [
                                  _filterChip(
                                    notifier,
                                    state.selectedStatusFilter,
                                    'all',
                                    'coordinator.status_all'.tr(),
                                  ),
                                  const SizedBox(width: AppSpacing.xs),
                                  _filterChip(
                                    notifier,
                                    state.selectedStatusFilter,
                                    'Active',
                                    'coordinator.status_active'.tr(),
                                  ),
                                  const SizedBox(width: AppSpacing.xs),
                                  _filterChip(
                                    notifier,
                                    state.selectedStatusFilter,
                                    'Dormant',
                                    'coordinator.status_dormant'.tr(),
                                  ),
                                  const SizedBox(width: AppSpacing.xs),
                                  _filterChip(
                                    notifier,
                                    state.selectedStatusFilter,
                                    'Inactive',
                                    'coordinator.status_inactive'.tr(),
                                  ),
                                  const SizedBox(width: AppSpacing.xs),
                                  _filterChip(
                                    notifier,
                                    state.selectedStatusFilter,
                                    'NeverActivated',
                                    'coordinator.status_never_activated'.tr(),
                                  ),
                                ],
                              ),
                            ),
                          ],
                        ),
                      ),
                      const Divider(height: 1),
                      Expanded(
                        child: state.filteredVolunteers.isEmpty
                            ? AppEmptyState(
                                icon: Icons.people_outline,
                                message:
                                    'coordinator.no_volunteers_found'.tr(),
                              )
                            : ListView.separated(
                                padding: const EdgeInsetsDirectional.all(
                                    AppSpacing.md),
                                itemCount: state.filteredVolunteers.length,
                                separatorBuilder: (_, _) =>
                                    const SizedBox(height: AppSpacing.sm),
                                itemBuilder: (context, index) {
                                  final v = state.filteredVolunteers[index]
                                      as Map<String, dynamic>;
                                  return _VolunteerCard(
                                    volunteer: v,
                                    theme: theme,
                                    onReactivate: () => _reactivate(
                                      context,
                                      ref,
                                      params,
                                      v['userId'] as String,
                                    ),
                                  );
                                },
                              ),
                      ),
                    ],
                  ),
      ),
    );
  }

  Widget _filterChip(
    CoordinatorRosterNotifier notifier,
    String selectedFilter,
    String filterKey,
    String label,
  ) {
    final isSelected = selectedFilter == filterKey;
    return ChoiceChip(
      label: Text(label),
      selected: isSelected,
      onSelected: (selected) {
        if (selected) notifier.setStatusFilter(filterKey);
      },
    );
  }
}

class _VolunteerCard extends StatelessWidget {
  const _VolunteerCard({
    required this.volunteer,
    required this.theme,
    required this.onReactivate,
  });

  final Map<String, dynamic> volunteer;
  final ThemeData theme;
  final VoidCallback onReactivate;

  @override
  Widget build(BuildContext context) {
    final name = volunteer['displayName'] as String? ?? '–';
    final phone = volunteer['phone'] as String?;
    final email = volunteer['email'] as String?;
    final status = volunteer['rosterStatus'] as String? ?? 'NeverActivated';
    final totalHours = volunteer['totalHoursLogged'] as int? ?? 0;
    final lastDate = volunteer['lastActivityDate'] as String?;

    AppStatusTone chipTone = AppStatusTone.neutral;
    if (status == 'Active') chipTone = AppStatusTone.success;
    if (status == 'Dormant') chipTone = AppStatusTone.pending;
    if (status == 'Inactive') chipTone = AppStatusTone.error;

    final isReactivatable = status == 'Dormant' || status == 'Inactive';

    return Card(
      child: Padding(
        padding: const EdgeInsetsDirectional.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Expanded(
                  child: Text(
                    name,
                    style: theme.textTheme.titleMedium
                        ?.copyWith(fontWeight: FontWeight.bold),
                  ),
                ),
                AppStatusChip(tone: chipTone, label: status),
              ],
            ),
            const SizedBox(height: AppSpacing.xs),
            if (phone != null) ...[
              Row(
                children: [
                  const Icon(Icons.phone_outlined, size: 16),
                  const SizedBox(width: AppSpacing.xs),
                  Text(phone, style: theme.textTheme.bodySmall),
                ],
              ),
              const SizedBox(height: 2),
            ],
            if (email != null) ...[
              Row(
                children: [
                  const Icon(Icons.email_outlined, size: 16),
                  const SizedBox(width: AppSpacing.xs),
                  Text(email, style: theme.textTheme.bodySmall),
                ],
              ),
              const SizedBox(height: 2),
            ],
            const SizedBox(height: AppSpacing.sm),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  'coordinator.hours_logged'.tr(args: ['$totalHours']),
                  style: theme.textTheme.bodyMedium
                      ?.copyWith(fontWeight: FontWeight.w600),
                ),
                Text(
                  lastDate != null
                      ? 'coordinator.last_active'.tr(args: [lastDate])
                      : 'coordinator.never_active'.tr(),
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: theme.colorScheme.onSurfaceVariant,
                  ),
                ),
              ],
            ),
            if (isReactivatable) ...[
              const SizedBox(height: AppSpacing.sm),
              Align(
                alignment: AlignmentDirectional.centerEnd,
                child: AppButton(
                  label: 'coordinator.reactivate_action'.tr(),
                  icon: Icons.refresh,
                  variant: AppButtonVariant.tonal,
                  onPressed: onReactivate,
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
