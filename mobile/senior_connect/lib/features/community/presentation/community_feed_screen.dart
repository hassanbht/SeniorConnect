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
            // Category filter chips + P5-06 nearby toggle
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsetsDirectional.symmetric(
                horizontal: AppSpacing.md,
                vertical: AppSpacing.sm,
              ),
              child: Row(
                children: [
                  Padding(
                    padding:
                        const EdgeInsetsDirectional.only(end: AppSpacing.sm),
                    child: FilterChip(
                      selected: state.isNearbyMode,
                      avatar: const Icon(Icons.my_location, size: 18),
                      label: Text('community.nearby_filter'.tr()),
                      onSelected: (_) => notifier.toggleNearbyMode(),
                    ),
                  ),
                  if (!state.isNearbyMode)
                    ...CommunityFeedNotifier.categories.map((cat) {
                      final isSelected = (state.selectedCategory == null &&
                              cat == 'all') ||
                          state.selectedCategory == cat;
                      return Padding(
                        padding: const EdgeInsetsDirectional.only(
                            end: AppSpacing.sm),
                        child: FilterChip(
                          selected: isSelected,
                          label: Text('community.cat_$cat'.tr()),
                          onSelected: (selected) {
                            notifier.setCategory(selected ? cat : null);
                          },
                        ),
                      );
                    }),
                ],
              ),
            ),
            Expanded(
              child: state.isLoading
                  ? AppLoading(message: 'common.loading'.tr())
                  : state.nearbyUnavailable
                      ? AppEmptyState(
                          icon: Icons.location_off,
                          message: 'discovery.location_required'.tr(),
                        )
                      : state.events.isEmpty
                      ? AppEmptyState(
                          icon: Icons.event_busy,
                          message: 'empty.no_activities'.tr(),
                        )
                      : ListView.separated(
                          padding:
                              const EdgeInsetsDirectional.all(AppSpacing.md),
                          itemCount: state.events.length,
                          separatorBuilder: (_, _) =>
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
                                      if (ev['startsAtUtc'] != null) ...[
                                        Row(
                                          children: [
                                            Icon(Icons.calendar_today,
                                                size: 16,
                                                color: theme.colorScheme
                                                    .onSurfaceVariant),
                                            const SizedBox(
                                                width: AppSpacing.xs),
                                            Text(
                                              DateFormat.yMMMd(
                                                      context.locale
                                                          .toString())
                                                  .add_Hm()
                                                  .format(DateTime.parse(
                                                          ev['startsAtUtc']
                                                              as String)
                                                      .toLocal()),
                                              style: theme
                                                  .textTheme.bodyMedium,
                                            ),
                                          ],
                                        ),
                                        const SizedBox(height: 4),
                                      ],
                                      if (ev['locationAddress'] != null) ...[
                                        Row(
                                          children: [
                                            Icon(Icons.location_on,
                                                size: 16,
                                                color: theme.colorScheme
                                                    .onSurfaceVariant),
                                            const SizedBox(
                                                width: AppSpacing.xs),
                                            Expanded(
                                              child: Text(
                                                ev['locationAddress']
                                                    as String,
                                                style: theme
                                                    .textTheme.bodyMedium,
                                              ),
                                            ),
                                          ],
                                        ),
                                        const SizedBox(height: AppSpacing.sm),
                                      ],
                                      if (ev['distanceKm'] != null)
                                        Container(
                                          padding:
                                              const EdgeInsets.symmetric(
                                                  horizontal: 8, vertical: 4),
                                          decoration: BoxDecoration(
                                            color: theme.colorScheme
                                                .secondaryContainer,
                                            borderRadius:
                                                BorderRadius.circular(
                                                    AppRadius.sm),
                                          ),
                                          child: Text(
                                            'community.distance_km'.tr(
                                                namedArgs: {
                                                  'km': (ev['distanceKm']
                                                          as num)
                                                      .toStringAsFixed(1)
                                                }),
                                            style: theme.textTheme.bodySmall
                                                ?.copyWith(
                                              color: theme.colorScheme
                                                  .onSecondaryContainer,
                                              fontWeight: FontWeight.w600,
                                            ),
                                          ),
                                        )
                                      else if (ev['goingCount'] != null)
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
                                            _spotsLabel(context, ev),
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

  String _spotsLabel(BuildContext context, Map<String, dynamic> ev) {
    final goingCount = ev['goingCount'] as int? ?? 0;
    final capacity = ev['capacity'] as int?;
    final waitlistCount = ev['waitlistCount'] as int? ?? 0;

    if (capacity == null) {
      return '$goingCount ${'community.attendees'.tr()}';
    }
    if (goingCount >= capacity && waitlistCount > 0) {
      return '${'community.waitlist'.tr()} ($waitlistCount)';
    }
    return '$goingCount / $capacity ${'community.spots_taken'.tr()}';
  }
}
