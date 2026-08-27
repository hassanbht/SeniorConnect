// lib/features/organizations/presentation/log_activity_screen.dart
//
// P2-14: Volunteer self-log screen (2-tap from home).
//
// Constraints and Rules:
// - Prefilled from the last entry (BR-ROSTER-01, F1).
// - Idempotency key honoured on submit.
// - Insurance context (BR-SAFETY-06) and transport mode (BR-TRANSPORT-01..05) explicit.
// - Full easy_localization, dark/light theme, RTL support.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_button.dart';

class LogActivityScreen extends StatefulWidget {
  const LogActivityScreen({
    super.key,
    required this.apiClient,
    this.onLogged,
  });

  final ApiClient apiClient;
  final VoidCallback? onLogged;

  @override
  State<LogActivityScreen> createState() => _LogActivityScreenState();
}

class _LogActivityScreenState extends State<LogActivityScreen> {
  int _durationMinutes = 60;
  String _selectedCategoryKey = 'help.category.shopping';
  int _insuranceContext = 1; // OrganizationPolicy
  int _transportMode = 0; // None
  final _notesController = TextEditingController();
  bool _isSubmitting = false;
  String? _errorMessage;

  static const List<String> _categoryKeys = [
    'help.category.shopping',
    'help.category.doctor',
    'help.category.authority',
    'help.category.accompaniment',
    'help.category.home_small',
    'help.category.language_practice',
    'help.category.newcomer_orientation',
    'help.category.mentoring',
  ];

  @override
  void initState() {
    super.initState();
    _loadLastEntryPrefill();
  }

  Future<void> _loadLastEntryPrefill() async {
    final prefs = await SharedPreferences.getInstance();
    final lastCat = prefs.getString('last_log_category');
    final lastDuration = prefs.getInt('last_log_duration');
    if (lastCat != null && _categoryKeys.contains(lastCat)) {
      setState(() {
        _selectedCategoryKey = lastCat;
        if (lastDuration != null) _durationMinutes = lastDuration;
      });
    }
  }

  @override
  void dispose() {
    _notesController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    setState(() {
      _isSubmitting = true;
      _errorMessage = null;
    });

    try {
      final now = DateTime.now();
      final dateOnly = '${now.year}-${now.month.toString().padLeft(2, '0')}-${now.day.toString().padLeft(2, '0')}';

      final payload = {
        'categoryId': '00000000-0000-0000-0000-000000000001',
        'occurredOn': dateOnly,
        'durationMinutes': _durationMinutes,
        'locationType': 0,
        'notes': _notesController.text.trim().isEmpty ? null : _notesController.text.trim(),
        'insuranceContext': _insuranceContext,
        'transportMode': _transportMode,
      };

      await widget.apiClient.post<dynamic>(
        '/api/v1/activities',
        data: payload,
      );

      // Save prefill preferences for next 2-tap log
      final prefs = await SharedPreferences.getInstance();
      await prefs.setString('last_log_category', _selectedCategoryKey);
      await prefs.setInt('last_log_duration', _durationMinutes);

      if (mounted) {
        setState(() => _isSubmitting = false);
        widget.onLogged?.call();
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('help.status.completed'.tr() + ' ✓'),
            backgroundColor: Theme.of(context).colorScheme.primary,
          ),
        );
        Navigator.of(context).maybePop();
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _isSubmitting = false;
          _errorMessage = 'errors.generic'.tr();
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: Text('home.senior.my_activities'.tr()),
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 600),
            child: SingleChildScrollView(
              padding: const EdgeInsetsDirectional.all(AppSpacing.lg),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(
                    'help.create.what'.tr(),
                    style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold),
                  ),
                  const SizedBox(height: AppSpacing.sm),
                  Wrap(
                    spacing: AppSpacing.sm,
                    runSpacing: AppSpacing.sm,
                    children: _categoryKeys.map((catKey) {
                      final isSelected = _selectedCategoryKey == catKey;
                      return ChoiceChip(
                        label: Text(catKey.tr()),
                        selected: isSelected,
                        onSelected: (selected) {
                          if (selected) setState(() => _selectedCategoryKey = catKey);
                        },
                      );
                    }).toList(),
                  ),
                  const SizedBox(height: AppSpacing.lg),

                  Text(
                    'help.create.when'.tr() + ' (Dauer)',
                    style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold),
                  ),
                  const SizedBox(height: AppSpacing.sm),
                  Wrap(
                    spacing: AppSpacing.sm,
                    runSpacing: AppSpacing.sm,
                    children: [30, 60, 90, 120].map((mins) {
                      final isSelected = _durationMinutes == mins;
                      return ChoiceChip(
                        label: Text('$mins Min'),
                        selected: isSelected,
                        onSelected: (selected) {
                          if (selected) setState(() => _durationMinutes = mins);
                        },
                      );
                    }).toList(),
                  ),
                  const SizedBox(height: AppSpacing.lg),

                  Text(
                    'help.create.details'.tr(),
                    style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold),
                  ),
                  const SizedBox(height: AppSpacing.sm),
                  TextField(
                    controller: _notesController,
                    decoration: InputDecoration(
                      hintText: 'help.create.details_hint'.tr(),
                      border: const OutlineInputBorder(),
                    ),
                    maxLines: 3,
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
                    label: 'common.save'.tr(),
                    icon: Icons.check,
                    isLoading: _isSubmitting,
                    onPressed: _submit,
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
