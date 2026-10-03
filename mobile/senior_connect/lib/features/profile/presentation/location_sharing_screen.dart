// lib/features/profile/presentation/location_sharing_screen.dart
//
// Phase 3: "Who can see where I live?" — the person chooses how much of their
// location others may see. Plain wording that states the actual effect of each
// option (no vague "allow location"). Precise sharing asks for confirmation.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_states.dart';
import '../application/location_sharing_notifier.dart';
import '../data/location_sharing_repository.dart';

class LocationSharingScreen extends ConsumerWidget {
  const LocationSharingScreen({super.key, required this.apiClient});

  final ApiClient apiClient;

  static const _optionKeys = {
    LocationSharingLevel.none: 'none',
    LocationSharingLevel.approximate: 'approximate',
    LocationSharingLevel.precise: 'precise',
  };

  Future<void> _choose(
    BuildContext context,
    WidgetRef ref,
    LocationSharingLevel level,
  ) async {
    if (level == LocationSharingLevel.precise) {
      final confirmed = await showDialog<bool>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: Text('location_sharing.precise_confirm_title'.tr()),
          content: Text('location_sharing.precise_confirm_body'.tr()),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(ctx).pop(false),
              child: Text('common.cancel'.tr()),
            ),
            FilledButton(
              onPressed: () => Navigator.of(ctx).pop(true),
              child: Text('location_sharing.precise_confirm_yes'.tr()),
            ),
          ],
        ),
      );
      if (confirmed != true) return;
    }

    final notifier = ref.read(locationSharingProvider(apiClient).notifier);
    final ok = await notifier.change(level);
    if (!context.mounted) return;

    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(
          ok
              ? 'location_sharing.saved'.tr()
              : (notifier.saveErrorKey ?? 'errors.generic').tr(),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(locationSharingProvider(apiClient));
    final notifier = ref.read(locationSharingProvider(apiClient).notifier);
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(title: Text('location_sharing.title'.tr())),
      body: SafeArea(
        child: state.when(
          initial: () => AppLoading(message: 'common.loading'.tr()),
          loading: () => AppLoading(message: 'common.loading'.tr()),
          empty: (_) => AppLoading(message: 'common.loading'.tr()),
          error: (key, _) => AppErrorView(
            message: key.tr(),
            retryLabel: 'common.retry'.tr(),
            onRetry: notifier.load,
          ),
          loaded: (sharing) => ListView(
            padding: const EdgeInsetsDirectional.all(AppSpacing.lg),
            children: [
              Text(
                'location_sharing.intro'.tr(),
                style: theme.textTheme.bodyLarge,
              ),
              const SizedBox(height: AppSpacing.md),
              RadioGroup<LocationSharingLevel>(
                groupValue: sharing.level,
                onChanged: (level) {
                  if (level != null && level != sharing.level) {
                    _choose(context, ref, level);
                  }
                },
                child: Column(
                  children: [
                    for (final level in LocationSharingLevel.values)
                      Card(
                        margin: const EdgeInsetsDirectional.only(
                          bottom: AppSpacing.sm,
                        ),
                        child: RadioListTile<LocationSharingLevel>(
                          key: ValueKey('location-sharing-${level.code}'),
                          value: level,
                          contentPadding: const EdgeInsetsDirectional.all(
                            AppSpacing.sm,
                          ),
                          title: Text(
                            'location_sharing.${_optionKeys[level]}_title'.tr(),
                            style: theme.textTheme.titleMedium,
                          ),
                          subtitle: Text(
                            'location_sharing.${_optionKeys[level]}_desc'.tr(),
                          ),
                        ),
                      ),
                  ],
                ),
              ),
              const SizedBox(height: AppSpacing.md),
              // Phase 4 / ADR-026: the visit address is a separate rule,
              // explained here so nobody mistakes this setting for it.
              Card(
                key: const ValueKey('visit-address-info'),
                child: Padding(
                  padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'location_sharing.visit_address_title'.tr(),
                        style: theme.textTheme.titleMedium,
                      ),
                      const SizedBox(height: AppSpacing.xs),
                      Text('location_sharing.visit_address_body'.tr()),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: AppSpacing.md),
              Text(
                'location_sharing.footnote'.tr(),
                style: theme.textTheme.bodySmall?.copyWith(
                  color: theme.colorScheme.onSurfaceVariant,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
