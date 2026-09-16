// lib/features/organizations/presentation/organization_profile_screen.dart
//
// P2-25: Per-organization page — news and events, view-only for any
// authenticated user; staff (active Coordinator/Admin) additionally see a
// "manage" entry point into OrganizationPostFormScreen.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../core/router/app_router.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_states.dart';
import '../application/organization_profile_notifier.dart';
import '../data/intake_form_repository.dart';
import 'organization_post_form_screen.dart';

class OrganizationProfileScreen extends ConsumerWidget {
  const OrganizationProfileScreen({
    super.key,
    required this.organizationId,
    required this.apiClient,
  });

  final String organizationId;
  final ApiClient apiClient;

  void _openIntakeForm(BuildContext context, IntakeFormType formType) {
    context.pushNamed(
      'intake-form',
      pathParameters: {
        'id': organizationId,
        'formType': _formTypeSegment(formType),
      },
    );
  }

  void _openSubmissions(BuildContext context, IntakeFormType formType) {
    context.pushNamed(
      'intake-form-submissions',
      pathParameters: {
        'id': organizationId,
        'formType': _formTypeSegment(formType),
      },
    );
  }

  String _formTypeSegment(IntakeFormType formType) =>
      formType == IntakeFormType.helpSeeker ? 'help_seeker' : 'volunteer';

  Future<void> _activateFwzTemplate(
    BuildContext context,
    WidgetRef ref,
    OrganizationProfileParams params,
  ) async {
    final error = await ref
        .read(organizationProfileProvider(params).notifier)
        .activateFwzTemplate();
    if (!context.mounted) return;
    if (error == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('organizations.template_activated'.tr())),
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
    final params = OrganizationProfileParams(
      organizationId: organizationId,
      apiClient: apiClient,
    );
    final state = ref.watch(organizationProfileProvider(params));
    final orgName = state.organization?['name'] as String? ?? '';

    return Scaffold(
      appBar: AppBar(title: Text(orgName)),
      floatingActionButton: state.canManage
          ? FloatingActionButton.extended(
              onPressed: () async {
                final saved = await Navigator.of(context).push<bool>(
                  MaterialPageRoute<bool>(
                    builder: (_) => OrganizationPostFormScreen(
                      organizationId: organizationId,
                      apiClient: apiClient,
                    ),
                  ),
                );
                if (saved == true) {
                  ref
                      .read(organizationProfileProvider(params).notifier)
                      .load();
                }
              },
              icon: const Icon(Icons.add),
              label: Text('organizations.post_news_or_event'.tr()),
            )
          : null,
      body: SafeArea(
        child: state.isLoading
            ? AppLoading(message: 'common.loading'.tr())
            : ListView(
                padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                children: [
                  if (state.organization?['supportEmail'] != null ||
                      state.organization?['supportPhone'] != null)
                    Padding(
                      padding: const EdgeInsetsDirectional.only(
                        bottom: AppSpacing.md,
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          if (state.organization?['supportEmail'] != null)
                            Text(
                              state.organization!['supportEmail'] as String,
                              style: theme.textTheme.bodyMedium,
                            ),
                          if (state.organization?['supportPhone'] != null)
                            Text(
                              state.organization!['supportPhone'] as String,
                              style: theme.textTheme.bodyMedium,
                            ),
                        ],
                      ),
                    ),
                  Text(
                    'organizations.news'.tr(),
                    style: theme.textTheme.titleLarge,
                  ),
                  const SizedBox(height: AppSpacing.sm),
                  if (state.newsItems.isEmpty)
                    Padding(
                      padding: const EdgeInsetsDirectional.only(
                        bottom: AppSpacing.md,
                      ),
                      child: Text('organizations.no_posts'.tr()),
                    )
                  else
                    ...state.newsItems.map((item) => _PostCard(item: item)),
                  const SizedBox(height: AppSpacing.lg),
                  Text(
                    'organizations.events'.tr(),
                    style: theme.textTheme.titleLarge,
                  ),
                  const SizedBox(height: AppSpacing.sm),
                  if (state.events.isEmpty)
                    Text('organizations.no_posts'.tr())
                  else
                    ...state.events.map((item) => _PostCard(item: item)),
                  const SizedBox(height: AppSpacing.xl),
                  if (state.canManage) ...[
                    Text(
                      'organizations.coordinator_tools_title'.tr(),
                      style: theme.textTheme.titleLarge,
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    AppButton(
                      label: 'coordinator.attention_title'.tr(),
                      icon: Icons.warning_amber_rounded,
                      variant: AppButtonVariant.primary,
                      onPressed: () => context.push(
                        '${AppRoutes.organizations}/$organizationId/coordinator/attention',
                      ),
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    AppButton(
                      label: 'coordinator.roster_title'.tr(),
                      icon: Icons.people_outline,
                      variant: AppButtonVariant.tonal,
                      onPressed: () => context.push(
                        '${AppRoutes.organizations}/$organizationId/coordinator/roster',
                      ),
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    AppButton(
                      label: 'coordinator.hours_queue_title'.tr(),
                      icon: Icons.pending_actions_outlined,
                      variant: AppButtonVariant.tonal,
                      onPressed: () => context.push(
                        '${AppRoutes.organizations}/$organizationId/coordinator/hours-queue',
                      ),
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    AppButton(
                      label: 'coordinator.bulk_entry_title'.tr(),
                      icon: Icons.edit_note_outlined,
                      variant: AppButtonVariant.tonal,
                      onPressed: () => context.push(
                        '${AppRoutes.organizations}/$organizationId/coordinator/bulk-entry',
                      ),
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    AppButton(
                      label: 'organizations.review_volunteer_applications'.tr(),
                      icon: Icons.how_to_reg_outlined,
                      variant: AppButtonVariant.tonal,
                      onPressed: () =>
                          _openSubmissions(context, IntakeFormType.volunteer),
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    AppButton(
                      label:
                          'organizations.review_help_seeker_applications'.tr(),
                      icon: Icons.assignment_ind_outlined,
                      variant: AppButtonVariant.tonal,
                      onPressed: () =>
                          _openSubmissions(context, IntakeFormType.helpSeeker),
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    AppButton(
                      label: 'organizations.activate_fwz_template'.tr(),
                      variant: AppButtonVariant.destructive,
                      confirmationText:
                          'organizations.activate_fwz_template_confirm'.tr(),
                      isLoading: state.isActivatingTemplate,
                      onPressed: state.isActivatingTemplate
                          ? null
                          : () => _activateFwzTemplate(context, ref, params),
                    ),
                  ] else ...[
                    Text(
                      'organizations.get_involved_title'.tr(),
                      style: theme.textTheme.titleLarge,
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    AppButton(
                      label: 'organizations.apply_volunteer'.tr(),
                      onPressed: () =>
                          _openIntakeForm(context, IntakeFormType.volunteer),
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    AppButton(
                      label: 'organizations.apply_help_seeker'.tr(),
                      variant: AppButtonVariant.tonal,
                      onPressed: () =>
                          _openIntakeForm(context, IntakeFormType.helpSeeker),
                    ),
                  ],
                ],
              ),
      ),
    );
  }
}

class _PostCard extends StatelessWidget {
  const _PostCard({required this.item});

  final Map<String, dynamic> item;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final title = item['title'] as String? ?? '';
    final description = item['description'] as String? ?? '';
    final isCancelled = item['isCancelled'] as bool? ?? false;

    return Card(
      elevation: 1,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.md),
      ),
      margin: const EdgeInsetsDirectional.only(bottom: AppSpacing.sm),
      child: Padding(
        padding: const EdgeInsetsDirectional.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              title,
              style: theme.textTheme.titleMedium?.copyWith(
                decoration: isCancelled ? TextDecoration.lineThrough : null,
              ),
            ),
            if (description.isNotEmpty) ...[
              const SizedBox(height: AppSpacing.xs),
              Text(description, style: theme.textTheme.bodyMedium),
            ],
          ],
        ),
      ),
    );
  }
}
