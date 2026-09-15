// lib/features/help_requests/presentation/volunteer_feed_screen.dart
//
// P3-22: Volunteer opportunity feed.
//
// Constraints and Rules:
// - "In Ihrer Nähe" feed with proximity filters.
// - Contact details (phone/exact address) hidden until accepted (BR-COMM-04).
// - Ineligible cards greyed out with clear reason & path to eligibility in plain German.
// - Atomic accept handling (BR-HELP-03) with graceful 409 Conflict handling.
// - Full responsive layout and empty/loading/error states.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../core/router/app_router.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_states.dart';
import '../application/volunteer_feed_notifier.dart';
import 'widgets/first_meeting_protocol_dialog.dart';

class VolunteerFeedScreen extends ConsumerWidget {
  const VolunteerFeedScreen({
    super.key,
    required this.apiClient,
  });

  final ApiClient apiClient;

  Future<void> _acceptRequest(
    BuildContext context,
    WidgetRef ref,
    HelpRequestFeedItem item,
  ) async {
    final notifier = ref.read(volunteerFeedProvider(apiClient).notifier);
    final success = await notifier.acceptRequest(item);

    if (!context.mounted) return;

    if (success) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('help.status.assigned'.tr()),
          backgroundColor: Theme.of(context).colorScheme.primary,
        ),
      );
      context.push('${AppRoutes.activeAssignment}/${item.id}');
    } else {
      showDialog<void>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: Text('errors.generic'.tr()),
          content: Text('errors.already_taken'.tr()),
          actions: [
            TextButton(
              onPressed: () {
                Navigator.of(ctx).pop();
                notifier.loadFeed();
              },
              child: Text('common.close'.tr()),
            ),
          ],
        ),
      );
    }
  }

  void _showIneligibleDialog(BuildContext context, HelpRequestFeedItem item) {
    showDialog<void>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('trust.missing_title'.tr()),
        content: Text(item.ineligibleReason ?? ''),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(),
            child: Text('common.close'.tr()),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final feedState = ref.watch(volunteerFeedProvider(apiClient));
    final notifier = ref.read(volunteerFeedProvider(apiClient).notifier);

    return Scaffold(
      appBar: AppBar(
        title: Text('home.senior.my_activities'.tr()),
        actions: [
          IconButton(
            icon: const Icon(Icons.shield_outlined),
            tooltip: 'protocol.title'.tr(),
            onPressed: () => FirstMeetingProtocolDialog.show(context),
          ),
          IconButton(
            icon: const Icon(Icons.refresh),
            tooltip: 'common.retry'.tr(),
            onPressed: notifier.loadFeed,
          ),
        ],
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 640),
            child: _buildBody(context, ref, theme, feedState, notifier),
          ),
        ),
      ),
    );
  }

  Widget _buildBody(
    BuildContext context,
    WidgetRef ref,
    ThemeData theme,
    VolunteerFeedState feedState,
    VolunteerFeedNotifier notifier,
  ) {
    if (feedState.isLoading) {
      return AppLoading(message: 'common.loading'.tr());
    }

    if (feedState.errorMessage != null) {
      return AppErrorView(
        message: feedState.errorMessage!,
        retryLabel: 'common.retry'.tr(),
        onRetry: notifier.loadFeed,
      );
    }

    if (feedState.requests.isEmpty) {
      return AppEmptyState(
        icon: Icons.inbox_outlined,
        message: 'empty.no_requests'.tr(),
        actionLabel: 'common.retry'.tr(),
        onAction: notifier.loadFeed,
      );
    }

    return Column(
      children: [
        // Filter bar
        Padding(
          padding: const EdgeInsetsDirectional.symmetric(
            horizontal: AppSpacing.lg,
            vertical: AppSpacing.sm,
          ),
          child: Row(
            children: [
              Icon(Icons.near_me, size: 20, color: theme.colorScheme.primary),
              const SizedBox(width: AppSpacing.sm),
              Text(
                'common.km_away'.tr(args: ['${feedState.maxDistance.toInt()}']),
                style: theme.textTheme.labelLarge,
              ),
              Expanded(
                child: Slider(
                  value: feedState.maxDistance,
                  min: 1,
                  max: 25,
                  divisions: 24,
                  onChanged: (v) => notifier.setMaxDistance(v),
                ),
              ),
            ],
          ),
        ),
        // List
        Expanded(
          child: RefreshIndicator(
            onRefresh: notifier.loadFeed,
            child: ListView.separated(
              padding: const EdgeInsetsDirectional.all(AppSpacing.md),
              itemCount: feedState.requests.length,
              separatorBuilder: (_, _) =>
                  const SizedBox(height: AppSpacing.md),
              itemBuilder: (ctx, index) {
                final item = feedState.requests[index];
                return _HelpRequestCard(
                  item: item,
                  isAccepting: feedState.acceptingId == item.id,
                  onAccept: () => _acceptRequest(context, ref, item),
                  onIneligibleTapped: () =>
                      _showIneligibleDialog(context, item),
                );
              },
            ),
          ),
        ),
      ],
    );
  }
}

class _HelpRequestCard extends StatelessWidget {
  const _HelpRequestCard({
    required this.item,
    required this.isAccepting,
    required this.onAccept,
    required this.onIneligibleTapped,
  });

  final HelpRequestFeedItem item;
  final bool isAccepting;
  final VoidCallback onAccept;
  final VoidCallback onIneligibleTapped;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;

    final cardColor = item.isEligible
        ? colorScheme.surface
        : colorScheme.surfaceContainerHighest.withAlpha(128);

    return Card(
      color: cardColor,
      elevation: item.isEligible ? 2 : 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.md),
        side: BorderSide(
          color: item.isEligible
              ? colorScheme.outlineVariant
              : colorScheme.outline.withAlpha(50),
        ),
      ),
      child: Padding(
        padding: const EdgeInsetsDirectional.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Category & Distance header
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Chip(
                  label: Text(
                    item.categoryName,
                    style: theme.textTheme.labelMedium?.copyWith(
                      fontWeight: FontWeight.bold,
                      color: item.isEligible
                          ? colorScheme.onSecondaryContainer
                          : colorScheme.outline,
                    ),
                  ),
                  backgroundColor: item.isEligible
                      ? colorScheme.secondaryContainer
                      : colorScheme.surfaceContainerHighest,
                  visualDensity: VisualDensity.compact,
                ),
                Row(
                  children: [
                    Icon(
                      Icons.place_outlined,
                      size: 16,
                      color: colorScheme.onSurfaceVariant,
                    ),
                    const SizedBox(width: 4),
                    Text(
                      'common.km_away'.tr(
                        args: [item.distanceKm.toStringAsFixed(1)],
                      ),
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: colorScheme.onSurfaceVariant,
                      ),
                    ),
                  ],
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.sm),

            // Timing
            Row(
              children: [
                Icon(
                  Icons.calendar_today_outlined,
                  size: 16,
                  color: colorScheme.onSurfaceVariant,
                ),
                const SizedBox(width: AppSpacing.xs),
                Text(
                  item.scheduledTime,
                  style: theme.textTheme.bodyMedium?.copyWith(
                    fontWeight: FontWeight.w600,
                  ),
                ),
                const SizedBox(width: AppSpacing.md),
                Icon(
                  Icons.schedule_outlined,
                  size: 16,
                  color: colorScheme.onSurfaceVariant,
                ),
                const SizedBox(width: AppSpacing.xs),
                Text(
                  '${item.durationMinutes} min',
                  style: theme.textTheme.bodyMedium,
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.md),

            // Ineligibility Warning or Accept Button
            if (!item.isEligible) ...[
              InkWell(
                onTap: onIneligibleTapped,
                borderRadius: BorderRadius.circular(AppRadius.sm),
                child: Container(
                  padding: const EdgeInsetsDirectional.all(AppSpacing.sm),
                  decoration: BoxDecoration(
                    color: colorScheme.errorContainer.withAlpha(50),
                    borderRadius: BorderRadius.circular(AppRadius.sm),
                    border: Border.all(
                      color: colorScheme.error.withAlpha(100),
                    ),
                  ),
                  child: Row(
                    children: [
                      Icon(
                        Icons.info_outline,
                        color: colorScheme.error,
                        size: 20,
                      ),
                      const SizedBox(width: AppSpacing.xs),
                      Expanded(
                        child: Text(
                          item.ineligibleReason ?? 'trust.ineligible_badge'.tr(),
                          style: theme.textTheme.bodySmall?.copyWith(
                            color: colorScheme.error,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                      ),
                      Icon(
                        Icons.chevron_right,
                        color: colorScheme.error,
                        size: 16,
                      ),
                    ],
                  ),
                ),
              ),
            ] else ...[
              Align(
                alignment: AlignmentDirectional.centerEnd,
                child: AppButton(
                  label: 'help.action.accept'.tr(),
                  variant: AppButtonVariant.primary,
                  isLoading: isAccepting,
                  onPressed: onAccept,
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
