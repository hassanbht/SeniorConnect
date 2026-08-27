import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:senior_connect/core/design_system/app_tokens.dart';
import 'package:senior_connect/core/design_system/widgets/app_button.dart';

/// Corporate Volunteering & ESG Impact dashboard screen (P8-07).
/// Displays employee engagement metrics, total donated volunteer hours,
/// and download link for the official CSR/ESG certificate.
class CorporateEsgScreen extends StatelessWidget {
  final String companyName;
  final double totalHours;
  final int participatingEmployees;
  final int beneficiariesSupported;
  final double estimatedSocialValueEur;
  final VoidCallback? onDownloadCertificate;

  const CorporateEsgScreen({
    super.key,
    this.companyName = 'Corporate Volunteering Partner',
    this.totalHours = 142.5,
    this.participatingEmployees = 28,
    this.beneficiariesSupported = 45,
    this.estimatedSocialValueEur = 4987.50,
    this.onDownloadCertificate,
  });

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: Text('esg.title'.tr()),
      ),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(AppSpacing.md),
          children: [
            // Header Banner
            Card(
              elevation: 0,
              color: theme.colorScheme.primaryContainer.withValues(alpha: 0.3),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(AppRadius.lg),
                side: BorderSide(color: theme.colorScheme.primary.withValues(alpha: 0.3)),
              ),
              child: Padding(
                padding: const EdgeInsets.all(AppSpacing.lg),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Icon(Icons.business, color: theme.colorScheme.primary, size: 28),
                        const SizedBox(width: AppSpacing.sm),
                        Expanded(
                          child: Text(
                            companyName,
                            style: theme.textTheme.titleMedium?.copyWith(
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: AppSpacing.xs),
                    Text(
                      'esg.subtitle'.tr(),
                      style: theme.textTheme.bodyMedium?.copyWith(
                        color: theme.colorScheme.onSurfaceVariant,
                      ),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: AppSpacing.lg),

            // Key KPI Metric Cards
            Row(
              children: [
                Expanded(
                  child: _MetricCard(
                    title: 'esg.total_hours'.tr(),
                    value: '${totalHours.toStringAsFixed(1)} h',
                    icon: Icons.timer,
                    color: theme.colorScheme.primary,
                  ),
                ),
                const SizedBox(width: AppSpacing.md),
                Expanded(
                  child: _MetricCard(
                    title: 'esg.participating_employees'.tr(),
                    value: participatingEmployees.toString(),
                    icon: Icons.people,
                    color: theme.colorScheme.secondary,
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.md),
            Row(
              children: [
                Expanded(
                  child: _MetricCard(
                    title: 'esg.beneficiaries_supported'.tr(),
                    value: beneficiariesSupported.toString(),
                    icon: Icons.favorite,
                    color: Colors.teal,
                  ),
                ),
                const SizedBox(width: AppSpacing.md),
                Expanded(
                  child: _MetricCard(
                    title: 'Sozialer Mehrwert',
                    value: '€ ${estimatedSocialValueEur.toStringAsFixed(0)}',
                    icon: Icons.euro,
                    color: Colors.deepPurple,
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.lg),

            // SDG Contribution Card
            Card(
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
                      'UN Sustainable Development Goals (SDGs)',
                      style: theme.textTheme.titleSmall?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    _SdgRow(
                      badge: 'SDG 3',
                      label: 'Gesundheit und Wohlergehen im Alter',
                      color: Colors.green.shade700,
                    ),
                    const SizedBox(height: AppSpacing.xs),
                    _SdgRow(
                      badge: 'SDG 10',
                      label: 'Reduktion sozialer Isolation und Inklusion',
                      color: Colors.pink.shade700,
                    ),
                    const SizedBox(height: AppSpacing.xs),
                    _SdgRow(
                      badge: 'SDG 11',
                      label: 'Nachhaltige Städte und lebendige Nachbarschaften',
                      color: Colors.orange.shade800,
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: AppSpacing.xl),

            // Download Certificate Button
            AppButton(
              label: 'esg.download_certificate'.tr(),
              icon: Icons.picture_as_pdf,
              variant: AppButtonVariant.primary,
              onPressed: () {
                if (onDownloadCertificate != null) {
                  onDownloadCertificate!();
                } else {
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(content: Text('ESG-Zertifikat heruntergeladen.')),
                  );
                }
              },
            ),
          ],
        ),
      ),
    );
  }
}

class _MetricCard extends StatelessWidget {
  final String title;
  final String value;
  final IconData icon;
  final Color color;

  const _MetricCard({
    required this.title,
    required this.value,
    required this.icon,
    required this.color,
  });

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Card(
      elevation: 0,
      color: theme.colorScheme.surfaceContainerLow,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.md),
        side: BorderSide(color: theme.colorScheme.outlineVariant),
      ),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(icon, color: color, size: 24),
            const SizedBox(height: AppSpacing.sm),
            Text(
              value,
              style: theme.textTheme.headlineSmall?.copyWith(
                fontWeight: FontWeight.bold,
                color: theme.colorScheme.onSurface,
              ),
            ),
            const SizedBox(height: AppSpacing.xxs),
            Text(
              title,
              style: theme.textTheme.bodySmall?.copyWith(
                color: theme.colorScheme.onSurfaceVariant,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _SdgRow extends StatelessWidget {
  final String badge;
  final String label;
  final Color color;

  const _SdgRow({
    required this.badge,
    required this.label,
    required this.color,
  });

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Row(
      children: [
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
          decoration: BoxDecoration(
            color: color,
            borderRadius: BorderRadius.circular(4),
          ),
          child: Text(
            badge,
            style: const TextStyle(
              color: Colors.white,
              fontWeight: FontWeight.bold,
              fontSize: 11,
            ),
          ),
        ),
        const SizedBox(width: AppSpacing.sm),
        Expanded(
          child: Text(
            label,
            style: theme.textTheme.bodySmall,
          ),
        ),
      ],
    );
  }
}
