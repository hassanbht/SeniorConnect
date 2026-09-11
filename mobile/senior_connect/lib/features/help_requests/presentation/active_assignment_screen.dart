// lib/features/help_requests/presentation/active_assignment_screen.dart
//
// P3-23: Active assignment and Check-in / Check-out screens.
//
// Constraints and Rules:
// - Contact details (Name, Address, Phone) revealed only after assignment (BR-COMM-04).
// - Time-bounded Check-in / Check-out without ANY background location tracking (BR-HELP-06).
// - Direct 2-tap safeguarding concern reporting access (BR-SG-04, P4-10).
// - Accessible large touch targets in Senior Mode.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../core/router/app_router.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_states.dart';
import 'widgets/safeguarding_concern_dialog.dart';

class _AssignmentDetails {
  const _AssignmentDetails({
    required this.categoryNameKey,
    required this.address,
    required this.notes,
    required this.seniorDisplayName,
    required this.seniorPhone,
    required this.rowVersion,
  });

  final String categoryNameKey;
  final String? address;
  final String? notes;
  final String? seniorDisplayName;
  final String? seniorPhone;
  final int rowVersion;
}

class ActiveAssignmentScreen extends StatefulWidget {
  const ActiveAssignmentScreen({
    super.key,
    required this.apiClient,
    this.assignmentId,
  });

  final ApiClient apiClient;
  final String? assignmentId;

  @override
  State<ActiveAssignmentScreen> createState() => _ActiveAssignmentScreenState();
}

class _ActiveAssignmentScreenState extends State<ActiveAssignmentScreen> {
  bool _isLoading = true;
  bool _isCheckedIn = false;
  bool _isCompleted = false;
  bool _isActionInProgress = false;
  String? _loadError;
  String? _actionError;
  _AssignmentDetails? _details;

  @override
  void initState() {
    super.initState();
    _loadAssignment();
  }

  Future<void> _loadAssignment() async {
    final id = widget.assignmentId;
    if (id == null) {
      setState(() {
        _isLoading = false;
        _loadError = 'errors.generic'.tr();
      });
      return;
    }

    setState(() {
      _isLoading = true;
      _loadError = null;
    });

    try {
      final requestData = await widget.apiClient.get<dynamic>(
        '/api/v1/help-requests/$id',
      );
      final categoriesData = await widget.apiClient.get<dynamic>(
        '/api/v1/activities/categories',
      );

      final m = requestData as Map<String, dynamic>;
      var categoryNameKey = 'help.category.shopping';
      if (categoriesData is List) {
        for (final c in categoriesData) {
          final cm = c as Map<String, dynamic>;
          if (cm['id'] == m['categoryId']) {
            categoryNameKey = cm['nameKey'] as String? ?? categoryNameKey;
            break;
          }
        }
      }

      final details = _AssignmentDetails(
        categoryNameKey: categoryNameKey,
        address: m['locationAddress'] as String?,
        notes: m['notes'] as String?,
        seniorDisplayName: m['seniorDisplayName'] as String?,
        seniorPhone: m['seniorPhone'] as String?,
        rowVersion: (m['rowVersion'] as num?)?.toInt() ?? 0,
      );

      final status = m['status'] as String?;

      if (mounted) {
        setState(() {
          _details = details;
          _isCheckedIn = status == 'InProgress' || status == 'Completed';
          _isCompleted = status == 'Completed';
          _isLoading = false;
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _isLoading = false;
          _loadError = 'errors.generic'.tr();
        });
      }
    }
  }

  Future<void> _makeCall(String phoneNumber) async {
    final uri = Uri(scheme: 'tel', path: phoneNumber);
    if (await canLaunchUrl(uri)) {
      await launchUrl(uri);
    }
  }

  Future<void> _handleCheckIn() async {
    setState(() {
      _isActionInProgress = true;
      _actionError = null;
    });
    try {
      await widget.apiClient.post<dynamic>(
        '/api/v1/help-requests/${widget.assignmentId}:check-in',
      );
      if (mounted) {
        setState(() {
          _isCheckedIn = true;
          _isActionInProgress = false;
        });
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('help.checkin'.tr() + ' ✓'),
            backgroundColor: Theme.of(context).colorScheme.primary,
          ),
        );
      }
    } catch (_) {
      // P3-23 fix: a failed check-in must never be shown as a successful
      // one — the coordinator's hours record depends on this being real.
      if (mounted) {
        setState(() {
          _isActionInProgress = false;
          _actionError = 'errors.generic'.tr();
        });
      }
    }
  }

  Future<void> _handleComplete() async {
    setState(() {
      _isActionInProgress = true;
      _actionError = null;
    });
    try {
      await widget.apiClient.post<dynamic>(
        '/api/v1/help-requests/${widget.assignmentId}:complete',
        data: {'actualDurationMinutes': 60},
      );
      if (mounted) {
        setState(() {
          _isCompleted = true;
          _isActionInProgress = false;
        });
      }
    } catch (_) {
      // P3-23 fix: same rule — a failed completion must surface as an
      // error, never silently pretend the visit was recorded.
      if (mounted) {
        setState(() {
          _isActionInProgress = false;
          _actionError = 'errors.generic'.tr();
        });
      }
    }
  }

  void _reportSafeguardingConcern() {
    SafeguardingConcernDialog.show(
      context,
      subjectUserId: widget.assignmentId ?? 'unknown',
      apiClient: widget.apiClient,
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    if (_isLoading) {
      return Scaffold(
        body: SafeArea(child: AppLoading(message: 'common.loading'.tr())),
      );
    }

    if (_loadError != null || _details == null) {
      return Scaffold(
        body: SafeArea(
          child: AppErrorView(
            message: _loadError ?? 'errors.generic'.tr(),
            retryLabel: 'common.retry'.tr(),
            onRetry: _loadAssignment,
          ),
        ),
      );
    }

    final details = _details!;

    if (_isCompleted) {
      return Scaffold(
        body: SafeArea(
          child: Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 560),
              child: Padding(
                padding: const EdgeInsetsDirectional.all(AppSpacing.xl),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Icon(Icons.task_alt, size: 80, color: theme.colorScheme.primary),
                    const SizedBox(height: AppSpacing.lg),
                    Text(
                      'help.status.completed'.tr(),
                      style: theme.textTheme.headlineMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                      textAlign: TextAlign.center,
                    ),
                    const SizedBox(height: AppSpacing.md),
                    Text(
                      'Vielen Dank für Ihren Einsatz! Die Stunden wurden für den Monatsbericht erfasst.',
                      style: theme.textTheme.bodyLarge?.copyWith(
                        color: theme.colorScheme.onSurfaceVariant,
                      ),
                      textAlign: TextAlign.center,
                    ),
                    const SizedBox(height: AppSpacing.xxl),
                    AppButton(
                      label: 'common.close'.tr(),
                      onPressed: () => context.go(AppRoutes.home),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      );
    }

    return Scaffold(
      appBar: AppBar(
        title: Text('help.status.assigned'.tr()),
        actions: [
          IconButton(
            icon: const Icon(Icons.security),
            tooltip: 'safeguarding.report'.tr(),
            onPressed: _reportSafeguardingConcern,
          ),
        ],
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
                  // Status Banner
                  Container(
                    padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                    decoration: BoxDecoration(
                      color: _isCheckedIn
                          ? theme.colorScheme.tertiaryContainer
                          : theme.colorScheme.primaryContainer,
                      borderRadius: AppRadius.card,
                    ),
                    child: Row(
                      children: [
                        Icon(
                          _isCheckedIn ? Icons.timelapse : Icons.calendar_today,
                          color: _isCheckedIn
                              ? theme.colorScheme.onTertiaryContainer
                              : theme.colorScheme.onPrimaryContainer,
                        ),
                        const SizedBox(width: AppSpacing.md),
                        Expanded(
                          child: Text(
                            _isCheckedIn
                                ? 'help.status.in_progress'.tr()
                                : 'help.status.assigned'.tr(),
                            style: theme.textTheme.titleMedium?.copyWith(
                              fontWeight: FontWeight.bold,
                              color: _isCheckedIn
                                  ? theme.colorScheme.onTertiaryContainer
                                  : theme.colorScheme.onPrimaryContainer,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: AppSpacing.lg),

                  // Senior Contact Details Card (Revealed only after assignment)
                  Card(
                    elevation: 0,
                    shape: RoundedRectangleBorder(
                      borderRadius: AppRadius.card,
                      side: BorderSide(color: theme.colorScheme.outlineVariant),
                    ),
                    child: Padding(
                      padding: const EdgeInsetsDirectional.all(AppSpacing.lg),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              CircleAvatar(
                                radius: 24,
                                backgroundColor: theme.colorScheme.primaryContainer,
                                child: Text(
                                  (details.seniorDisplayName?.isNotEmpty ?? false)
                                      ? details.seniorDisplayName![0]
                                      : 'S',
                                  style: const TextStyle(
                                    fontSize: 20,
                                    fontWeight: FontWeight.bold,
                                  ),
                                ),
                              ),
                              const SizedBox(width: AppSpacing.md),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      details.seniorDisplayName ?? 'help.status.assigned'.tr(),
                                      style: theme.textTheme.titleLarge?.copyWith(
                                        fontWeight: FontWeight.bold,
                                      ),
                                    ),
                                    Text(
                                      details.categoryNameKey.tr(),
                                      style: theme.textTheme.bodyMedium?.copyWith(
                                        color: theme.colorScheme.onSurfaceVariant,
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            ],
                          ),
                          const Divider(height: AppSpacing.xl),

                          // Phone Call Action — only shown once the backend
                          // actually revealed a number (BR-COMM-04).
                          if (details.seniorPhone != null) ...[
                            ListTile(
                              contentPadding: EdgeInsets.zero,
                              leading: Icon(Icons.phone, color: theme.colorScheme.primary),
                              title: Text(details.seniorPhone!),
                              trailing: IconButton(
                                icon: const Icon(Icons.call),
                                onPressed: () => _makeCall(details.seniorPhone!),
                              ),
                            ),
                            const SizedBox(height: AppSpacing.xs),
                          ],

                          // Address
                          if (details.address != null)
                            ListTile(
                              contentPadding: EdgeInsets.zero,
                              leading: Icon(Icons.place, color: theme.colorScheme.primary),
                              title: Text(details.address!),
                            ),

                          if ((details.notes ?? '').isNotEmpty) ...[
                            const SizedBox(height: AppSpacing.sm),
                            Container(
                              padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                              decoration: BoxDecoration(
                                color: theme.colorScheme.surfaceContainerHighest,
                                borderRadius: AppRadius.card,
                              ),
                              child: Text(
                                details.notes!,
                                style: theme.textTheme.bodyMedium,
                              ),
                            ),
                          ],
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: AppSpacing.xl),

                  if (_actionError != null) ...[
                    Text(
                      _actionError!,
                      style: TextStyle(color: theme.colorScheme.error),
                    ),
                    const SizedBox(height: AppSpacing.md),
                  ],

                  // Check-in or Complete Action Button
                  if (!_isCheckedIn)
                    AppButton(
                      label: 'help.checkin'.tr() + ' (Angekommen)',
                      icon: Icons.login,
                      isLoading: _isActionInProgress,
                      onPressed: _handleCheckIn,
                    )
                  else
                    AppButton(
                      label: 'help.checkout'.tr() + ' (Abschließen)',
                      icon: Icons.check_circle,
                      isLoading: _isActionInProgress,
                      variant: AppButtonVariant.primary,
                      onPressed: _handleComplete,
                    ),

                  const SizedBox(height: AppSpacing.lg),

                  // Report Concern (2-tap safety, BR-SG-04)
                  TextButton.icon(
                    icon: Icon(Icons.flag_outlined, color: theme.colorScheme.error),
                    label: Text(
                      'safeguarding.report'.tr(),
                      style: TextStyle(color: theme.colorScheme.error),
                    ),
                    onPressed: _reportSafeguardingConcern,
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
