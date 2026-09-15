// lib/features/community/presentation/my_appointments_screen.dart
//
// P5-07: "Meine Termine" screen.
// Strict Senior Mode Requirement: Rendered as a clean, vertical list of large cards
// (never a multi-column calendar grid). Docs/design-system.md §3.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_states.dart';
import '../application/my_appointments_notifier.dart';

class MyAppointmentsScreen extends ConsumerWidget {
  const MyAppointmentsScreen({
    super.key,
    required this.apiClient,
  });

  final ApiClient apiClient;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final state = ref.watch(myAppointmentsProvider(apiClient));
    final notifier = ref.read(myAppointmentsProvider(apiClient).notifier);

    return Scaffold(
      appBar: AppBar(
        title: Text('community.my_appointments'.tr()),
      ),
      body: SafeArea(
        child: state.isLoading
            ? AppLoading(message: 'common.loading'.tr())
            : state.error != null
                ? AppErrorView(
                    message: state.error!,
                    retryLabel: 'common.retry'.tr(),
                    onRetry: () => notifier.loadAppointments(),
                  )
                : state.appointments.isEmpty
                    ? AppEmptyState(
                        icon: Icons.event_available,
                        message: 'empty.no_activities'.tr(),
                      )
                    : RefreshIndicator(
                        onRefresh: () => notifier.loadAppointments(),
                        child: ListView.separated(
                          padding:
                              const EdgeInsetsDirectional.all(AppSpacing.md),
                          itemCount: state.appointments.length,
                          separatorBuilder: (_, __) =>
                              const SizedBox(height: AppSpacing.md),
                          itemBuilder: (context, index) {
                            final item = state.appointments[index];
                            final isWaitlisted =
                                item['myStatus'] == 'Waitlisted';
                            final isCancelled =
                                item['isCancelled'] == true;

                            return Card(
                              elevation: 2,
                              shape: RoundedRectangleBorder(
                                borderRadius:
                                    BorderRadius.circular(AppRadius.md),
                                side: BorderSide(
                                  color: isCancelled
                                      ? theme.colorScheme.error
                                      : isWaitlisted
                                          ? theme.colorScheme.tertiary
                                          : theme.colorScheme.outlineVariant,
                                  width: 1.5,
                                ),
                              ),
                              child: Padding(
                                padding: const EdgeInsetsDirectional.all(
                                    AppSpacing.lg),
                                child: Column(
                                  crossAxisAlignment:
                                      CrossAxisAlignment.start,
                                  children: [
                                    Row(
                                      children: [
                                        Expanded(
                                          child: Text(
                                            item['title'] as String? ?? '',
                                            style: theme.textTheme.titleMedium
                                                ?.copyWith(
                                              fontWeight: FontWeight.bold,
                                            ),
                                          ),
                                        ),
                                        _StatusBadge(
                                          isCancelled: isCancelled,
                                          isWaitlisted: isWaitlisted,
                                          waitlistPosition:
                                              item['waitlistPosition']
                                                  as int?,
                                          theme: theme,
                                        ),
                                      ],
                                    ),
                                    const SizedBox(height: AppSpacing.sm),
                                    if (item['locationAddress'] != null) ...[
                                      Row(
                                        children: [
                                          Icon(Icons.location_on,
                                              size: 18,
                                              color: theme.colorScheme
                                                  .onSurfaceVariant),
                                          const SizedBox(
                                              width: AppSpacing.xs),
                                          Expanded(
                                            child: Text(
                                              item['locationAddress']
                                                  as String,
                                              style: theme
                                                  .textTheme.bodyMedium
                                                  ?.copyWith(
                                                color: theme.colorScheme
                                                    .onSurfaceVariant,
                                              ),
                                            ),
                                          ),
                                        ],
                                      ),
                                      const SizedBox(height: AppSpacing.sm),
                                    ],
                                    Row(
                                      children: [
                                        Icon(Icons.access_time,
                                            size: 18,
                                            color: theme
                                                .colorScheme.primary),
                                        const SizedBox(
                                            width: AppSpacing.xs),
                                        Text(
                                          'community.starts_soon'.tr(),
                                          style: theme
                                              .textTheme.bodyMedium
                                              ?.copyWith(
                                            fontWeight: FontWeight.w600,
                                            color: theme.colorScheme.primary,
                                          ),
                                        ),
                                      ],
                                    ),
                                    const SizedBox(height: AppSpacing.md),
                                    AppButton(
                                      label:
                                          'community.cancel_registration'.tr(),
                                      variant: AppButtonVariant.outlined,
                                      icon: Icons.cancel_outlined,
                                      onPressed: () => notifier
                                          .cancelAppointment(
                                              item['eventId'] as String),
                                    ),
                                  ],
                                ),
                              ),
                            );
                          },
                        ),
                      ),
      ),
    );
  }
}

class _StatusBadge extends StatelessWidget {
  const _StatusBadge({
    required this.isCancelled,
    required this.isWaitlisted,
    this.waitlistPosition,
    required this.theme,
  });

  final bool isCancelled;
  final bool isWaitlisted;
  final int? waitlistPosition;
  final ThemeData theme;

  @override
  Widget build(BuildContext context) {
    if (isCancelled) {
      return Container(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
        decoration: BoxDecoration(
          color: theme.colorScheme.errorContainer,
          borderRadius: BorderRadius.circular(AppRadius.sm),
        ),
        child: Text(
          'community.cancelled'.tr(),
          style: theme.textTheme.bodySmall?.copyWith(
            color: theme.colorScheme.onErrorContainer,
            fontWeight: FontWeight.bold,
          ),
        ),
      );
    }

    if (isWaitlisted) {
      return Container(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
        decoration: BoxDecoration(
          color: theme.colorScheme.tertiaryContainer,
          borderRadius: BorderRadius.circular(AppRadius.sm),
        ),
        child: Text(
          waitlistPosition != null
              ? '${'community.waitlist'.tr()} (#$waitlistPosition)'
              : 'community.waitlist'.tr(),
          style: theme.textTheme.bodySmall?.copyWith(
            color: theme.colorScheme.onTertiaryContainer,
            fontWeight: FontWeight.bold,
          ),
        ),
      );
    }

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: theme.colorScheme.primaryContainer,
        borderRadius: BorderRadius.circular(AppRadius.sm),
      ),
      child: Text(
        'community.confirmed'.tr(),
        style: theme.textTheme.bodySmall?.copyWith(
          color: theme.colorScheme.onPrimaryContainer,
          fontWeight: FontWeight.bold,
        ),
      ),
    );
  }
}
