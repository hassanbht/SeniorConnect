// lib/features/community/presentation/event_detail_screen.dart
//
// P5-03, P5-05: Event Detail Screen with 1-tap join and discussion thread access.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_states.dart';
import '../application/event_detail_notifier.dart';

class EventDetailScreen extends ConsumerWidget {
  const EventDetailScreen({
    super.key,
    required this.eventId,
    required this.apiClient,
  });

  final String eventId;
  final ApiClient apiClient;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final params = EventDetailParams(eventId: eventId, apiClient: apiClient);
    final state = ref.watch(eventDetailProvider(params));
    final notifier = ref.read(eventDetailProvider(params).notifier);

    if (state.isLoading && state.event == null) {
      return Scaffold(body: AppLoading(message: 'common.loading'.tr()));
    }

    if (state.error != null && state.event == null) {
      return Scaffold(
        appBar: AppBar(),
        body: AppErrorView(
          message: state.error!,
          retryLabel: 'common.retry'.tr(),
          onRetry: () => notifier.loadEvent(),
        ),
      );
    }

    final ev = state.event ?? {};
    final title = ev['title'] as String? ?? 'Event';
    final description = ev['description'] as String? ?? '';
    final address = ev['locationAddress'] as String?;
    final capacity = ev['capacity'] as int?;
    final goingCount = ev['goingCount'] as int? ?? 0;

    return Scaffold(
      appBar: AppBar(
        title: Text(title),
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsetsDirectional.all(AppSpacing.lg),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 600),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  title,
                  style: theme.textTheme.headlineSmall?.copyWith(
                    fontWeight: FontWeight.bold,
                    color: theme.colorScheme.primary,
                  ),
                ),
                const SizedBox(height: AppSpacing.md),
                if (description.isNotEmpty) ...[
                  Text(
                    description,
                    style: theme.textTheme.bodyLarge,
                  ),
                  const SizedBox(height: AppSpacing.lg),
                ],
                Card(
                  elevation: 1,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(AppRadius.md),
                  ),
                  child: Padding(
                    padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                    child: Column(
                      children: [
                        if (address != null) ...[
                          Row(
                            children: [
                              Icon(Icons.location_on,
                                  color: theme.colorScheme.primary),
                              const SizedBox(width: AppSpacing.sm),
                              Expanded(
                                child: Text(
                                  address,
                                  style: theme.textTheme.bodyMedium,
                                ),
                              ),
                            ],
                          ),
                          const Divider(height: AppSpacing.lg),
                        ],
                        Row(
                          children: [
                            Icon(Icons.people,
                                color: theme.colorScheme.primary),
                            const SizedBox(width: AppSpacing.sm),
                            Text(
                              capacity != null
                                  ? '$goingCount / $capacity ${'community.spots_taken'.tr()}'
                                  : '$goingCount ${'community.attendees'.tr()}',
                              style: theme.textTheme.bodyMedium?.copyWith(
                                  fontWeight: FontWeight.w600),
                            ),
                          ],
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(height: AppSpacing.xl),
                AppButton(
                  label: state.isRegistered
                      ? 'community.cancel_registration'.tr()
                      : 'community.join_event'.tr(),
                  variant: state.isRegistered
                      ? AppButtonVariant.outlined
                      : AppButtonVariant.primary,
                  icon: state.isRegistered
                      ? Icons.check
                      : Icons.add_circle_outline,
                  isLoading: state.isActionInProgress,
                  onPressed: () => notifier.toggleRegistration(),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
