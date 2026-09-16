// lib/features/help_requests/presentation/senior_request_flow_screen.dart
//
// P3-21: Senior help request submission flow.
//
// Design and Accessibility Constraints:
// - Max 5 taps from start to submission.
// - 6 picture/icon category cards with high readability and >= 64dp touch targets.
// - Plain German review screen ("Stimmt das so?").
// - Emergency phrase detection (routes directly to P3-24 EmergencyScreen).
// - Blocked category referral support (BR-SCOPE-02/03).
// - Full RTL support and semantic labels for screen readers.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../core/router/app_router.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_states.dart';
import '../application/senior_request_flow_notifier.dart';

class SeniorRequestFlowScreen extends ConsumerStatefulWidget {
  const SeniorRequestFlowScreen({
    super.key,
    required this.apiClient,
    this.initialCategories,
    this.seniorUserId,
  });

  final ApiClient apiClient;
  final List<Map<String, dynamic>>? initialCategories;
  final String? seniorUserId;

  @override
  ConsumerState<SeniorRequestFlowScreen> createState() =>
      _SeniorRequestFlowScreenState();
}

class _SeniorRequestFlowScreenState
    extends ConsumerState<SeniorRequestFlowScreen> {
  final _detailsController = TextEditingController();

  SeniorRequestParams get _params => SeniorRequestParams(
        apiClient: widget.apiClient,
        initialCategories: widget.initialCategories,
        seniorUserId: widget.seniorUserId,
      );

  @override
  void dispose() {
    _detailsController.dispose();
    super.dispose();
  }

  bool _checkEmergencyKeywords(String text) {
    final lower = text.toLowerCase();
    const emergencyWords = [
      'notfall',
      'schmerz',
      'sturz',
      'gefallen',
      'atemnot',
      'blut',
      'bewusstlos',
      '144',
      '112',
      'herzinfarkt',
      'schlaganfall',
      'emergency'
    ];
    return emergencyWords.any((word) => lower.contains(word));
  }

  void _onDetailsSubmitted() {
    final note = _detailsController.text.trim();
    if (_checkEmergencyKeywords(note)) {
      _showEmergencyAlert();
      return;
    }

    ref.read(seniorRequestFlowProvider(_params).notifier).goToReview();
  }

  void _showEmergencyAlert() {
    showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (ctx) => AlertDialog(
        icon: Icon(
          Icons.warning,
          color: Theme.of(ctx).colorScheme.error,
          size: 48,
        ),
        title: Text('emergency.title'.tr()),
        content: Text('help.emergency_detected_msg'.tr()),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(),
            child: Text('common.back'.tr()),
          ),
          FilledButton(
            style: FilledButton.styleFrom(
              backgroundColor: Theme.of(ctx).colorScheme.error,
            ),
            onPressed: () {
              Navigator.of(ctx).pop();
              context.push(AppRoutes.emergency);
            },
            child: Text('home.senior.emergency'.tr()),
          ),
        ],
      ),
    );
  }

  void _onBackPressed(SeniorRequestFlowState state) {
    final notifier = ref.read(seniorRequestFlowProvider(_params).notifier);
    switch (state.currentStep) {
      case SeniorRequestStep.loadingCategories:
      case SeniorRequestStep.categoriesFailed:
      case SeniorRequestStep.selectCategory:
        context.pop();
        break;
      case SeniorRequestStep.selectTiming:
        notifier.goToStep(SeniorRequestStep.selectCategory);
        break;
      case SeniorRequestStep.addDetails:
        notifier.goToStep(SeniorRequestStep.selectTiming);
        break;
      case SeniorRequestStep.reviewAndConfirm:
        notifier.goToStep(SeniorRequestStep.addDetails);
        break;
      case SeniorRequestStep.submittedSuccess:
      case SeniorRequestStep.blockedReferral:
        context.pop();
        break;
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final state = ref.watch(seniorRequestFlowProvider(_params));
    final notifier = ref.read(seniorRequestFlowProvider(_params).notifier);

    return Scaffold(
      appBar: AppBar(
        title: Text('help.create.title'.tr()),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          tooltip: 'semantic.back_button'.tr(),
          onPressed: () => _onBackPressed(state),
        ),
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 560),
            child: SingleChildScrollView(
              padding: const EdgeInsetsDirectional.all(AppSpacing.lg),
              child: _buildCurrentStep(theme, state, notifier),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildCurrentStep(
    ThemeData theme,
    SeniorRequestFlowState state,
    SeniorRequestFlowNotifier notifier,
  ) {
    switch (state.currentStep) {
      case SeniorRequestStep.loadingCategories:
        return AppLoading(message: 'common.loading'.tr());
      case SeniorRequestStep.categoriesFailed:
        return AppErrorView(
          message: 'errors.generic'.tr(),
          retryLabel: 'common.retry'.tr(),
          onRetry: notifier.loadCategories,
        );
      case SeniorRequestStep.selectCategory:
        return _buildCategoryStep(theme, notifier);
      case SeniorRequestStep.selectTiming:
        return _buildTimingStep(theme, notifier);
      case SeniorRequestStep.addDetails:
        return _buildDetailsStep(theme);
      case SeniorRequestStep.reviewAndConfirm:
        return _buildReviewStep(theme, state, notifier);
      case SeniorRequestStep.submittedSuccess:
        return _buildSuccessStep(theme, state);
      case SeniorRequestStep.blockedReferral:
        return _buildBlockedReferralStep(theme, notifier);
    }
  }

  Widget _buildCategoryStep(
    ThemeData theme,
    SeniorRequestFlowNotifier notifier,
  ) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          'help.create.what'.tr(),
          style: theme.textTheme.headlineSmall
              ?.copyWith(fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: AppSpacing.lg),
        for (final cat in SeniorRequestFlowNotifier.categories) ...[
          _CategoryCard(
            item: cat,
            onTap: () => notifier.onCategorySelected(cat),
          ),
          const SizedBox(height: AppSpacing.md),
        ],
      ],
    );
  }

  Widget _buildTimingStep(
    ThemeData theme,
    SeniorRequestFlowNotifier notifier,
  ) {
    final timings = [
      {'key': 'today', 'label': 'common.today'.tr(), 'icon': Icons.today},
      {'key': 'tomorrow', 'label': 'common.tomorrow'.tr(), 'icon': Icons.event},
      {
        'key': 'this_week',
        'label': 'common.this_week'.tr(),
        'icon': Icons.date_range
      },
    ];

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          'help.create.when'.tr(),
          style: theme.textTheme.headlineSmall
              ?.copyWith(fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: AppSpacing.lg),
        for (final item in timings) ...[
          _TimingCard(
            label: item['label']! as String,
            icon: item['icon']! as IconData,
            onTap: () => notifier.onTimingSelected(item['key']! as String),
          ),
          const SizedBox(height: AppSpacing.md),
        ],
      ],
    );
  }

  Widget _buildDetailsStep(ThemeData theme) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          'help.create.details'.tr(),
          style: theme.textTheme.headlineSmall
              ?.copyWith(fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: AppSpacing.sm),
        Text(
          'help.create.details_hint'.tr(),
          style: theme.textTheme.bodyMedium?.copyWith(
            color: theme.colorScheme.onSurfaceVariant,
          ),
        ),
        const SizedBox(height: AppSpacing.lg),
        TextField(
          controller: _detailsController,
          maxLines: 4,
          style: theme.textTheme.bodyLarge,
          decoration: InputDecoration(
            hintText: 'help.create.details_hint'.tr(),
            border: const OutlineInputBorder(),
          ),
        ),
        const SizedBox(height: AppSpacing.xl),
        AppButton(
          label: 'common.next'.tr(),
          icon: Icons.arrow_forward,
          onPressed: _onDetailsSubmitted,
        ),
      ],
    );
  }

  Widget _buildReviewStep(
    ThemeData theme,
    SeniorRequestFlowState state,
    SeniorRequestFlowNotifier notifier,
  ) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          'help.create.review_title'.tr(),
          style: theme.textTheme.headlineSmall
              ?.copyWith(fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: AppSpacing.lg),
        Card(
          elevation: 0,
          color: theme.colorScheme.surfaceContainerHighest,
          shape: const RoundedRectangleBorder(
            borderRadius: AppRadius.card,
          ),
          child: Padding(
            padding: const EdgeInsetsDirectional.all(AppSpacing.lg),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _ReviewRow(
                  icon: state.selectedCategory?.icon ?? Icons.help,
                  label: state.selectedCategory?.titleKey.tr() ?? '',
                ),
                const Divider(height: AppSpacing.lg),
                _ReviewRow(
                  icon: Icons.access_time,
                  label: state.selectedTiming == 'today'
                      ? 'common.today'.tr()
                      : state.selectedTiming == 'tomorrow'
                          ? 'common.tomorrow'.tr()
                          : 'common.this_week'.tr(),
                ),
                if (_detailsController.text.trim().isNotEmpty) ...[
                  const Divider(height: AppSpacing.lg),
                  _ReviewRow(
                    icon: Icons.notes,
                    label: _detailsController.text.trim(),
                  ),
                ],
              ],
            ),
          ),
        ),
        const SizedBox(height: AppSpacing.xl),
        if (state.errorMessage != null) ...[
          Text(
            state.errorMessage!,
            style: TextStyle(color: theme.colorScheme.error),
          ),
          const SizedBox(height: AppSpacing.md),
        ],
        AppButton(
          label: 'help.create.submit'.tr(),
          icon: Icons.check_circle_outline,
          isLoading: state.isSubmitting,
          onPressed: () =>
              notifier.submitRequest(_detailsController.text.trim()),
        ),
      ],
    );
  }

  Widget _buildSuccessStep(
    ThemeData theme,
    SeniorRequestFlowState state,
  ) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: [
        const SizedBox(height: AppSpacing.xxl),
        Icon(Icons.check_circle, size: 80, color: theme.colorScheme.primary),
        const SizedBox(height: AppSpacing.lg),
        Text(
          'help.create.submitted'.tr(),
          style: theme.textTheme.headlineMedium
              ?.copyWith(fontWeight: FontWeight.bold),
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: AppSpacing.md),
        Text(
          'help.create.submitted_detail'.tr(),
          style: theme.textTheme.bodyLarge
              ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: AppSpacing.xxl),
        AppButton(
          label: state.createdRequestId != null
              ? 'help.my_request.title'.tr()
              : 'common.close'.tr(),
          onPressed: () {
            final id = state.createdRequestId;
            if (id != null) {
              context.go('${AppRoutes.myRequestStatus}/$id');
            } else {
              context.go(AppRoutes.home);
            }
          },
        ),
      ],
    );
  }

  Widget _buildBlockedReferralStep(
    ThemeData theme,
    SeniorRequestFlowNotifier notifier,
  ) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Icon(Icons.health_and_safety,
            size: 64, color: theme.colorScheme.error),
        const SizedBox(height: AppSpacing.md),
        Text(
          'help.blocked.title'.tr(),
          style: theme.textTheme.headlineSmall?.copyWith(
            fontWeight: FontWeight.bold,
            color: theme.colorScheme.error,
          ),
        ),
        const SizedBox(height: AppSpacing.sm),
        Text(
          'help.blocked.description'.tr(),
          style: theme.textTheme.bodyMedium,
        ),
        const SizedBox(height: AppSpacing.xl),
        AppButton(
          label: 'help.blocked.other_request'.tr(),
          variant: AppButtonVariant.tonal,
          onPressed: () =>
              notifier.goToStep(SeniorRequestStep.selectCategory),
        ),
      ],
    );
  }
}

class _CategoryCard extends StatelessWidget {
  const _CategoryCard({
    required this.item,
    required this.onTap,
  });

  final HelpCategoryItem item;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      elevation: 0,
      shape: BorderSide(color: theme.colorScheme.outlineVariant) ==
              BorderSide.none
          ? const RoundedRectangleBorder(borderRadius: AppRadius.card)
          : RoundedRectangleBorder(
              borderRadius: AppRadius.card,
              side: BorderSide(color: theme.colorScheme.outlineVariant),
            ),
      child: InkWell(
        borderRadius: const BorderRadius.all(Radius.circular(AppRadius.lg)),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsetsDirectional.all(AppSpacing.lg),
          child: Row(
            children: [
              Icon(item.icon, size: 36, color: theme.colorScheme.primary),
              const SizedBox(width: AppSpacing.lg),
              Expanded(
                child: Text(
                  item.titleKey.tr(),
                  style: theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),
              const Icon(Icons.chevron_right),
            ],
          ),
        ),
      ),
    );
  }
}

class _TimingCard extends StatelessWidget {
  const _TimingCard({
    required this.label,
    required this.icon,
    required this.onTap,
  });

  final String label;
  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      elevation: 0,
      shape: const RoundedRectangleBorder(borderRadius: AppRadius.card),
      color: theme.colorScheme.surfaceContainerHighest,
      child: InkWell(
        borderRadius: const BorderRadius.all(Radius.circular(AppRadius.lg)),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsetsDirectional.all(AppSpacing.lg),
          child: Row(
            children: [
              Icon(icon, size: 32, color: theme.colorScheme.primary),
              const SizedBox(width: AppSpacing.lg),
              Expanded(
                child: Text(
                  label,
                  style: theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),
              const Icon(Icons.chevron_right),
            ],
          ),
        ),
      ),
    );
  }
}

class _ReviewRow extends StatelessWidget {
  const _ReviewRow({
    required this.icon,
    required this.label,
  });

  final IconData icon;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Icon(icon, size: 24, color: Theme.of(context).colorScheme.primary),
        const SizedBox(width: AppSpacing.md),
        Expanded(
          child: Text(
            label,
            style: Theme.of(context).textTheme.bodyLarge,
          ),
        ),
      ],
    );
  }
}
