// lib/features/family/presentation/senior_access_log_screen.dart
//
// P6-08 / BR-FAMILY-06: "Wer hat was gesehen?" transparent access log.
// Provides the senior with a clear, plain-language audit trail of every access
// to their profile, help requests, and records in the past 30 days.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_states.dart';
import '../application/senior_access_log_notifier.dart';

class SeniorAccessLogScreen extends ConsumerWidget {
  const SeniorAccessLogScreen({
    super.key,
    required this.apiClient,
    this.seniorUserId,
  });

  final ApiClient apiClient;
  final String? seniorUserId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final params = SeniorAccessLogParams(
      apiClient: apiClient,
      seniorUserId: seniorUserId,
    );
    final state = ref.watch(seniorAccessLogProvider(params));
    final notifier = ref.read(seniorAccessLogProvider(params).notifier);

    return Scaffold(
      appBar: AppBar(
        title: Text('family.access_log_title'.tr()),
      ),
      body: SafeArea(
        child: state.isLoading
            ? AppLoading(message: 'common.loading'.tr())
            : state.error != null
                ? AppErrorView(
                    message: state.error!,
                    retryLabel: 'common.retry'.tr(),
                    onRetry: () => notifier.loadAccessLogs(),
                  )
                : state.logs.isEmpty
                    ? AppEmptyState(
                        icon: Icons.history_toggle_off,
                        message: 'family.no_logs_found'.tr(),
                      )
                    : RefreshIndicator(
                        onRefresh: () => notifier.loadAccessLogs(),
                        child: ListView(
                          padding: const EdgeInsetsDirectional.all(
                              AppSpacing.md),
                          children: [
                            Card(
                              color:
                                  theme.colorScheme.surfaceContainerHighest,
                              shape: RoundedRectangleBorder(
                                borderRadius:
                                    BorderRadius.circular(AppRadius.md),
                              ),
                              child: Padding(
                                padding: const EdgeInsetsDirectional.all(
                                    AppSpacing.md),
                                child: Row(
                                  children: [
                                    Icon(
                                      Icons.shield_outlined,
                                      color: theme.colorScheme.primary,
                                      size: 32,
                                    ),
                                    const SizedBox(width: AppSpacing.md),
                                    Expanded(
                                      child: Text(
                                        'family.access_log_explanation'.tr(),
                                        style: theme.textTheme.bodyMedium,
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            ),
                            const SizedBox(height: AppSpacing.md),
                            ...state.logs.map((log) {
                              return Card(
                                margin: const EdgeInsets.only(
                                    bottom: AppSpacing.sm),
                                shape: RoundedRectangleBorder(
                                  borderRadius:
                                      BorderRadius.circular(AppRadius.md),
                                  side: BorderSide(
                                      color:
                                          theme.colorScheme.outlineVariant),
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
                                          Icon(
                                            Icons.visibility_outlined,
                                            size: 20,
                                            color: theme.colorScheme.primary,
                                          ),
                                          const SizedBox(
                                              width: AppSpacing.sm),
                                          Expanded(
                                            child: Text(
                                              log['accessedByUserName']
                                                      as String? ??
                                                  'family.family_member'.tr(),
                                              style: theme
                                                  .textTheme.titleMedium
                                                  ?.copyWith(
                                                fontWeight: FontWeight.bold,
                                              ),
                                            ),
                                          ),
                                        ],
                                      ),
                                      const SizedBox(height: AppSpacing.xs),
                                      Text(
                                        log['plainLanguageDescription']
                                                as String? ??
                                            '',
                                        style: theme.textTheme.bodyLarge,
                                      ),
                                      const SizedBox(height: AppSpacing.xs),
                                      Text(
                                        '30 Tage Transparenz',
                                        style: theme.textTheme.bodySmall
                                            ?.copyWith(
                                          color: theme
                                              .colorScheme.onSurfaceVariant,
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                              );
                            }),
                          ],
                        ),
                      ),
      ),
    );
  }
}
