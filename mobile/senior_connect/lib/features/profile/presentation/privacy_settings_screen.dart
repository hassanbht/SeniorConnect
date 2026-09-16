// lib/features/profile/presentation/privacy_settings_screen.dart
//
// P7-05, P7-06, P7-07: Privacy, GDPR consents, data export and account deletion.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_states.dart';
import '../application/privacy_settings_notifier.dart';

class PrivacySettingsScreen extends ConsumerWidget {
  const PrivacySettingsScreen({super.key, required this.apiClient});

  final ApiClient apiClient;

  Future<void> _exportData(BuildContext context, WidgetRef ref) async {
    final notifier = ref.read(privacySettingsProvider(apiClient).notifier);
    final result = await notifier.exportUserData();
    if (!context.mounted) return;

    if (result != null) {
      final sizeKb = (result.rawJson.length / 1024).toStringAsFixed(1);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('privacy.export_success'.tr(args: [sizeKb])),
          duration: const Duration(seconds: 4),
        ),
      );
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('errors.generic'.tr()),
          backgroundColor: Theme.of(context).colorScheme.error,
        ),
      );
    }
  }

  Future<void> _withdrawConsent(
      BuildContext context, WidgetRef ref, String consentType) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('privacy.withdraw'.tr()),
        content: Text('privacy.withdraw_confirm'.tr()),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(false),
            child: Text('common.cancel'.tr()),
          ),
          FilledButton(
            onPressed: () => Navigator.of(ctx).pop(true),
            child: Text('privacy.withdraw'.tr()),
          ),
        ],
      ),
    );

    if (confirmed != true || !context.mounted) return;

    final success = await ref
        .read(privacySettingsProvider(apiClient).notifier)
        .withdrawConsent(consentType);
    if (!context.mounted) return;

    if (success) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('privacy.withdraw_success'.tr())),
      );
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('errors.generic'.tr()),
          backgroundColor: Theme.of(context).colorScheme.error,
        ),
      );
    }
  }

  Future<void> _requestAccountDeletion(
      BuildContext context, WidgetRef ref) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('privacy.delete_confirm_title'.tr()),
        content: Text('privacy.delete_confirm_desc'.tr()),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(false),
            child: Text('common.cancel'.tr()),
          ),
          FilledButton(
            style: FilledButton.styleFrom(
              backgroundColor: Theme.of(ctx).colorScheme.error,
              foregroundColor: Theme.of(ctx).colorScheme.onError,
            ),
            onPressed: () => Navigator.of(ctx).pop(true),
            child: Text('privacy.delete_account'.tr()),
          ),
        ],
      ),
    );

    if (confirmed != true || !context.mounted) return;

    final req = await ref
        .read(privacySettingsProvider(apiClient).notifier)
        .requestDeletion('User self-service deletion');
    if (!context.mounted) return;

    if (req != null) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('privacy.delete_requested'.tr())),
      );
      _showTokenConfirmationDialog(context, ref);
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('errors.generic'.tr()),
          backgroundColor: Theme.of(context).colorScheme.error,
        ),
      );
    }
  }

  Future<void> _showTokenConfirmationDialog(
      BuildContext context, WidgetRef ref) async {
    final tokenController = TextEditingController();
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('privacy.delete_account'.tr()),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('privacy.delete_token_prompt'.tr()),
            const SizedBox(height: 12),
            TextField(
              controller: tokenController,
              autofocus: true,
              decoration: const InputDecoration(
                border: OutlineInputBorder(),
                hintText: 'Token...',
              ),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(false),
            child: Text('common.cancel'.tr()),
          ),
          FilledButton(
            style: FilledButton.styleFrom(
              backgroundColor: Theme.of(ctx).colorScheme.error,
              foregroundColor: Theme.of(ctx).colorScheme.onError,
            ),
            onPressed: () => Navigator.of(ctx).pop(true),
            child: Text('common.confirm'.tr()),
          ),
        ],
      ),
    );

    if (confirmed != true || !context.mounted) return;

    final token = tokenController.text.trim();
    if (token.isEmpty) return;

    final success = await ref
        .read(privacySettingsProvider(apiClient).notifier)
        .confirmDeletion(token);
    if (!context.mounted) return;

    if (success) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('privacy.delete_confirmed'.tr())),
      );
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('errors.generic'.tr()),
          backgroundColor: Theme.of(context).colorScheme.error,
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;
    final state = ref.watch(privacySettingsProvider(apiClient));
    final notifier = ref.read(privacySettingsProvider(apiClient).notifier);

    return Scaffold(
      appBar: AppBar(
        title: Text('privacy.title'.tr()),
      ),
      body: state.when(
        initial: () => const Center(child: CircularProgressIndicator()),
        loading: () => const Center(child: CircularProgressIndicator()),
        empty: (_) => const SizedBox.shrink(),
        error: (key, error) => AppErrorView(
          message: key.tr(),
          retryLabel: 'common.retry'.tr(),
          onRetry: notifier.load,
        ),
        loaded: (data) => ListView(
          padding: const EdgeInsets.all(AppSpacing.lg),
          children: [
            // Data export section
            Card(
              elevation: 0,
              color: colorScheme.surfaceContainerLow,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(AppRadius.md),
                side: BorderSide(color: colorScheme.outlineVariant),
              ),
              child: Padding(
                padding: const EdgeInsets.all(AppSpacing.md),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Row(
                      children: [
                        Icon(Icons.download_for_offline_outlined,
                            color: colorScheme.primary, size: 28),
                        const SizedBox(width: AppSpacing.sm),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                'privacy.export_title'.tr(),
                                style: theme.textTheme.titleMedium
                                    ?.copyWith(fontWeight: FontWeight.bold),
                              ),
                              Text(
                                'privacy.export_subtitle'.tr(),
                                style: theme.textTheme.bodySmall?.copyWith(
                                  color: colorScheme.onSurfaceVariant,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: AppSpacing.md),
                    FilledButton.tonalIcon(
                      onPressed: () => _exportData(context, ref),
                      icon: const Icon(Icons.file_download_outlined),
                      label: Text('privacy.export_button'.tr()),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: AppSpacing.lg),

            // Active Consents section
            Text(
              'privacy.consents_title'.tr(),
              style: theme.textTheme.titleMedium?.copyWith(
                fontWeight: FontWeight.bold,
                color: colorScheme.primary,
              ),
            ),
            const SizedBox(height: AppSpacing.xs),
            Text(
              'privacy.consents_desc'.tr(),
              style: theme.textTheme.bodySmall?.copyWith(
                color: colorScheme.onSurfaceVariant,
              ),
            ),
            const SizedBox(height: AppSpacing.sm),

            if (data.consents.isEmpty)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: AppSpacing.md),
                child: Text(
                  'empty.no_items'.tr(),
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: colorScheme.onSurfaceVariant,
                  ),
                ),
              )
            else
              ...data.consents.map((consent) {
                final dateFormatted =
                    '${consent.grantedAtUtc.day}.${consent.grantedAtUtc.month}.${consent.grantedAtUtc.year}';
                return Card(
                  margin: const EdgeInsets.only(bottom: AppSpacing.sm),
                  elevation: 0,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(AppRadius.sm),
                    side: BorderSide(color: colorScheme.outlineVariant),
                  ),
                  child: ListTile(
                    contentPadding: const EdgeInsets.symmetric(
                      horizontal: AppSpacing.md,
                      vertical: AppSpacing.xs,
                    ),
                    leading: Icon(
                      consent.granted
                          ? Icons.check_circle_outline
                          : Icons.cancel_outlined,
                      color: consent.granted
                          ? colorScheme.primary
                          : colorScheme.error,
                    ),
                    title: Text(
                      _resolveConsentTitle(consent.consentType),
                      style: theme.textTheme.bodyLarge?.copyWith(
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    subtitle: Text(
                      'privacy.consent_version'.tr(
                        args: [consent.documentVersion, dateFormatted],
                      ),
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: colorScheme.onSurfaceVariant,
                      ),
                    ),
                    trailing: consent.granted
                        ? OutlinedButton(
                            onPressed: () => _withdrawConsent(
                                context, ref, consent.consentType),
                            child: Text('privacy.withdraw'.tr()),
                          )
                        : Text(
                            'privacy.withdraw_success'.tr(),
                            style: theme.textTheme.labelSmall?.copyWith(
                              color: colorScheme.outline,
                            ),
                          ),
                  ),
                );
              }),

            const SizedBox(height: AppSpacing.xl),

            // Danger Zone: Account Deletion
            Card(
              elevation: 0,
              color: colorScheme.errorContainer.withValues(alpha: 0.2),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(AppRadius.md),
                side: BorderSide(color: colorScheme.error.withValues(alpha: 0.5)),
              ),
              child: Padding(
                padding: const EdgeInsets.all(AppSpacing.md),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text(
                      'privacy.delete_account'.tr(),
                      style: theme.textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                        color: colorScheme.error,
                      ),
                    ),
                    const SizedBox(height: AppSpacing.xs),
                    Text(
                      'privacy.delete_account_desc'.tr(),
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: colorScheme.onSurfaceVariant,
                      ),
                    ),
                    const SizedBox(height: AppSpacing.md),
                    OutlinedButton(
                      style: OutlinedButton.styleFrom(
                        foregroundColor: colorScheme.error,
                        side: BorderSide(color: colorScheme.error),
                      ),
                      onPressed: () => _requestAccountDeletion(context, ref),
                      child: Text('privacy.delete_account'.tr()),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  String _resolveConsentTitle(String consentType) {
    switch (consentType.toLowerCase()) {
      case 'terms':
        return 'privacy.consent_terms'.tr();
      case 'privacy':
        return 'settings.privacy'.tr();
      case 'notificationspush':
      case 'notificationssms':
      case 'notificationsemail':
        return 'privacy.consent_contact'.tr();
      default:
        return consentType;
    }
  }
}
