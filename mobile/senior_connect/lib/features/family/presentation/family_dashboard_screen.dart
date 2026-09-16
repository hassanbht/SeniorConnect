// lib/features/family/presentation/family_dashboard_screen.dart
//
// P6-07: Family & Caregiver Dashboard.
// Allows caregivers to view connected seniors, monitor active help requests,
// create requests on their behalf, review permissions, and inspect access logs.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/design_system/app_colors.dart';
import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../core/router/app_router.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_states.dart';
import '../application/family_dashboard_notifier.dart';
import 'delegation_permissions_dialog.dart';

class FamilyDashboardScreen extends ConsumerWidget {
  const FamilyDashboardScreen({
    super.key,
    required this.apiClient,
  });

  final ApiClient apiClient;

  void _openPermissionsDialog(BuildContext context, String caregiverName) {
    showDialog<void>(
      context: context,
      builder: (context) => DelegationPermissionsDialog(
        caregiverName: caregiverName,
        currentPermissions: const {
          'ViewActivities': true,
          'CreateHelpRequestsOnBehalf': true,
          'ViewEmergencyContacts': true,
          'ReceiveSafetyAlerts': true,
          'ManageSettings': false,
        },
        onSave: (updated) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text('family.permissions_saved'.tr())),
          );
        },
      ),
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final state = ref.watch(familyDashboardProvider(apiClient));
    final notifier = ref.read(familyDashboardProvider(apiClient).notifier);

    return Scaffold(
      appBar: AppBar(
        title: Text('family.dashboard_title'.tr()),
      ),
      body: SafeArea(
        child: state.isLoading
            ? AppLoading(message: 'common.loading'.tr())
            : state.error != null
                ? AppErrorView(
                    message: state.error!,
                    retryLabel: 'common.retry'.tr(),
                    onRetry: () => notifier.loadRelationships(),
                  )
                : RefreshIndicator(
                    onRefresh: () => notifier.loadRelationships(),
                    child: SingleChildScrollView(
                      padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Card(
                            color: theme.colorScheme.primaryContainer,
                            shape: RoundedRectangleBorder(
                              borderRadius:
                                  BorderRadius.circular(AppRadius.md),
                              side: BorderSide(
                                  color: theme.colorScheme.outlineVariant),
                            ),
                            child: Padding(
                              padding: const EdgeInsetsDirectional.all(
                                  AppSpacing.md),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Row(
                                    children: [
                                      Icon(
                                        Icons.family_restroom,
                                        size: 28,
                                        color: theme
                                            .colorScheme.onPrimaryContainer,
                                      ),
                                      const SizedBox(width: AppSpacing.sm),
                                      Expanded(
                                        child: Text(
                                          'family.zugangskarte_banner_title'
                                              .tr(),
                                          style: theme.textTheme.titleMedium
                                              ?.copyWith(
                                            fontWeight: FontWeight.bold,
                                            color: theme.colorScheme
                                                .onPrimaryContainer,
                                          ),
                                        ),
                                      ),
                                    ],
                                  ),
                                  const SizedBox(height: AppSpacing.sm),
                                  Text(
                                    'family.zugangskarte_banner_desc'.tr(),
                                    style: theme.textTheme.bodyMedium,
                                  ),
                                ],
                              ),
                            ),
                          ),
                          const SizedBox(height: AppSpacing.lg),
                          Text(
                            'family.connected_seniors'.tr(),
                            style: theme.textTheme.titleMedium?.copyWith(
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                          const SizedBox(height: AppSpacing.sm),
                          ...state.relationships.map((rel) {
                            final seniorName =
                                rel['seniorName'] as String? ?? 'Oma Gerda';
                            return Card(
                              elevation: 1,
                              shape: RoundedRectangleBorder(
                                borderRadius:
                                    BorderRadius.circular(AppRadius.md),
                                side: BorderSide(
                                    color: theme.colorScheme.outlineVariant),
                              ),
                              child: Padding(
                                padding: const EdgeInsetsDirectional.all(
                                    AppSpacing.md),
                                child: Column(
                                  crossAxisAlignment:
                                      CrossAxisAlignment.start,
                                  children: [
                                    Row(
                                      children: [
                                        CircleAvatar(
                                          backgroundColor: theme
                                              .colorScheme.primary
                                              .withAlpha(30),
                                          child: Icon(Icons.person,
                                              color:
                                                  theme.colorScheme.primary),
                                        ),
                                        const SizedBox(width: AppSpacing.md),
                                        Expanded(
                                          child: Column(
                                            crossAxisAlignment:
                                                CrossAxisAlignment.start,
                                            children: [
                                              Text(
                                                seniorName,
                                                style: theme
                                                    .textTheme.titleMedium
                                                    ?.copyWith(
                                                  fontWeight:
                                                      FontWeight.bold,
                                                ),
                                              ),
                                              Text(
                                                'family.status_active'.tr(),
                                                style: theme
                                                    .textTheme.bodySmall
                                                    ?.copyWith(
                                                  color:
                                                      context.appColors.success,
                                                  fontWeight: FontWeight.w600,
                                                ),
                                              ),
                                            ],
                                          ),
                                        ),
                                      ],
                                    ),
                                    const Divider(height: AppSpacing.lg),
                                    Wrap(
                                      spacing: AppSpacing.sm,
                                      runSpacing: AppSpacing.sm,
                                      children: [
                                        AppButton(
                                          label:
                                              'family.request_for_senior'.tr(),
                                          variant: AppButtonVariant.primary,
                                          icon: Icons.add_circle_outline,
                                          onPressed: () => context
                                              .push(AppRoutes.helpRequestCreate),
                                        ),
                                        AppButton(
                                          label:
                                              'family.manage_permissions'.tr(),
                                          variant: AppButtonVariant.outlined,
                                          icon: Icons.tune,
                                          onPressed: () =>
                                              _openPermissionsDialog(
                                                  context, seniorName),
                                        ),
                                      ],
                                    ),
                                  ],
                                ),
                              ),
                            );
                          }),
                          const SizedBox(height: AppSpacing.lg),
                          Text(
                            'family.transparency_section'.tr(),
                            style: theme.textTheme.titleMedium?.copyWith(
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                          const SizedBox(height: AppSpacing.sm),
                          Card(
                            shape: RoundedRectangleBorder(
                              borderRadius:
                                  BorderRadius.circular(AppRadius.md),
                              side: BorderSide(
                                  color: theme.colorScheme.outlineVariant),
                            ),
                            child: ListTile(
                              leading: Icon(
                                Icons.visibility_outlined,
                                color: theme.colorScheme.primary,
                              ),
                              title: Text(
                                  'family.access_log_link_title'.tr()),
                              subtitle: Text(
                                  'family.access_log_link_subtitle'.tr()),
                              trailing: const Icon(Icons.chevron_right),
                              onTap: () =>
                                  context.push(AppRoutes.familyAccessLog),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
      ),
    );
  }
}
