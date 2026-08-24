import 'package:flutter/material.dart';

import '../../../core/design_system/app_colors.dart';
import '../../../core/design_system/app_tokens.dart';

class FamilyDashboardScreen extends StatelessWidget {
  const FamilyDashboardScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Familie & Angehörige'),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Card(
              color: theme.colorScheme.primaryContainer,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(AppRadius.md),
                side: BorderSide(color: theme.colorScheme.outlineVariant),
              ),
              child: Padding(
                padding: const EdgeInsets.all(AppSpacing.md),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Zugangskarte für Angehörige',
                      style: theme.textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                        color: theme.colorScheme.onPrimaryContainer,
                      ),
                    ),
                    const SizedBox(height: 6),
                    Text(
                      'Verbinden Sie Ihr Profil direkt mit Ihren Angehörigen, um Unterstützung sicher zu koordinieren.',
                      style: theme.textTheme.bodyMedium,
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: AppSpacing.lg),
            Text(
              'Verbundene Angehörige',
              style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: AppSpacing.sm),
            ListTile(
              leading: const CircleAvatar(child: Icon(Icons.person)),
              title: const Text('Anna Meier (Tochter)'),
              subtitle: const Text('Berechtigung: Aktivitäten einsehen & Notfallkontakt'),
              trailing: Icon(Icons.check_circle, color: context.appColors.success),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(AppRadius.sm),
                side: BorderSide(color: theme.colorScheme.outlineVariant),
              ),
            ),
            const SizedBox(height: AppSpacing.lg),
            Text(
              'Transparenz & Datenschutz (Wer hat was gesehen?)',
              style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: AppSpacing.sm),
            Card(
              child: ListTile(
                leading: Icon(Icons.visibility, color: theme.colorScheme.onSurfaceVariant),
                title: const Text('Zugriffsprotokoll der letzten 30 Tage'),
                subtitle: const Text('Alle Einsichten Ihrer Angehörigen sind hier transparent dokumentiert.'),
                trailing: const Icon(Icons.chevron_right),
                onTap: () {},
              ),
            ),
          ],
        ),
      ),
    );
  }
}
