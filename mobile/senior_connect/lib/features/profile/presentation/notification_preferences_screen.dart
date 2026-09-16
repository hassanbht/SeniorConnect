// lib/features/profile/presentation/notification_preferences_screen.dart
//
// P7-03: Notification preferences screen for quiet hours and category opt-outs.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_states.dart';
import '../application/notification_preferences_notifier.dart';

class NotificationPreferencesScreen extends ConsumerWidget {
  const NotificationPreferencesScreen({super.key, required this.apiClient});

  final ApiClient apiClient;

  Future<void> _selectTime(
    BuildContext context,
    WidgetRef ref, {
    required bool isStart,
    required String currentTime,
    required bool quietHoursEnabled,
    required String otherTime,
  }) async {
    final parts = currentTime.split(':');
    final initial = TimeOfDay(
      hour: int.tryParse(parts[0]) ?? (isStart ? 20 : 8),
      minute: parts.length > 1 ? int.tryParse(parts[1]) ?? 0 : 0,
    );

    final picked = await showTimePicker(
      context: context,
      initialTime: initial,
    );

    if (picked == null || !context.mounted) return;

    final formatted =
        '${picked.hour.toString().padLeft(2, '0')}:${picked.minute.toString().padLeft(2, '0')}:00';

    final start = isStart ? formatted : otherTime;
    final end = isStart ? otherTime : formatted;

    final success = await ref
        .read(notificationPreferencesProvider(apiClient).notifier)
        .updateQuietHours(
          enabled: quietHoursEnabled,
          start: start,
          end: end,
        );

    if (context.mounted && success) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('notifications.saved'.tr())),
      );
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;
    final state = ref.watch(notificationPreferencesProvider(apiClient));
    final notifier =
        ref.read(notificationPreferencesProvider(apiClient).notifier);

    return Scaffold(
      appBar: AppBar(
        title: Text('notifications.title'.tr()),
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
        loaded: (prefs) => ListView(
          padding: const EdgeInsets.all(AppSpacing.lg),
          children: [
            // Safety banner notice (BR-SAFE-04)
            Container(
              padding: const EdgeInsets.all(AppSpacing.md),
              decoration: BoxDecoration(
                color: colorScheme.primaryContainer.withValues(alpha: 0.3),
                borderRadius: BorderRadius.circular(AppRadius.md),
                border: Border.all(
                  color: colorScheme.primary.withValues(alpha: 0.5),
                ),
              ),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(Icons.info_outline, color: colorScheme.primary),
                  const SizedBox(width: AppSpacing.sm),
                  Expanded(
                    child: Text(
                      'notifications.safety_notice'.tr(),
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: colorScheme.onSurface,
                      ),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: AppSpacing.lg),

            // Quiet Hours Section
            Text(
              'notifications.quiet_hours_title'.tr(),
              style: theme.textTheme.titleMedium?.copyWith(
                fontWeight: FontWeight.bold,
                color: colorScheme.primary,
              ),
            ),
            const SizedBox(height: AppSpacing.xs),
            Text(
              'notifications.quiet_hours_desc'.tr(),
              style: theme.textTheme.bodySmall?.copyWith(
                color: colorScheme.onSurfaceVariant,
              ),
            ),
            const SizedBox(height: AppSpacing.sm),

            Card(
              elevation: 0,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(AppRadius.md),
                side: BorderSide(color: colorScheme.outlineVariant),
              ),
              child: Column(
                children: [
                  SwitchListTile(
                    title: Text(
                      'notifications.quiet_hours_enabled'.tr(),
                      style: theme.textTheme.bodyLarge?.copyWith(
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    value: prefs.quietHoursEnabled,
                    onChanged: (val) {
                      notifier.updateQuietHours(
                        enabled: val,
                        start: prefs.quietHoursStart,
                        end: prefs.quietHoursEnd,
                      );
                    },
                  ),
                  if (prefs.quietHoursEnabled) ...[
                    const Divider(height: 1),
                    ListTile(
                      leading: const Icon(Icons.bedtime_outlined),
                      title: Text('notifications.quiet_hours_start'.tr()),
                      trailing: Text(
                        prefs.quietHoursStart.substring(0, 5),
                        style: theme.textTheme.titleMedium?.copyWith(
                          color: colorScheme.primary,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      onTap: () => _selectTime(
                        context,
                        ref,
                        isStart: true,
                        currentTime: prefs.quietHoursStart,
                        quietHoursEnabled: prefs.quietHoursEnabled,
                        otherTime: prefs.quietHoursEnd,
                      ),
                    ),
                    const Divider(height: 1),
                    ListTile(
                      leading: const Icon(Icons.wb_sunny_outlined),
                      title: Text('notifications.quiet_hours_end'.tr()),
                      trailing: Text(
                        prefs.quietHoursEnd.substring(0, 5),
                        style: theme.textTheme.titleMedium?.copyWith(
                          color: colorScheme.primary,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      onTap: () => _selectTime(
                        context,
                        ref,
                        isStart: false,
                        currentTime: prefs.quietHoursEnd,
                        quietHoursEnabled: prefs.quietHoursEnabled,
                        otherTime: prefs.quietHoursStart,
                      ),
                    ),
                  ],
                ],
              ),
            ),
            const SizedBox(height: AppSpacing.xl),

            // Notification Categories Section
            Text(
              'notifications.categories_title'.tr(),
              style: theme.textTheme.titleMedium?.copyWith(
                fontWeight: FontWeight.bold,
                color: colorScheme.primary,
              ),
            ),
            const SizedBox(height: AppSpacing.sm),

            Card(
              elevation: 0,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(AppRadius.md),
                side: BorderSide(color: colorScheme.outlineVariant),
              ),
              child: Column(
                children: [
                  SwitchListTile(
                    secondary: const Icon(Icons.handshake_outlined),
                    title: Text('notifications.cat_help_requests'.tr()),
                    value: prefs.helpRequestsCategoryEnabled,
                    onChanged: (val) => notifier.toggleCategory(
                      categoryKey: 'help_requests',
                      enabled: val,
                    ),
                  ),
                  const Divider(height: 1),
                  SwitchListTile(
                    secondary: const Icon(Icons.groups_outlined),
                    title: Text('notifications.cat_community'.tr()),
                    value: prefs.communityCategoryEnabled,
                    onChanged: (val) => notifier.toggleCategory(
                      categoryKey: 'community',
                      enabled: val,
                    ),
                  ),
                  const Divider(height: 1),
                  SwitchListTile(
                    secondary: const Icon(Icons.family_restroom_outlined),
                    title: Text('notifications.cat_family'.tr()),
                    value: prefs.familyWelfareCategoryEnabled,
                    onChanged: (val) => notifier.toggleCategory(
                      categoryKey: 'family_welfare',
                      enabled: val,
                    ),
                  ),
                  const Divider(height: 1),
                  SwitchListTile(
                    secondary: const Icon(Icons.settings_suggest_outlined),
                    title: Text('notifications.cat_system'.tr()),
                    value: prefs.systemAccountCategoryEnabled,
                    onChanged: (val) => notifier.toggleCategory(
                      categoryKey: 'system_account',
                      enabled: val,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
