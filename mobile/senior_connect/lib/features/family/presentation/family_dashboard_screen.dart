import 'package:flutter/material.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/theme/app_tokens.dart';

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
        padding: const EdgeInsets.all(AppTokens.paddingMd),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Card(
              color: AppColors.primary.withValues(alpha: 0.05),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(AppTokens.radiusMd),
                side: BorderSide(color: AppColors.primary.withValues(alpha: 0.2)),
              ),
              child: Padding(
                padding: const EdgeInsets.all(AppTokens.paddingMd),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Zugangskarte für Angehörige',
                      style: theme.textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                        color: AppColors.primary,
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
            const SizedBox(height: AppTokens.paddingLg),
            Text(
              'Verbundene Angehörige',
              style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: AppTokens.paddingSm),
            ListTile(
              leading: const CircleAvatar(child: Icon(Icons.person)),
              title: const Text('Anna Meier (Tochter)'),
              subtitle: const Text('Berechtigung: Aktivitäten einsehen & Notfallkontakt'),
              trailing: const Icon(Icons.check_circle, color: Colors.green),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(AppTokens.radiusSm),
                side: const BorderSide(color: Colors.black12),
              ),
            ),
            const SizedBox(height: AppTokens.paddingLg),
            Text(
              'Transparenz & Datenschutz (Wer hat was gesehen?)',
              style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: AppTokens.paddingSm),
            Card(
              child: ListTile(
                leading: const Icon(Icons.visibility, color: Colors.blueGrey),
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
