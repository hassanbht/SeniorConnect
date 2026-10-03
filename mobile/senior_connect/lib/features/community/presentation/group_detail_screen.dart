// lib/features/community/presentation/group_detail_screen.dart

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_states.dart';
import '../application/group_detail_notifier.dart';
import '../domain/community_group.dart';
import '../domain/community_post.dart';
import '../domain/group_enums.dart';
import 'widgets/create_post_sheet.dart';
import 'widgets/report_dialog.dart';

class GroupDetailScreen extends ConsumerWidget {
  const GroupDetailScreen({
    super.key,
    required this.groupId,
    required this.apiClient,
  });

  final String groupId;
  final ApiClient apiClient;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final params = GroupDetailParams(groupId: groupId, apiClient: apiClient);
    final state = ref.watch(groupDetailProvider(params));
    final notifier = ref.read(groupDetailProvider(params).notifier);
    final group = state.group;

    return Scaffold(
      appBar: AppBar(
        title: Text(group?.name ?? ''),
        actions: [
          if (group != null)
            IconButton(
              icon: const Icon(Icons.flag_outlined),
              tooltip: 'community.report.title'.tr(),
              onPressed: () => ReportDialog.show(
                context,
                targetType: CommunityReportTargetType.group,
                targetId: group.id,
                apiClient: apiClient,
                groupId: group.id,
              ),
            ),
        ],
      ),
      floatingActionButton: group != null && group.isActiveMember
          ? FloatingActionButton.extended(
              onPressed: () async {
                final created = await CreatePostSheet.show(
                  context,
                  groupId: groupId,
                  apiClient: apiClient,
                  canManagePosts: group.canManagePosts,
                );
                if (created != null) notifier.refreshPosts();
              },
              icon: const Icon(Icons.add),
              label: Text('community.post.new_post_title'.tr()),
            )
          : null,
      body: SafeArea(
        child: state.isLoading
            ? AppLoading(message: 'common.loading'.tr())
            : state.error != null
            ? AppErrorView(
                message: state.error!.tr(),
                retryLabel: 'common.retry'.tr(),
                onRetry: notifier.load,
              )
            : group == null
            ? AppEmptyState(
                icon: Icons.group_off_outlined,
                message: 'community.group.not_found'.tr(),
              )
            : _GroupDetailBody(
                group: group,
                posts: state.sortedPosts,
                isActionInProgress: state.isActionInProgress,
                actionError: state.actionError,
                apiClient: apiClient,
                onJoin: notifier.join,
                onLeave: notifier.leave,
              ),
      ),
    );
  }
}

class _GroupDetailBody extends StatelessWidget {
  const _GroupDetailBody({
    required this.group,
    required this.posts,
    required this.isActionInProgress,
    required this.actionError,
    required this.apiClient,
    required this.onJoin,
    required this.onLeave,
  });

  final CommunityGroup group;
  final List<CommunityPost> posts;
  final bool isActionInProgress;
  final String? actionError;
  final ApiClient apiClient;
  final VoidCallback onJoin;
  final VoidCallback onLeave;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final isPending = group.myMembershipStatus == GroupMembershipStatus.pending;

    return ListView(
      padding: EdgeInsetsDirectional.symmetric(
        horizontal: context.pagePadding,
        vertical: AppSpacing.md,
      ),
      children: [
        Text(group.description, style: theme.textTheme.bodyLarge),
        const SizedBox(height: AppSpacing.sm),
        Row(
          children: [
            Icon(
              Icons.people_outline,
              size: 18,
              color: theme.colorScheme.onSurfaceVariant,
            ),
            const SizedBox(width: AppSpacing.xs),
            Text(
              'community.group.member_count'.tr(
                namedArgs: {'count': '${group.memberCount}'},
              ),
              style: theme.textTheme.bodyMedium,
            ),
          ],
        ),
        if (group.rules.isNotEmpty) ...[
          const SizedBox(height: AppSpacing.md),
          Text(
            'community.group.rules_title'.tr(),
            style: theme.textTheme.titleMedium,
          ),
          const SizedBox(height: AppSpacing.xs),
          Text(group.rules, style: theme.textTheme.bodyMedium),
        ],
        const SizedBox(height: AppSpacing.lg),
        if (isPending)
          Text(
            'community.group.join_pending'.tr(),
            style: theme.textTheme.bodyMedium?.copyWith(
              color: theme.colorScheme.tertiary,
            ),
          )
        else
          AppButton(
            label: group.isActiveMember
                ? 'community.leave'.tr()
                : 'community.join'.tr(),
            variant: group.isActiveMember
                ? AppButtonVariant.outlined
                : AppButtonVariant.primary,
            isLoading: isActionInProgress,
            onPressed: group.isActiveMember ? onLeave : onJoin,
          ),
        if (actionError != null) ...[
          const SizedBox(height: AppSpacing.sm),
          Text(
            actionError!.tr(),
            style: theme.textTheme.bodySmall?.copyWith(
              color: theme.colorScheme.error,
            ),
          ),
        ],
        const SizedBox(height: AppSpacing.xl),
        Text(
          'community.post.feed_title'.tr(),
          style: theme.textTheme.titleLarge,
        ),
        const SizedBox(height: AppSpacing.sm),
        if (posts.isEmpty)
          Padding(
            padding: const EdgeInsetsDirectional.only(top: AppSpacing.sm),
            child: Text('community.post.empty'.tr()),
          )
        else
          ...posts.map(
            (post) =>
                _PostCard(post: post, groupId: group.id, apiClient: apiClient),
          ),
      ],
    );
  }
}

class _PostCard extends StatelessWidget {
  const _PostCard({
    required this.post,
    required this.groupId,
    required this.apiClient,
  });

  final CommunityPost post;
  final String groupId;
  final ApiClient apiClient;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Card(
      elevation: post.isPinned ? 2 : 1,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.md),
        side: post.isPinned
            ? BorderSide(color: theme.colorScheme.primary, width: 1)
            : BorderSide.none,
      ),
      margin: const EdgeInsetsDirectional.only(bottom: AppSpacing.sm),
      child: Padding(
        padding: const EdgeInsetsDirectional.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                if (post.isPinned) ...[
                  Icon(
                    Icons.push_pin,
                    size: 16,
                    color: theme.colorScheme.primary,
                  ),
                  const SizedBox(width: AppSpacing.xs),
                ],
                if (post.isAnnouncement)
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 8,
                      vertical: 2,
                    ),
                    decoration: BoxDecoration(
                      color: theme.colorScheme.primaryContainer,
                      borderRadius: BorderRadius.circular(AppRadius.sm),
                    ),
                    margin: const EdgeInsetsDirectional.only(
                      end: AppSpacing.xs,
                    ),
                    child: Text(
                      'community.post.announcement_label'.tr(),
                      style: theme.textTheme.labelSmall?.copyWith(
                        color: theme.colorScheme.onPrimaryContainer,
                      ),
                    ),
                  ),
                Expanded(
                  child: Text(post.title, style: theme.textTheme.titleMedium),
                ),
                IconButton(
                  icon: const Icon(Icons.flag_outlined, size: 20),
                  tooltip: 'community.report.title'.tr(),
                  onPressed: () => ReportDialog.show(
                    context,
                    targetType: CommunityReportTargetType.post,
                    targetId: post.id,
                    apiClient: apiClient,
                    groupId: groupId,
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.xs),
            Text(post.content, style: theme.textTheme.bodyMedium),
            if (post.isAwaitingReview) ...[
              const SizedBox(height: AppSpacing.xs),
              Text(
                'community.post.awaiting_review'.tr(),
                style: theme.textTheme.bodySmall?.copyWith(
                  color: theme.colorScheme.tertiary,
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
