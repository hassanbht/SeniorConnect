// lib/features/family/presentation/delegation_permissions_dialog.dart
//
// P6-02 / BR-FAMILY-01..04: Granular, revocable permissions dialog.
// Allows seniors or legal guardians to toggle permissions for connected family members.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../shared/widgets/app_button.dart';

class DelegationPermissionsDialog extends StatefulWidget {
  const DelegationPermissionsDialog({
    super.key,
    required this.caregiverName,
    required this.currentPermissions,
    required this.onSave,
  });

  final String caregiverName;
  final Map<String, bool> currentPermissions;
  final void Function(Map<String, bool> updated) onSave;

  @override
  State<DelegationPermissionsDialog> createState() => _DelegationPermissionsDialogState();
}

class _DelegationPermissionsDialogState extends State<DelegationPermissionsDialog> {
  late Map<String, bool> _permissions;

  @override
  void initState() {
    super.initState();
    _permissions = Map<String, bool>.from(widget.currentPermissions);
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return AlertDialog(
      title: Text('family.permissions_for'.tr(args: [widget.caregiverName])),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            _buildSwitchTile(
              title: 'family.perm_view_activities'.tr(),
              subtitle: 'family.perm_view_activities_hint'.tr(),
              key: 'ViewActivities',
            ),
            _buildSwitchTile(
              title: 'family.perm_create_requests'.tr(),
              subtitle: 'family.perm_create_requests_hint'.tr(),
              key: 'CreateHelpRequestsOnBehalf',
            ),
            _buildSwitchTile(
              title: 'family.perm_view_emergency'.tr(),
              subtitle: 'family.perm_view_emergency_hint'.tr(),
              key: 'ViewEmergencyContacts',
            ),
            _buildSwitchTile(
              title: 'family.perm_safety_alerts'.tr(),
              subtitle: 'family.perm_safety_alerts_hint'.tr(),
              key: 'ReceiveSafetyAlerts',
            ),
            _buildSwitchTile(
              title: 'family.perm_manage_settings'.tr(),
              subtitle: 'family.perm_manage_settings_hint'.tr(),
              key: 'ManageSettings',
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
            widget.onSave(_permissions);
            Navigator.of(context).pop();
          },
        ),
      ],
    );
  }

  Widget _buildSwitchTile({
    required String title,
    required String subtitle,
    required String key,
  }) {
    final theme = Theme.of(context);
    final value = _permissions[key] ?? false;

    return SwitchListTile.adaptive(
      title: Text(title, style: theme.textTheme.bodyLarge?.copyWith(fontWeight: FontWeight.w600)),
      subtitle: Text(subtitle, style: theme.textTheme.bodySmall),
      value: value,
      onChanged: (newValue) {
        setState(() {
          _permissions[key] = newValue;
        });
      },
    );
  }
}
