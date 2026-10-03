// lib/features/community/presentation/widgets/create_post_sheet.dart
//
// Bottom sheet for posting into a group — title, content, and (owner/admin/
// moderator only) an "announcement" toggle that also pins the post.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/design_system/app_tokens.dart';
import '../../../../core/network/api_client.dart';
import '../../../../shared/widgets/app_button.dart';
import '../../application/create_post_notifier.dart';
import '../../domain/community_post.dart';

class CreatePostSheet extends ConsumerStatefulWidget {
  const CreatePostSheet({
    super.key,
    required this.groupId,
    required this.apiClient,
    required this.canManagePosts,
  });

  final String groupId;
  final ApiClient apiClient;

  /// Whether the current user is Owner/Admin/Moderator — gates the
  /// announcement toggle. The backend re-checks this; this only decides
  /// whether the control is shown at all.
  final bool canManagePosts;

  /// Returns the created post, or null if the sheet was dismissed.
  static Future<CommunityPost?> show(
    BuildContext context, {
    required String groupId,
    required ApiClient apiClient,
    required bool canManagePosts,
  }) {
    return showModalBottomSheet<CommunityPost>(
      context: context,
      isScrollControlled: true,
      builder: (_) => CreatePostSheet(
        groupId: groupId,
        apiClient: apiClient,
        canManagePosts: canManagePosts,
      ),
    );
  }

  @override
  ConsumerState<CreatePostSheet> createState() => _CreatePostSheetState();
}

class _CreatePostSheetState extends ConsumerState<CreatePostSheet> {
  final _titleController = TextEditingController();
  final _contentController = TextEditingController();

  @override
  void dispose() {
    _titleController.dispose();
    _contentController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final title = _titleController.text.trim();
    final content = _contentController.text.trim();
    if (title.isEmpty || content.isEmpty) return;

    final params = CreatePostParams(
      groupId: widget.groupId,
      apiClient: widget.apiClient,
    );
    final post = await ref
        .read(createPostProvider(params).notifier)
        .submit(title: title, content: content);
    if (post != null && mounted) {
      Navigator.of(context).pop(post);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final params = CreatePostParams(
      groupId: widget.groupId,
      apiClient: widget.apiClient,
    );
    final state = ref.watch(createPostProvider(params));
    final notifier = ref.read(createPostProvider(params).notifier);

    return Padding(
      padding: EdgeInsetsDirectional.only(
        start: AppSpacing.lg,
        end: AppSpacing.lg,
        top: AppSpacing.lg,
        bottom: AppSpacing.lg + MediaQuery.viewInsetsOf(context).bottom,
      ),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              'community.post.new_post_title'.tr(),
              style: theme.textTheme.headlineSmall,
            ),
            const SizedBox(height: AppSpacing.lg),
            TextField(
              controller: _titleController,
              decoration: InputDecoration(
                labelText: 'community.post.title_label'.tr(),
                border: const OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: AppSpacing.md),
            TextField(
              controller: _contentController,
              maxLines: 5,
              decoration: InputDecoration(
                labelText: 'community.post.content_label'.tr(),
                border: const OutlineInputBorder(),
              ),
            ),
            if (widget.canManagePosts) ...[
              const SizedBox(height: AppSpacing.sm),
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                value: state.isAnnouncement,
                onChanged: notifier.setIsAnnouncement,
                title: Text('community.post.announcement_toggle'.tr()),
                subtitle: Text('community.post.announcement_hint'.tr()),
              ),
            ],
            if (state.error != null) ...[
              const SizedBox(height: AppSpacing.sm),
              Text(
                state.error!.tr(),
                style: theme.textTheme.bodySmall?.copyWith(
                  color: theme.colorScheme.error,
                ),
              ),
            ],
            const SizedBox(height: AppSpacing.lg),
            AppButton(
              label: 'community.post.publish'.tr(),
              isLoading: state.isSubmitting,
              onPressed: _submit,
            ),
          ],
        ),
      ),
    );
  }
}
