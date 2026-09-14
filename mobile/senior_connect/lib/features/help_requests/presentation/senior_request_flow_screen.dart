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
import 'package:go_router/go_router.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../core/router/app_router.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_states.dart';

enum _RequestStep {
  loadingCategories,
  categoriesFailed,
  selectCategory,
  selectTiming,
  addDetails,
  reviewAndConfirm,
  submittedSuccess,
  blockedReferral,
}

class HelpCategoryItem {
  const HelpCategoryItem({
    required this.id,
    required this.titleKey,
    required this.icon,
  });

  final String id;
  final String titleKey;
  final IconData icon;
}

class SeniorRequestFlowScreen extends StatefulWidget {
  const SeniorRequestFlowScreen({
    super.key,
    required this.apiClient,
    this.initialCategories,
  });

  final ApiClient apiClient;
  final List<Map<String, dynamic>>? initialCategories;

  @override
  State<SeniorRequestFlowScreen> createState() => _SeniorRequestFlowScreenState();
}

class _SeniorRequestFlowScreenState extends State<SeniorRequestFlowScreen> {
  _RequestStep _currentStep = _RequestStep.loadingCategories;

  HelpCategoryItem? _selectedCategory;
  String _selectedTiming = 'today';
  final _detailsController = TextEditingController();
  bool _isSubmitting = false;
  String? _errorMessage;

  // P3-21 fix: real category ids fetched from the backend, never a
  // hardcoded GUID — the server-side blocked-category/emergency routing
  // (BR-SCOPE-02/03) depends entirely on the correct category being sent.
  final Map<String, String> _categoryIdsByCode = {};
  final Map<String, bool> _categoryBlockedByCode = {};

  @override
  void initState() {
    super.initState();
    _loadCategories();
  }

  Future<void> _loadCategories() async {
    if (widget.initialCategories != null) {
      _categoryIdsByCode.clear();
      _categoryBlockedByCode.clear();
      for (final item in widget.initialCategories!) {
        final code = item['code'] as String?;
        final id = item['id'] as String?;
        if (code == null || id == null) continue;
        _categoryIdsByCode[code] = id;
        _categoryBlockedByCode[code] = item['isBlocked'] as bool? ?? false;
      }
      setState(() => _currentStep = _RequestStep.selectCategory);
      return;
    }

    setState(() => _currentStep = _RequestStep.loadingCategories);

    try {
      final data = await widget.apiClient.get<dynamic>(
        '/api/v1/activities/categories',
      );

      if (data is! List) {
        throw const FormatException('Unexpected categories response shape');
      }

      _categoryIdsByCode.clear();
      _categoryBlockedByCode.clear();
      for (final item in data) {
        final m = item as Map<String, dynamic>;
        final code = m['code'] as String?;
        final id = m['id'] as String?;
        if (code == null || id == null) continue;
        _categoryIdsByCode[code] = id;
        _categoryBlockedByCode[code] = m['isBlocked'] as bool? ?? false;
      }

      if (mounted) {
        setState(() => _currentStep = _RequestStep.selectCategory);
      }
    } catch (_) {
      if (mounted) {
        setState(() => _currentStep = _RequestStep.categoriesFailed);
      }
    }
  }

  static const List<HelpCategoryItem> _categories = [
    HelpCategoryItem(
      id: 'shopping',
      titleKey: 'help.category.shopping',
      icon: Icons.shopping_cart_outlined,
    ),
    HelpCategoryItem(
      id: 'doctor',
      titleKey: 'help.category.doctor',
      icon: Icons.local_hospital_outlined,
    ),
    HelpCategoryItem(
      id: 'authority',
      titleKey: 'help.category.authority',
      icon: Icons.account_balance_outlined,
    ),
    HelpCategoryItem(
      id: 'accompaniment',
      titleKey: 'help.category.accompaniment',
      icon: Icons.directions_walk_outlined,
    ),
    HelpCategoryItem(
      id: 'home_small',
      titleKey: 'help.category.home_small',
      icon: Icons.home_repair_service_outlined,
    ),
    HelpCategoryItem(
      id: 'language_practice',
      titleKey: 'help.category.language_practice',
      icon: Icons.translate_outlined,
    ),
    HelpCategoryItem(
      id: 'newcomer_orientation',
      titleKey: 'help.category.newcomer_orientation',
      icon: Icons.explore_outlined,
    ),
    HelpCategoryItem(
      id: 'mentoring',
      titleKey: 'help.category.mentoring',
      icon: Icons.school_outlined,
    ),
    // No 'other' card: every offered category must resolve to a real
    // backend ActivityCategory id (see _loadCategories) — a catch-all card
    // that silently sent the wrong category id was exactly the P3-21 bug
    // the Gate 3 audit found.
  ];

  @override
  void dispose() {
    _detailsController.dispose();
    super.dispose();
  }

  // Detects emergency keywords to divert to Emergency screen (BR-SCOPE-04)
  bool _checkEmergencyKeywords(String text) {
    final lower = text.toLowerCase();
    const emergencyWords = [
      'notfall', 'schmerz', 'sturz', 'gefallen', 'atemnot', 'blut',
      'bewusstlos', '144', '112', 'herzinfarkt', 'schlaganfall', 'emergency'
    ];
    return emergencyWords.any((word) => lower.contains(word));
  }

  void _onCategorySelected(HelpCategoryItem category) {
    // BR-SCOPE-02/03: a blocked category never becomes a HelpRequest —
    // route to the referral step instead of the normal flow.
    if (_categoryBlockedByCode[category.id] ?? false) {
      setState(() {
        _selectedCategory = category;
        _currentStep = _RequestStep.blockedReferral;
      });
      return;
    }

    setState(() {
      _selectedCategory = category;
      _currentStep = _RequestStep.selectTiming;
    });
  }

  void _onTimingSelected(String timingKey) {
    setState(() {
      _selectedTiming = timingKey;
      _currentStep = _RequestStep.addDetails;
    });
  }

  void _onDetailsSubmitted() {
    final note = _detailsController.text.trim();
    if (_checkEmergencyKeywords(note)) {
      _showEmergencyAlert();
      return;
    }

    setState(() {
      _currentStep = _RequestStep.reviewAndConfirm;
    });
  }

  void _showEmergencyAlert() {
    showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (ctx) => AlertDialog(
        icon: Icon(Icons.warning, color: Theme.of(ctx).colorScheme.error, size: 48),
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

  Future<void> _submitRequest() async {
    final categoryId = _categoryIdsByCode[_selectedCategory?.id];
    if (categoryId == null) {
      setState(() => _errorMessage = 'errors.generic'.tr());
      return;
    }

    setState(() {
      _isSubmitting = true;
      _errorMessage = null;
    });

    try {
      final now = DateTime.now().toUtc();
      DateTime scheduledStart = now.add(const Duration(hours: 2));
      if (_selectedTiming == 'tomorrow') {
        scheduledStart = now.add(const Duration(days: 1));
      } else if (_selectedTiming == 'this_week') {
        scheduledStart = now.add(const Duration(days: 3));
      }

      final payload = {
        'categoryId': categoryId,
        'scheduledStartUtc': scheduledStart.toIso8601String(),
        'scheduledEndUtc': scheduledStart.add(const Duration(hours: 2)).toIso8601String(),
        'durationMinutes': 60,
        'locationType': 0, // InPersonHome
        'notes': _detailsController.text.trim().isEmpty ? null : _detailsController.text.trim(),
      };

      await widget.apiClient.post<dynamic>(
        '/api/v1/help-requests',
        data: payload,
      );

      if (mounted) {
        setState(() {
          _isSubmitting = false;
          _currentStep = _RequestStep.submittedSuccess;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _isSubmitting = false;
          _errorMessage = 'errors.generic'.tr();
        });
      }
    }
  }

  void _onBackPressed() {
    setState(() {
      switch (_currentStep) {
        case _RequestStep.loadingCategories:
        case _RequestStep.categoriesFailed:
        case _RequestStep.selectCategory:
          context.pop();
          break;
        case _RequestStep.selectTiming:
          _currentStep = _RequestStep.selectCategory;
          break;
        case _RequestStep.addDetails:
          _currentStep = _RequestStep.selectTiming;
          break;
        case _RequestStep.reviewAndConfirm:
          _currentStep = _RequestStep.addDetails;
          break;
        case _RequestStep.submittedSuccess:
        case _RequestStep.blockedReferral:
          context.pop();
          break;
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: Text('help.create.title'.tr()),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          tooltip: 'semantic.back_button'.tr(),
          onPressed: _onBackPressed,
        ),
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 560),
            child: SingleChildScrollView(
              padding: const EdgeInsetsDirectional.all(AppSpacing.lg),
              child: _buildCurrentStep(theme),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildCurrentStep(ThemeData theme) {
    switch (_currentStep) {
      case _RequestStep.loadingCategories:
        return AppLoading(message: 'common.loading'.tr());
      case _RequestStep.categoriesFailed:
        return AppErrorView(
          message: 'errors.generic'.tr(),
          retryLabel: 'common.retry'.tr(),
          onRetry: _loadCategories,
        );
      case _RequestStep.selectCategory:
        return _buildCategoryStep(theme);
      case _RequestStep.selectTiming:
        return _buildTimingStep(theme);
      case _RequestStep.addDetails:
        return _buildDetailsStep(theme);
      case _RequestStep.reviewAndConfirm:
        return _buildReviewStep(theme);
      case _RequestStep.submittedSuccess:
        return _buildSuccessStep(theme);
      case _RequestStep.blockedReferral:
        return _buildBlockedReferralStep(theme);
    }
  }

  Widget _buildCategoryStep(ThemeData theme) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          'help.create.what'.tr(),
          style: theme.textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: AppSpacing.lg),
        for (final cat in _categories) ...[
          _CategoryCard(
            item: cat,
            onTap: () => _onCategorySelected(cat),
          ),
          const SizedBox(height: AppSpacing.md),
        ],
      ],
    );
  }

  Widget _buildTimingStep(ThemeData theme) {
    final timings = [
      {'key': 'today', 'label': 'common.today'.tr(), 'icon': Icons.today},
      {'key': 'tomorrow', 'label': 'common.tomorrow'.tr(), 'icon': Icons.event},
      {'key': 'this_week', 'label': 'common.this_week'.tr(), 'icon': Icons.date_range},
    ];

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          'help.create.when'.tr(),
          style: theme.textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: AppSpacing.lg),
        for (final item in timings) ...[
          _TimingCard(
            label: item['label']! as String,
            icon: item['icon']! as IconData,
            onTap: () => _onTimingSelected(item['key']! as String),
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
          style: theme.textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.bold),
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

  Widget _buildReviewStep(ThemeData theme) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          'help.create.review_title'.tr(),
          style: theme.textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.bold),
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
                  icon: _selectedCategory?.icon ?? Icons.help,
                  label: _selectedCategory?.titleKey.tr() ?? '',
                ),
                const Divider(height: AppSpacing.lg),
                _ReviewRow(
                  icon: Icons.access_time,
                  label: _selectedTiming == 'today'
                      ? 'common.today'.tr()
                      : _selectedTiming == 'tomorrow'
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
        if (_errorMessage != null) ...[
          Text(
            _errorMessage!,
            style: TextStyle(color: theme.colorScheme.error),
          ),
          const SizedBox(height: AppSpacing.md),
        ],
        AppButton(
          label: 'help.create.submit'.tr(),
          icon: Icons.check_circle_outline,
          isLoading: _isSubmitting,
          onPressed: _submitRequest,
        ),
      ],
    );
  }

  Widget _buildSuccessStep(ThemeData theme) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: [
        const SizedBox(height: AppSpacing.xxl),
        Icon(Icons.check_circle, size: 80, color: theme.colorScheme.primary),
        const SizedBox(height: AppSpacing.lg),
        Text(
          'help.create.submitted'.tr(),
          style: theme.textTheme.headlineMedium?.copyWith(fontWeight: FontWeight.bold),
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: AppSpacing.md),
        Text(
          'help.create.submitted_detail'.tr(),
          style: theme.textTheme.bodyLarge?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: AppSpacing.xxl),
        AppButton(
          label: 'common.close'.tr(),
          onPressed: () => context.go(AppRoutes.home),
        ),
      ],
    );
  }

  Widget _buildBlockedReferralStep(ThemeData theme) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Icon(Icons.health_and_safety, size: 64, color: theme.colorScheme.error),
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
          onPressed: () {
            setState(() {
              _currentStep = _RequestStep.selectCategory;
            });
          },
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
      shape: BorderSide(color: theme.colorScheme.outlineVariant) == BorderSide.none
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
      shape: RoundedRectangleBorder(
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
    final theme = Theme.of(context);
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, size: 24, color: theme.colorScheme.primary),
        const SizedBox(width: AppSpacing.md),
        Expanded(
          child: Text(
            label,
            style: theme.textTheme.bodyLarge?.copyWith(fontWeight: FontWeight.w500),
          ),
        ),
      ],
    );
  }
}
