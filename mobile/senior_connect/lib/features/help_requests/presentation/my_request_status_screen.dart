// lib/features/help_requests/presentation/my_request_status_screen.dart
//
// P3-23 / P4-08 fix: the senior/requester side of an accepted assignment
// had NO screen at all — active_assignment_screen.dart is exclusively the
// volunteer's view. This is the counterpart: read-only, shows who is
// coming and when, reveals the volunteer's contact info once assigned
// (BR-COMM-04, backend already gates this correctly), and gives access to
// the First Meeting Protocol (P4-08) from the requester's side too.
//
// No check-in / complete actions here — those are the volunteer's, not
// the senior's.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_states.dart';
import '../application/my_request_status_notifier.dart';
import 'widgets/first_meeting_protocol_dialog.dart';
import 'widgets/safeguarding_concern_dialog.dart';

class MyRequestStatusScreen extends ConsumerWidget {
  const MyRequestStatusScreen({
    super.key,
    required this.apiClient,
    this.requestId,
  });

  final ApiClient apiClient;
  final String? requestId;

  Future<void> _makeCall(String phoneNumber) async {
    final uri = Uri(scheme: 'tel', path: phoneNumber);
    if (await canLaunchUrl(uri)) {
      await launchUrl(uri);
    }
  }

  void _reportSafeguardingConcern(BuildContext context) {
    SafeguardingConcernDialog.show(
      context,
      subjectUserId: requestId ?? 'unknown',
      apiClient: apiClient,
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final params = MyRequestStatusParams(apiClient: apiClient, requestId: requestId);
    final state = ref.watch(myRequestStatusProvider(params));

    if (state.isLoading) {
      return Scaffold(
        body: SafeArea(child: AppLoading(message: 'common.loading'.tr())),
      );
    }

    if (state.loadError != null || state.details == null) {
      return Scaffold(
        body: SafeArea(
          child: AppErrorView(
            message: state.loadError ?? 'errors.generic'.tr(),
            retryLabel: 'common.retry'.tr(),
            onRetry: () => ref.read(myRequestStatusProvider(params).notifier).loadStatus(),
          ),
        ),
      );
    }

    final details = state.details!;
    final hasVolunteer = details.volunteerDisplayName != null;

    return Scaffold(
      appBar: AppBar(
        title: Text('help.my_request.title'.tr()),
        actions: [
          IconButton(
            icon: const Icon(Icons.shield_outlined),
            tooltip: 'protocol.title'.tr(),
            onPressed: () => FirstMeetingProtocolDialog.show(context),
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
                  // Status Banner — reuses the same help.status.* keys as
                  // the volunteer's screen, so the two sides never drift.
                  Container(
                    padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                    decoration: BoxDecoration(
                      color: hasVolunteer
                          ? theme.colorScheme.tertiaryContainer
                          : theme.colorScheme.primaryContainer,
                      borderRadius: AppRadius.card,
                    ),
                    child: Row(
                      children: [
                        Icon(
                          hasVolunteer ? Icons.handshake : Icons.hourglass_top,
                          color: hasVolunteer
                              ? theme.colorScheme.onTertiaryContainer
                              : theme.colorScheme.onPrimaryContainer,
                        ),
                        const SizedBox(width: AppSpacing.md),
                        Expanded(
                          child: Text(
                            'help.status.${_statusKey(details.status)}'.tr(),
                            style: theme.textTheme.titleMedium?.copyWith(
                              fontWeight: FontWeight.bold,
                              color: hasVolunteer
                                  ? theme.colorScheme.onTertiaryContainer
                                  : theme.colorScheme.onPrimaryContainer,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: AppSpacing.lg),

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
                          Text(
                            details.categoryNameKey.tr(),
                            style: theme.textTheme.titleLarge?.copyWith(fontWeight: FontWeight.bold),
                          ),
                          if (details.scheduledStartUtc != null) ...[
                            const SizedBox(height: AppSpacing.xs),
                            Row(
                              children: [
                                Icon(Icons.event, size: 20, color: theme.colorScheme.onSurfaceVariant),
                                const SizedBox(width: AppSpacing.sm),
                                Text(
                                  details.scheduledStartUtc!.toLocal().toString(),
                                  style: theme.textTheme.bodyMedium?.copyWith(
                                    color: theme.colorScheme.onSurfaceVariant,
                                  ),
                                ),
                              ],
                            ),
                          ],

                          if ((details.notes ?? '').isNotEmpty) ...[
                            const SizedBox(height: AppSpacing.md),
                            Container(
                              padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                              decoration: BoxDecoration(
                                color: theme.colorScheme.surfaceContainerHighest,
                                borderRadius: AppRadius.card,
                              ),
                              child: Text(details.notes!, style: theme.textTheme.bodyMedium),
                            ),
                          ],

                          if (hasVolunteer) ...[
                            const Divider(height: AppSpacing.xl),
                            Row(
                              children: [
                                CircleAvatar(
                                  radius: 24,
                                  backgroundColor: theme.colorScheme.primaryContainer,
                                  child: Text(
                                    details.volunteerDisplayName![0],
                                    style: const TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
                                  ),
                                ),
                                const SizedBox(width: AppSpacing.md),
                                Expanded(
                                  child: Text(
                                    details.volunteerDisplayName!,
                                    style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold),
                                  ),
                                ),
                              ],
                            ),
                            if (details.volunteerPhone != null) ...[
                              const SizedBox(height: AppSpacing.sm),
                              ListTile(
                                contentPadding: EdgeInsets.zero,
                                leading: Icon(Icons.phone, color: theme.colorScheme.primary),
                                title: Text(details.volunteerPhone!),
                                trailing: IconButton(
                                  icon: const Icon(Icons.call),
                                  onPressed: () => _makeCall(details.volunteerPhone!),
                                ),
                              ),
                            ],
                          ] else ...[
                            const SizedBox(height: AppSpacing.md),
                            Text(
                              'help.my_request.waiting_for_volunteer'.tr(),
                              style: theme.textTheme.bodyMedium?.copyWith(
                                color: theme.colorScheme.onSurfaceVariant,
                              ),
                            ),
                          ],
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: AppSpacing.lg),

                  TextButton.icon(
                    icon: Icon(Icons.flag_outlined, color: theme.colorScheme.error),
                    label: Text(
                      'safeguarding.report'.tr(),
                      style: TextStyle(color: theme.colorScheme.error),
                    ),
                    onPressed: () => _reportSafeguardingConcern(context),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  static String _statusKey(String backendStatus) {
    switch (backendStatus) {
      case 'Open':
      case 'Matching':
        return 'open';
      case 'Offered':
        return 'offered';
      case 'Assigned':
        return 'assigned';
      case 'InProgress':
        return 'in_progress';
      case 'Completed':
        return 'completed';
      case 'Cancelled':
        return 'cancelled';
      case 'Expired':
        return 'expired';
      case 'NoShow':
        return 'cancelled'; // no dedicated no_show copy yet — closest existing key
      default:
        return 'open';
    }
  }
}
