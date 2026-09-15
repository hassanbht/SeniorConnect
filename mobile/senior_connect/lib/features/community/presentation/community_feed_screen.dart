// lib/features/community/presentation/community_feed_screen.dart
//
// P5-06, P5-07: Community discovery feed with category filtering and spot availability.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_states.dart';
import '../application/community_feed_notifier.dart';
import 'event_detail_screen.dart';

class CommunityFeedScreen extends ConsumerWidget {
  const CommunityFeedScreen({
    super.key,
    this.apiClient,
  });

  final ApiClient? apiClient;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final state = ref.watch(communityFeedProvider(apiClient));
    final notifier = ref.read(communityFeedProvider(apiClient).notifier);

    return Scaffold(
      appBar: AppBar(
        title: Text('community.feed_title'.tr()),
      ),
      body: SafeArea(
        child: Column(
          children: [
            // Category Filter Chips
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsetsDirectional.symmetric(
                horizontal: AppSpacing.md,
                vertical: AppSpacing.sm,
              ),
              child: Row(
                children: CommunityFeedNotifier.categories.map((cat) {
                  final isSelected =
                      (state.selectedCategory == null && cat == 'all') ||
                          state.selectedCategory == cat;
                  return Padding(
                    padding:
                        const EdgeInsetsDirectional.only(end: AppSpacing.sm),
                    child: FilterChip(
                      selected: isSelected,
                      label: Text('community.cat_$cat'.tr()),
                      onSelected: (selected) {
                        notifier.setCategory(selected ? cat : null);
                      },
                    ),
                  );
                }).toList(),
              ),
            ),
            Expanded(
              child: state.isLoading
                  ? AppLoading(message: 'common.loading'.tr())
                  : state.events.isEmpty
                      ? AppEmptyState(
                          icon: Icons.event_busy,
                          message: 'empty.no_activities'.tr(),
                        )
                      : ListView.separated(
                          padding:
                              const EdgeInsetsDirectional.all(AppSpacing.md),
                          itemCount: state.events.length,
                          separatorBuilder: (_, __) =>
                              const SizedBox(height: AppSpacing.md),
                          itemBuilder: (context, index) {
                            final ev = state.events[index];
                            return Card(
                              elevation: 1,
                              shape: RoundedRectangleBorder(
                                borderRadius:
                                    BorderRadius.circular(AppRadius.md),
                              ),
                              child: InkWell(
                                borderRadius:
                                    BorderRadius.circular(AppRadius.md),
                                onTap: () {
                                  if (apiClient != null) {
                                    Navigator.of(context).push(
                                      MaterialPageRoute<void>(
                                        builder: (_) => EventDetailScreen(
                                          eventId: ev['id'] as String? ??
                                              'ev-1',
                                          apiClient: apiClient!,
                                        ),
                                      ),
                                    );
                                  }
                                },
                                child: Padding(
                                  padding: const EdgeInsetsDirectional.all(
                                      AppSpacing.md),
                                  child: Column(
                                    crossAxisAlignment:
                                        CrossAxisAlignment.start,
                                    children: [
                                      Text(
                                        ev['title'] as String? ?? '',
                                        style: theme.textTheme.titleMedium
                                            ?.copyWith(
                                          fontWeight: FontWeight.bold,
                                          color: theme.colorScheme.primary,
                                        ),
                                      ),
                                      const SizedBox(height: AppSpacing.sm),
                                      if (ev['date'] != null) ...[
                                        Row(
                                          children: [
                                            Icon(Icons.calendar_today,
                                                size: 16,
                                                color: theme.colorScheme
                                                    .onSurfaceVariant),
                                            const SizedBox(
                                                width: AppSpacing.xs),
                                            Text(ev['date'] as String,
                                                style: theme
                                                    .textTheme.bodyMedium),
                                          ],
                                        ),
                                        const SizedBox(height: 4),
                                      ],
                                      if (ev['location'] != null) ...[
                                        Row(
                                          children: [
                                            Icon(Icons.location_on,
                                                size: 16,
                                                color: theme.colorScheme
                                                    .onSurfaceVariant),
                                            const SizedBox(
                                                width: AppSpacing.xs),
                                            Text(ev['location'] as String,
                                                style: theme
                                                    .textTheme.bodyMedium),
                                          ],
                                        ),
                                        const SizedBox(height: AppSpacing.sm),
                                      ],
                                      if (ev['spots'] != null)
                                        Container(
                                          padding:
                                              const EdgeInsets.symmetric(
                                                  horizontal: 8, vertical: 4),
                                          decoration: BoxDecoration(
                                            color: theme.colorScheme
                                                .primaryContainer,
                                            borderRadius:
                                                BorderRadius.circular(
                                                    AppRadius.sm),
                                          ),
                                          child: Text(
                                            ev['spots'] as String,
                                            style: theme.textTheme.bodySmall
                                                ?.copyWith(
                                              color: theme.colorScheme
                                                  .onPrimaryContainer,
                                              fontWeight: FontWeight.w600,
                                            ),
                                          ),
                                        ),
                                    ],
                                  ),
                                ),
                              ),
                            );
                          },
                        ),
            ),
          ],
        ),
      ),
    );
  }
}
