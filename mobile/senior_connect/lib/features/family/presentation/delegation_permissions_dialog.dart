// lib/features/family/presentation/delegation_permissions_dialog.dart
//
// P6-02 / BR-FAMILY-01..04: Granular, revocable permissions dialog.
// Allows seniors or legal guardians to toggle permissions for connected family members.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../shared/widgets/app_button.dart';
import '../application/delegation_permissions_notifier.dart';

class DelegationPermissionsDialog extends ConsumerWidget {
  const DelegationPermissionsDialog({
    super.key,
    required this.caregiverName,
    required this.currentPermissions,
    required this.onSave,
  });

  final String caregiverName;
  final Map<String, bool> currentPermissions;
  final Future<void> Function(Map<String, bool> updated) onSave;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final permissions = ref.watch(delegationPermissionsProvider(currentPermissions));
    final notifier =
        ref.read(delegationPermissionsProvider(currentPermissions).notifier);

    return AlertDialog(
      title: Text('family.permissions_for'.tr(args: [caregiverName])),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            _buildSwitchTile(
              context,
              title: 'family.perm_view_activities'.tr(),
              subtitle: 'family.perm_view_activities_hint'.tr(),
              key: 'ViewActivities',
              value: permissions['ViewActivities'] ?? false,
              onChanged: (val) => notifier.toggle('ViewActivities', val),
            ),
            _buildSwitchTile(
              context,
              title: 'family.perm_create_requests'.tr(),
              subtitle: 'family.perm_create_requests_hint'.tr(),
              key: 'CreateHelpRequestsOnBehalf',
              value: permissions['CreateHelpRequestsOnBehalf'] ?? false,
              onChanged: (val) =>
                  notifier.toggle('CreateHelpRequestsOnBehalf', val),
            ),
            _buildSwitchTile(
              context,
              title: 'family.perm_view_emergency'.tr(),
              subtitle: 'family.perm_view_emergency_hint'.tr(),
              key: 'ViewEmergencyContacts',
              value: permissions['ViewEmergencyContacts'] ?? false,
              onChanged: (val) =>
                  notifier.toggle('ViewEmergencyContacts', val),
            ),
            _buildSwitchTile(
              context,
              title: 'family.perm_safety_alerts'.tr(),
              subtitle: 'family.perm_safety_alerts_hint'.tr(),
              key: 'ReceiveSafetyAlerts',
              value: permissions['ReceiveSafetyAlerts'] ?? false,
              onChanged: (val) =>
                  notifier.toggle('ReceiveSafetyAlerts', val),
            ),
            _buildSwitchTile(
              context,
              title: 'family.perm_manage_settings'.tr(),
              subtitle: 'family.perm_manage_settings_hint'.tr(),
              key: 'ManageSettings',
              value: permissions['ManageSettings'] ?? false,
              onChanged: (val) => notifier.toggle('ManageSettings', val),
            ),
          ],
        ),
      ),
      actions: [
        AppButton(
          label: 'common.cancel'.tr(),
          variant: AppButtonVariant.outlined,
          onPressed: () => Navigator.of(context).pop(),
        ),
        const SizedBox(height: AppSpacing.xs),
        AppButton(
          label: 'common.save'.tr(),
          variant: AppButtonVariant.primary,
          onPressed: () {
            onSave(permissions);
            Navigator.of(context).pop();
          },
        ),
      ],
    );
  }

  Widget _buildSwitchTile(
    BuildContext context, {
    required String title,
    required String subtitle,
    required String key,
    required bool value,
    required ValueChanged<bool> onChanged,
  }) {
    final theme = Theme.of(context);

    return SwitchListTile.adaptive(
      title: Text(
        title,
        style: theme.textTheme.bodyLarge?.copyWith(fontWeight: FontWeight.w600),
      ),
      subtitle: Text(subtitle, style: theme.textTheme.bodySmall),
      value: value,
      onChanged: onChanged,
    );
  }
}
