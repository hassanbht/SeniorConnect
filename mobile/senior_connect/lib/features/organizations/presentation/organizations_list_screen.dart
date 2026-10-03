// lib/features/organizations/presentation/organizations_list_screen.dart
//
// P2-25: Organization directory — entry point into each org's profile page.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_states.dart';
import '../application/organizations_list_notifier.dart';
import 'organization_profile_screen.dart';

class OrganizationsListScreen extends ConsumerWidget {
  const OrganizationsListScreen({super.key, required this.apiClient});

  final ApiClient apiClient;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final asyncState = ref.watch(organizationsListProvider(apiClient));

    return Scaffold(
      appBar: AppBar(title: Text('organizations.directory_title'.tr())),
      body: SafeArea(
        child: asyncState.when(
          initial: () => AppLoading(message: 'common.loading'.tr()),
          loading: () => AppLoading(message: 'common.loading'.tr()),
          empty: (_) => AppEmptyState(
            icon: Icons.apartment_outlined,
            message: 'organizations.empty'.tr(),
          ),
          error: (message, _) => AppErrorView(
            message: message,
            retryLabel: 'common.retry'.tr(),
            onRetry: () =>
                ref.read(organizationsListProvider(apiClient).notifier).load(),
          ),
          loaded: (organizations) => ListView.separated(
            padding: const EdgeInsetsDirectional.all(AppSpacing.md),
            itemCount: organizations.length,
            separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.sm),
            itemBuilder: (context, index) {
              final org = organizations[index];
              final id = org['id'] as String? ?? '';
              final name = org['name'] as String? ?? '';
              return Card(
                elevation: 1,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(AppRadius.md),
                ),
                child: ListTile(
                  title: Text(name, style: theme.textTheme.titleMedium),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: id.isEmpty
                      ? null
                      : () => Navigator.of(context).push(
                            MaterialPageRoute<void>(
                              builder: (_) => OrganizationProfileScreen(
                                organizationId: id,
                                apiClient: apiClient,
                              ),
                            ),
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
