// lib/features/profile/presentation/discovery_settings_screen.dart
//
// Phase 4: "Can people find me?" — the discoverability switch, what has been
// confirmed about the person, and a plain reminder that being findable
// shares no location (that is the separate "Who can see where I live?").
// Badges name the fact and who confirmed it; nothing says "safe".

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_states.dart';
import '../application/discovery_settings_notifier.dart';
import '../data/discovery_settings_repository.dart';

class DiscoverySettingsScreen extends ConsumerWidget {
  const DiscoverySettingsScreen({super.key, required this.apiClient});

  final ApiClient apiClient;

  static const _facts = [
    'EMAIL_VERIFIED',
    'PHONE_VERIFIED',
    'ADDRESS_VERIFIED',
    'ORGANIZATION_VERIFIED',
  ];

  Future<void> _toggle(BuildContext context, WidgetRef ref, bool value) async {
    final notifier = ref.read(discoverySettingsProvider(apiClient).notifier);
    final ok = await notifier.setDiscoverable(value);
    if (!context.mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(
          ok
              ? 'discovery_settings.saved'.tr()
              : (notifier.saveErrorKey ?? 'errors.generic').tr(),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(discoverySettingsProvider(apiClient));
    final notifier = ref.read(discoverySettingsProvider(apiClient).notifier);
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(title: Text('discovery_settings.title'.tr())),
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
          loaded: (settings) => ListView(
            padding: const EdgeInsetsDirectional.all(AppSpacing.lg),
            children: [
              Card(
                child: SwitchListTile(
                  key: const ValueKey('discoverable-switch'),
                  contentPadding: const EdgeInsetsDirectional.all(
                    AppSpacing.sm,
                  ),
                  value: settings.discoverability.isDiscoverable,
                  onChanged: (v) => _toggle(context, ref, v),
                  title: Text(
                    'discovery_settings.switch_title'.tr(),
                    style: theme.textTheme.titleMedium,
                  ),
                  subtitle: Text('discovery_settings.switch_desc'.tr()),
                ),
              ),
              const SizedBox(height: AppSpacing.sm),
              Text(
                'discovery_settings.not_location'.tr(),
                style: theme.textTheme.bodyMedium,
              ),
              const SizedBox(height: AppSpacing.lg),
              Text(
                'discovery_settings.verification_title'.tr(),
                style: theme.textTheme.titleMedium,
              ),
              const SizedBox(height: AppSpacing.sm),
              for (final fact in _facts)
                _FactTile(
                  fact: fact,
                  badge: settings.verification.badges
                      .where((b) => b.fact == fact)
                      .firstOrNull,
                ),
              if (settings.verification.nextStep case final next?) ...[
                const SizedBox(height: AppSpacing.sm),
                Text(
                  'discovery_settings.next_step.$next'.tr(),
                  style: theme.textTheme.bodyMedium,
                ),
              ],
              const SizedBox(height: AppSpacing.md),
              Text(
                'discovery_settings.footnote'.tr(),
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

class _FactTile extends StatelessWidget {
  const _FactTile({required this.fact, required this.badge});

  final String fact;
  final VerificationBadge? badge;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final confirmed = badge != null;
    final org = badge?.organizationName;
    final date = badge?.verifiedAtUtc;

    return ListTile(
      contentPadding: EdgeInsets.zero,
      leading: Icon(
        confirmed ? Icons.check_circle_outline : Icons.radio_button_unchecked,
        color: confirmed
            ? theme.colorScheme.primary
            : theme.colorScheme.onSurfaceVariant,
        semanticLabel:
            (confirmed
                    ? 'discovery_settings.confirmed'
                    : 'discovery_settings.not_confirmed')
                .tr(),
      ),
      title: Text('discovery_settings.fact.$fact'.tr()),
      subtitle: confirmed
          ? Text(
              [
                if (org != null)
                  'discovery_settings.by_org'.tr(namedArgs: {'org': org}),
                if (date != null)
                  'discovery_settings.on_date'.tr(
                    namedArgs: {
                      'date': DateFormat.yMd(
                        Localizations.localeOf(context).toLanguageTag(),
                      ).format(date.toLocal()),
                    },
                  ),
              ].join(' · '),
            )
          : Text('discovery_settings.not_confirmed'.tr()),
    );
  }
}
