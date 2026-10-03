// lib/features/community/presentation/group_list_tab.dart
//
// "Groups" tab content of CommunityFeedScreen — search, type filter, list.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_states.dart';
import '../application/group_discovery_notifier.dart';
import '../domain/community_group.dart';
import '../domain/group_enums.dart';

class GroupListTab extends ConsumerWidget {
  const GroupListTab({super.key, required this.apiClient});

  final ApiClient apiClient;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final params = GroupDiscoveryParams(apiClient: apiClient);
    final state = ref.watch(groupDiscoveryProvider(params));
    final notifier = ref.read(groupDiscoveryProvider(params).notifier);

    return Column(
      children: [
        Padding(
          padding: EdgeInsetsDirectional.symmetric(
            horizontal: context.pagePadding,
            vertical: AppSpacing.sm,
          ),
          child: TextField(
            decoration: InputDecoration(
              hintText: 'community.group.search_hint'.tr(),
              prefixIcon: const Icon(Icons.search),
              border: OutlineInputBorder(borderRadius: AppRadius.field),
              isDense: true,
            ),
            onChanged: notifier.setSearchText,
          ),
        ),
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          padding: EdgeInsetsDirectional.symmetric(
            horizontal: context.pagePadding,
          ),
          child: Row(
            children: [
              Padding(
                padding: const EdgeInsetsDirectional.only(end: AppSpacing.sm),
                child: FilterChip(
                  selected: state.type == null,
                  label: Text('community.cat_all'.tr()),
                  onSelected: (_) => notifier.setFilters(type: null),
                ),
              ),
              ...GroupType.values.map((t) {
                return Padding(
                  padding: const EdgeInsetsDirectional.only(end: AppSpacing.sm),
                  child: FilterChip(
                    selected: state.type == t,
                    label: Text('community.group.type_${t.name}'.tr()),
                    onSelected: (selected) =>
                        notifier.setFilters(type: selected ? t : null),
                  ),
                );
              }),
            ],
          ),
        ),
        const SizedBox(height: AppSpacing.sm),
        Expanded(
          child: state.isLoading
              ? AppLoading(message: 'common.loading'.tr())
              : state.error != null
              ? AppErrorView(
                  message: state.error!.tr(),
                  retryLabel: 'common.retry'.tr(),
                  onRetry: notifier.load,
                )
              : state.groups.isEmpty
              ? AppEmptyState(
                  icon: Icons.groups_outlined,
                  message: 'community.group.empty'.tr(),
                )
              : ListView.separated(
                  padding: EdgeInsetsDirectional.symmetric(
                    horizontal: context.pagePadding,
                    vertical: AppSpacing.sm,
                  ),
                  itemCount: state.groups.length,
                  separatorBuilder: (_, _) =>
                      const SizedBox(height: AppSpacing.sm),
                  itemBuilder: (context, index) =>
                      _GroupCard(group: state.groups[index]),
                ),
        ),
      ],
    );
  }
}

class _GroupCard extends StatelessWidget {
  const _GroupCard({required this.group});

  final CommunityGroup group;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Card(
      elevation: 1,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.md),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.md),
        onTap: () =>
            context.pushNamed('group-detail', pathParameters: {'id': group.id}),
        child: Padding(
          padding: const EdgeInsetsDirectional.all(AppSpacing.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                group.name,
                style: theme.textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.bold,
                ),
              ),
              const SizedBox(height: AppSpacing.xs),
              Text(
                group.description,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: theme.textTheme.bodyMedium,
              ),
              const SizedBox(height: AppSpacing.sm),
              Row(
                children: [
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 8,
                      vertical: 4,
                    ),
                    decoration: BoxDecoration(
                      color: theme.colorScheme.secondaryContainer,
                      borderRadius: BorderRadius.circular(AppRadius.sm),
                    ),
                    child: Text(
                      'community.group.type_${group.type.name}'.tr(),
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: theme.colorScheme.onSecondaryContainer,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                  const SizedBox(width: AppSpacing.sm),
                  Icon(
                    Icons.people_outline,
                    size: 16,
                    color: theme.colorScheme.onSurfaceVariant,
                  ),
                  const SizedBox(width: AppSpacing.xs),
                  Text(
                    'community.group.member_count'.tr(
                      namedArgs: {'count': '${group.memberCount}'},
                    ),
                    style: theme.textTheme.bodySmall,
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}
