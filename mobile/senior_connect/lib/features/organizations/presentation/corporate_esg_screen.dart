import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:senior_connect/core/design_system/app_colors.dart';
import 'package:senior_connect/core/design_system/app_tokens.dart';
import 'package:senior_connect/shared/widgets/app_button.dart';

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
    required this.companyName,
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
      appBar: AppBar(title: Text('esg.title'.tr())),
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
                side: BorderSide(
                  color: theme.colorScheme.primary.withValues(alpha: 0.3),
                ),
              ),
              child: Padding(
                padding: const EdgeInsets.all(AppSpacing.lg),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Icon(
                          Icons.business,
                          color: theme.colorScheme.primary,
                          size: 28,
                        ),
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
                    color: context.appColors.success,
                  ),
                ),
                const SizedBox(width: AppSpacing.md),
                Expanded(
                  child: _MetricCard(
                    title: 'esg.social_value'.tr(),
                    value: '€ ${estimatedSocialValueEur.toStringAsFixed(0)}',
                    icon: Icons.euro,
                    color: theme.colorScheme.tertiary,
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
                      'esg.sdg_title'.tr(),
                      style: theme.textTheme.titleSmall?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    _SdgRow(
                      badge: 'esg.sdg_badge'.tr(namedArgs: {'n': '3'}),
                      label: 'esg.sdg3'.tr(),
                      color: context.appColors.success,
                      onColor: context.appColors.onSuccess,
                    ),
                    const SizedBox(height: AppSpacing.xs),
                    _SdgRow(
                      badge: 'esg.sdg_badge'.tr(namedArgs: {'n': '10'}),
                      label: 'esg.sdg10'.tr(),
                      color: theme.colorScheme.tertiary,
                      onColor: theme.colorScheme.onTertiary,
                    ),
                    const SizedBox(height: AppSpacing.xs),
                    _SdgRow(
                      badge: 'esg.sdg_badge'.tr(namedArgs: {'n': '11'}),
                      label: 'esg.sdg11'.tr(),
                      color: context.appColors.warning,
                      onColor: context.appColors.onWarning,
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
              // Disabled until a real certificate download is wired up.
              onPressed: onDownloadCertificate,
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
  final Color onColor;

  const _SdgRow({
    required this.badge,
    required this.label,
    required this.color,
    required this.onColor,
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
            style: theme.textTheme.labelSmall?.copyWith(
              color: onColor,
              fontWeight: FontWeight.bold,
            ),
          ),
        ),
        const SizedBox(width: AppSpacing.sm),
        Expanded(child: Text(label, style: theme.textTheme.bodySmall)),
      ],
    );
  }
}
