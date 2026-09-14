// lib/features/organizations/presentation/coordinator_hours_queue_screen.dart
//
// P2-28: Hours confirmation queue screen for coordinators.
// Displays unconfirmed hours submitted by volunteers, allowing coordinators
// to confirm or dispute individual records in 1 tap or batch-confirm.

import 'package:dio/dio.dart';
import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_states.dart';

class CoordinatorHoursQueueScreen extends StatefulWidget {
  const CoordinatorHoursQueueScreen({
    super.key,
    required this.organizationId,
    required this.apiClient,
  });

  final String organizationId;
  final ApiClient apiClient;

  @override
  State<CoordinatorHoursQueueScreen> createState() =>
      _CoordinatorHoursQueueScreenState();
}

class _CoordinatorHoursQueueScreenState
    extends State<CoordinatorHoursQueueScreen> {
  bool _isLoading = true;
  bool _isBatchProcessing = false;
  String? _errorMessage;
  List<dynamic> _unconfirmedActivities = [];

  @override
  void initState() {
    super.initState();
    _loadQueue();
  }

  Future<void> _loadQueue() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final resp = await widget.apiClient.get<dynamic>(
        '/api/v1/coordinator/attention/unconfirmed-hours?organizationId=${widget.organizationId}',
      );

      if (resp is List) {
        _unconfirmedActivities = resp;
      }

      if (mounted) setState(() => _isLoading = false);
    } on DioException catch (e) {
      if (mounted) {
        setState(() {
          _isLoading = false;
          _errorMessage = mapDioError(e).l10nKey.tr();
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _isLoading = false;
          _errorMessage = 'errors.generic'.tr();
        });
      }
    }
  }

  Future<void> _confirmActivity(String activityId) async {
    try {
      await widget.apiClient.post<dynamic>(
        '/api/v1/activities/$activityId:confirm',
      );

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('coordinator.activity_confirmed'.tr())),
        );
        _loadQueue();
      }
    } on DioException catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(mapDioError(e).l10nKey.tr())),
        );
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('errors.generic'.tr())),
        );
      }
    }
  }

  Future<void> _confirmAll() async {
    if (_unconfirmedActivities.isEmpty) return;

    setState(() => _isBatchProcessing = true);
    int successCount = 0;

    for (final act in _unconfirmedActivities) {
      final id = act['id'] as String?;
      if (id == null) continue;
      try {
        await widget.apiClient.post<dynamic>('/api/v1/activities/$id:confirm');
        successCount++;
      } catch (_) {}
    }

    if (mounted) {
      setState(() => _isBatchProcessing = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('coordinator.all_activities_confirmed'.tr(args: ['$successCount'])),
        ),
      );
      _loadQueue();
    }
  }

  Future<void> _disputeActivity(String activityId) async {
    final reasonController = TextEditingController();

    final shouldDispute = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('coordinator.dispute_title'.tr()),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text('coordinator.dispute_prompt'.tr()),
            const SizedBox(height: AppSpacing.sm),
            TextField(
              controller: reasonController,
              decoration: InputDecoration(
                hintText: 'coordinator.dispute_reason_hint'.tr(),
                border: const OutlineInputBorder(),
              ),
              maxLines: 2,
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(false),
            child: Text('common.cancel'.tr()),
          ),
          FilledButton(
            onPressed: () => Navigator.of(ctx).pop(true),
            child: Text('coordinator.dispute'.tr()),
          ),
        ],
      ),
    );

    if (shouldDispute == true) {
      try {
        await widget.apiClient.post<dynamic>(
          '/api/v1/activities/$activityId:dispute',
          data: {'reason': reasonController.text.trim()},
        );

        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text('coordinator.activity_disputed'.tr())),
          );
          _loadQueue();
        }
      } on DioException catch (e) {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text(mapDioError(e).l10nKey.tr())),
          );
        }
      } catch (_) {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text('errors.generic'.tr())),
          );
        }
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: Text('coordinator.hours_queue_title'.tr()),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            tooltip: 'common.retry'.tr(),
            onPressed: _loadQueue,
          ),
        ],
      ),
      bottomNavigationBar: _unconfirmedActivities.isNotEmpty
          ? SafeArea(
              child: Padding(
                padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                child: AppButton(
                  label: 'coordinator.confirm_all_button'.tr(
                    args: ['${_unconfirmedActivities.length}'],
                  ),
                  icon: Icons.done_all,
                  isLoading: _isBatchProcessing,
                  onPressed: _isBatchProcessing ? null : _confirmAll,
                ),
              ),
            )
          : null,
      body: SafeArea(
        child: _isLoading
            ? AppLoading(message: 'common.loading'.tr())
            : _errorMessage != null
                ? AppErrorView(
                    message: _errorMessage!,
                    retryLabel: 'common.retry'.tr(),
                    onRetry: _loadQueue,
                  )
                : _unconfirmedActivities.isEmpty
                    ? AppEmptyState(
                        icon: Icons.done_all,
                        message: 'coordinator.no_unconfirmed_hours'.tr(),
                      )
                    : ListView.separated(
                        padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                        itemCount: _unconfirmedActivities.length,
                        separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.sm),
                        itemBuilder: (context, index) {
                          final act = _unconfirmedActivities[index] as Map<String, dynamic>;
                          final id = act['id'] as String? ?? '';
                          final durationMinutes = act['durationMinutes'] as int? ?? 0;
                          final date = act['occurredOn'] as String? ?? '–';
                          final notes = act['notes'] as String?;
                          final status = act['status'] as String? ?? 'Logged';

                          return Card(
                            child: Padding(
                              padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Row(
                                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                    children: [
                                      Text(
                                        '${(durationMinutes / 60.0).toStringAsFixed(1)} Stunden ($durationMinutes min)',
                                        style: theme.textTheme.titleMedium?.copyWith(
                                          fontWeight: FontWeight.bold,
                                        ),
                                      ),
                                      Text(date, style: theme.textTheme.bodySmall),
                                    ],
                                  ),
                                  const SizedBox(height: AppSpacing.xs),
                                  if (notes != null && notes.isNotEmpty) ...[
                                    Text(
                                      notes,
                                      style: theme.textTheme.bodyMedium,
                                    ),
                                    const SizedBox(height: AppSpacing.xs),
                                  ],
                                  Text(
                                    'Status: $status',
                                    style: theme.textTheme.bodySmall?.copyWith(
                                      color: status == 'Disputed'
                                          ? theme.colorScheme.error
                                          : theme.colorScheme.primary,
                                      fontWeight: FontWeight.w600,
                                    ),
                                  ),
                                  const SizedBox(height: AppSpacing.md),
                                  Row(
                                    mainAxisAlignment: MainAxisAlignment.end,
                                    children: [
                                      OutlinedButton.icon(
                                        onPressed: () => _disputeActivity(id),
                                        icon: const Icon(Icons.close),
                                        label: Text('coordinator.dispute'.tr()),
                                        style: OutlinedButton.styleFrom(
                                          foregroundColor: theme.colorScheme.error,
                                        ),
                                      ),
                                      const SizedBox(width: AppSpacing.sm),
                                      FilledButton.icon(
                                        onPressed: () => _confirmActivity(id),
                                        icon: const Icon(Icons.check),
                                        label: Text('coordinator.confirm'.tr()),
                                      ),
                                    ],
                                  ),
                                ],
                              ),
                            ),
                          );
                        },
                      ),
      ),
    );
  }
}
