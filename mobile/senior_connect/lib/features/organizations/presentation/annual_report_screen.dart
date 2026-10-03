// lib/features/organizations/presentation/annual_report_screen.dart

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design_system/app_colors.dart';
import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_states.dart';
import '../application/annual_report_notifier.dart';
import '../data/annual_report_model.dart';

/// Coordinator Annual Statistics Screen ("Die Organisation in Zahlen")
/// Displays the 6 official KPI cards matching a standard organization annual impact report:
/// 1. Freiwillige
/// 2. Personen im Freiwilligenpool
/// 3. Vernetzungspartner:innen
/// 4. Vermittlungen
/// 5. Versicherte
/// 6. Veranstaltungen & Projekte
class AnnualReportScreen extends ConsumerWidget {
  final String organizationId;
  final ApiClient apiClient;
  final int? initialYear;

  const AnnualReportScreen({
    super.key,
    required this.organizationId,
    required this.apiClient,
    this.initialYear,
  });

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final params = AnnualReportParams(
      organizationId: organizationId,
      apiClient: apiClient,
      year: initialYear ?? 2025,
    );

    final state = ref.watch(annualReportProvider(params));
    final notifier = ref.read(annualReportProvider(params).notifier);
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(title: Text('annual_report.title'.tr())),
      body: SafeArea(
        child: state.isLoading
            ? AppLoading(message: 'common.loading'.tr())
            : state.errorKey != null
            ? AppErrorView(
                message: state.errorKey!.tr(),
                retryLabel: 'common.retry'.tr(),
                onRetry: () => notifier.load(state.selectedYear),
              )
            : _buildContent(context, state, notifier, theme),
      ),
    );
  }

  Widget _buildContent(
    BuildContext context,
    AnnualReportState state,
    AnnualReportNotifier notifier,
    ThemeData theme,
  ) {
    final report = state.report;
    if (report == null) {
      return const SizedBox.shrink();
    }

    return SingleChildScrollView(
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          // Header Card with Organization & Year Selection
          _buildHeaderCard(context, report, state, notifier, theme),
          const SizedBox(height: AppSpacing.md),

          // 6 Key Statistic Cards (2-column layout or responsive wrap)
          LayoutBuilder(
            builder: (context, constraints) {
              final isWide = constraints.maxWidth > 500;
              return Wrap(
                spacing: AppSpacing.md,
                runSpacing: AppSpacing.md,
                children: [
                  _buildStatCard(
                    context: context,
                    icon: Icons.people_alt_rounded,
                    color: Theme.of(context).colorScheme.tertiary,
                    metric: report.totalVolunteers,
                    label: 'annual_report.volunteers'.tr(),
                    isWide: isWide,
                  ),
                  _buildStatCard(
                    context: context,
                    icon: Icons.groups_rounded,
                    color: context.appColors.warning,
                    metric: report.volunteerPool,
                    label: 'annual_report.volunteer_pool'.tr(),
                    isWide: isWide,
                  ),
                  _buildStatCard(
                    context: context,
                    icon: Icons.handshake_rounded,
                    color: Theme.of(context).colorScheme.primary,
                    metric: report.networkPartners,
                    label: 'annual_report.network_partners'.tr(),
                    isWide: isWide,
                  ),
                  _buildStatCard(
                    context: context,
                    icon: Icons.assignment_turned_in_rounded,
                    color: Theme.of(context).colorScheme.secondary,
                    metric: report.placements,
                    label: 'annual_report.placements'.tr(),
                    isWide: isWide,
                  ),
                  _buildStatCard(
                    context: context,
                    icon: Icons.umbrella_rounded,
                    color: context.appColors.success,
                    metric: report.insuredPersons,
                    label: 'annual_report.insured'.tr(),
                    isWide: isWide,
                  ),
                  _buildStatCard(
                    context: context,
                    icon: Icons.event_available_rounded,
                    color: context.appColors.trustOrganization,
                    metric: report.eventsAndProjects,
                    label: 'annual_report.events_and_projects'.tr(),
                    isWide: isWide,
                  ),
                ],
              );
            },
          ),
          const SizedBox(height: AppSpacing.md),

          // Total Hours Summary Banner
          Card(
            elevation: 0,
            color: theme.colorScheme.surfaceContainerHighest.withValues(
              alpha: 0.5,
            ),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(AppRadius.md),
              side: BorderSide(
                color: theme.colorScheme.outlineVariant.withValues(alpha: 0.4),
              ),
            ),
            child: Padding(
              padding: const EdgeInsets.symmetric(
                horizontal: AppSpacing.md,
                vertical: AppSpacing.sm,
              ),
              child: Row(
                children: [
                  Icon(
                    Icons.access_time_rounded,
                    color: theme.colorScheme.primary,
                    size: 22,
                  ),
                  const SizedBox(width: AppSpacing.sm),
                  Expanded(
                    child: Text(
                      'annual_report.total_hours_label'.tr(
                        args: [report.totalHours.toStringAsFixed(1)],
                      ),
                      style: theme.textTheme.bodyMedium?.copyWith(
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: AppSpacing.lg),

          // Export PDF Button
          AppButton(
            label: 'annual_report.export_pdf'.tr(),
            icon: Icons.picture_as_pdf_outlined,
            isLoading: state.isExportingPdf,
            onPressed: () {
              ScaffoldMessenger.of(context).showSnackBar(
                SnackBar(content: Text('annual_report.export_ready'.tr())),
              );
            },
          ),
        ],
      ),
    );
  }

  Widget _buildHeaderCard(
    BuildContext context,
    AnnualStatisticsReport report,
    AnnualReportState state,
    AnnualReportNotifier notifier,
    ThemeData theme,
  ) {
    return Card(
      elevation: 0,
      color: theme.colorScheme.primaryContainer.withValues(alpha: 0.25),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.lg),
        side: BorderSide(
          color: theme.colorScheme.primary.withValues(alpha: 0.3),
        ),
      ),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.center,
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (report.organizationName != null) ...[
                    Text(
                      report.organizationName!,
                      style: theme.textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    const SizedBox(height: AppSpacing.xs),
                  ],
                  Text(
                    'annual_report.subtitle'.tr(args: [report.year.toString()]),
                    style: theme.textTheme.bodyMedium?.copyWith(
                      color: theme.colorScheme.onSurfaceVariant,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: AppSpacing.sm),
            DropdownButton<int>(
              value: state.selectedYear,
              underline: const SizedBox.shrink(),
              items: [2026, 2025, 2024, 2023]
                  .map(
                    (y) => DropdownMenuItem(
                      value: y,
                      child: Text(
                        y.toString(),
                        style: const TextStyle(fontWeight: FontWeight.bold),
                      ),
                    ),
                  )
                  .toList(),
              onChanged: (year) {
                if (year != null) {
                  notifier.setYear(year);
                }
              },
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildStatCard({
    required BuildContext context,
    required IconData icon,
    required Color color,
    required MetricWithGrowth? metric,
    required String label,
    required bool isWide,
  }) {
    final theme = Theme.of(context);
    final width = isWide ? 220.0 : (MediaQuery.of(context).size.width - 48) / 2;

    return SizedBox(
      width: width,
      child: Card(
        elevation: 0,
        color: theme.colorScheme.surfaceContainerHighest.withValues(alpha: 0.4),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppRadius.lg),
          side: BorderSide(
            color: theme.colorScheme.outlineVariant.withValues(alpha: 0.5),
          ),
        ),
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.center,
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              // Icon Badge
              Container(
                width: 48,
                height: 48,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: color.withValues(alpha: 0.15),
                ),
                child: Icon(icon, color: color, size: 26),
              ),
              const SizedBox(height: AppSpacing.sm),

              // Large Number
              // null = not recorded by the platform; never show a made-up 0.
              Text(
                metric?.currentValue.toString() ??
                    'annual_report.not_recorded'.tr(),
                textAlign: TextAlign.center,
                style:
                    (metric == null
                            ? theme.textTheme.titleMedium
                            : theme.textTheme.headlineMedium)
                        ?.copyWith(
                          fontWeight: FontWeight.w900,
                          letterSpacing: -0.5,
                        ),
              ),
              const SizedBox(height: AppSpacing.xs),

              // Metric Label
              Text(
                label,
                textAlign: TextAlign.center,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: theme.textTheme.bodyMedium?.copyWith(
                  fontWeight: FontWeight.w600,
                ),
              ),
              const SizedBox(height: AppSpacing.xs),

              // Growth Badge (e.g. "+64 (2025)")
              if (metric != null)
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: AppSpacing.sm,
                    vertical: 2,
                  ),
                  decoration: BoxDecoration(
                    borderRadius: BorderRadius.circular(AppRadius.full),
                    color: theme.colorScheme.primaryContainer.withValues(
                      alpha: 0.7,
                    ),
                  ),
                  child: Text(
                    metric.formattedGrowth,
                    style: theme.textTheme.labelSmall?.copyWith(
                      fontWeight: FontWeight.bold,
                      color: theme.colorScheme.onPrimaryContainer,
                    ),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}
