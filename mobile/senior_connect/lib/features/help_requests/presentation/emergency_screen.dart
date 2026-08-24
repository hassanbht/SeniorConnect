// lib/features/help_requests/presentation/emergency_screen.dart
//
// P3-24: Emergency screen.
//
// Critical Safety Rules (BR-SCOPE-04, BR-SCOPE-05):
// - Direct routing to 144 (Rettung) / 112 (Euronotruf).
// - NEVER claims or implies that help is on the way.
// - Two-step confirmation for calls to prevent accidental dials.
// - Extra-large touch targets (>= 64dp/72dp) accessible in Senior Mode.
// - Full RTL support and semantic announcements.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../shared/widgets/app_button.dart';

class EmergencyScreen extends StatelessWidget {
  const EmergencyScreen({super.key});

  Future<void> _makePhoneCall(BuildContext context, String number, String serviceName) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('emergency.call_confirm_title'.tr(args: [serviceName])),
        content: Text('emergency.call_confirm_desc'.tr(args: [serviceName, number])),
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
            child: Text('emergency.call_now'.tr()),
          ),
        ],
      ),
    );

    if (confirmed == true) {
      final uri = Uri(scheme: 'tel', path: number);
      if (await canLaunchUrl(uri)) {
        await launchUrl(uri);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    return Scaffold(
      appBar: AppBar(
        title: Text('emergency.title'.tr()),
        leading: IconButton(
          icon: const Icon(Icons.close),
          tooltip: 'common.close'.tr(),
          onPressed: () => Navigator.of(context).maybePop(),
        ),
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 560),
            child: Padding(
              padding: const EdgeInsetsDirectional.all(AppSpacing.lg),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Icon(
                    Icons.emergency,
                    size: 72,
                    color: colorScheme.error,
                  ),
                  const SizedBox(height: AppSpacing.md),
                  Text(
                    'emergency.title'.tr(),
                    style: theme.textTheme.headlineMedium?.copyWith(
                      fontWeight: FontWeight.bold,
                      color: colorScheme.error,
                    ),
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: AppSpacing.sm),
                  Text(
                    'emergency.disclaimer'.tr(),
                    style: theme.textTheme.bodyLarge?.copyWith(
                      color: colorScheme.onSurfaceVariant,
                    ),
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: AppSpacing.xxl),

                  // Call 144
                  AppButton(
                    label: 'emergency.call_144'.tr(),
                    icon: Icons.phone_in_talk,
                    variant: AppButtonVariant.danger,
                    minHeight: 72,
                    semanticLabel: 'emergency.call_144_semantic'.tr(),
                    onPressed: () => _makePhoneCall(context, '144', 'Rettung (144)'),
                  ),
                  const SizedBox(height: AppSpacing.lg),

                  // Call 112
                  AppButton(
                    label: 'emergency.call_112'.tr(),
                    icon: Icons.local_hospital,
                    variant: AppButtonVariant.danger,
                    minHeight: 72,
                    semanticLabel: 'emergency.call_112_semantic'.tr(),
                    onPressed: () => _makePhoneCall(context, '112', 'Euro-Notruf (112)'),
                  ),
                  const SizedBox(height: AppSpacing.xl),

                  // Return / Not an emergency
                  OutlinedButton(
                    style: OutlinedButton.styleFrom(
                      minimumSize: const Size.fromHeight(60),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(AppRadius.button),
                      ),
                    ),
                    onPressed: () => Navigator.of(context).maybePop(),
                    child: Text(
                      'emergency.no'.tr(),
                      style: theme.textTheme.titleMedium?.copyWith(
                        color: colorScheme.onSurface,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
