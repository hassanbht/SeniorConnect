// lib/features/organizations/presentation/organization_post_form_screen.dart
//
// P2-25: Staff-only create/edit/cancel form for an organization's news and
// events. Server-side org-staff authorization (see CommunityService) is the
// real gate — this screen is only reachable from OrganizationProfileScreen's
// "manage" button, which is itself only shown when /me/organizations lists
// the viewed org, so an unauthorized submit here is not a reachable path in
// normal use, only a defense-in-depth 403 if that local state goes stale.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_button.dart';
import '../application/org_post_form_notifier.dart';

class OrganizationPostFormScreen extends ConsumerStatefulWidget {
  const OrganizationPostFormScreen({
    super.key,
    required this.organizationId,
    required this.apiClient,
    this.existingPost,
  });

  final String organizationId;
  final ApiClient apiClient;

  /// When editing an existing post, its raw API map (must include `id`).
  final Map<String, dynamic>? existingPost;

  @override
  ConsumerState<OrganizationPostFormScreen> createState() =>
      _OrganizationPostFormScreenState();
}

class _OrganizationPostFormScreenState
    extends ConsumerState<OrganizationPostFormScreen> {
  final _titleController = TextEditingController();
  final _descriptionController = TextEditingController();
  final _capacityController = TextEditingController();

  bool get _isEditing => widget.existingPost != null;

  OrganizationPostFormParams get _params => OrganizationPostFormParams(
        organizationId: widget.organizationId,
        apiClient: widget.apiClient,
        existingPost: widget.existingPost,
      );

  @override
  void initState() {
    super.initState();
    final existing = widget.existingPost;
    if (existing != null) {
      _titleController.text = existing['title'] as String? ?? '';
      _descriptionController.text = existing['description'] as String? ?? '';
      final capacity = existing['capacity'];
      if (capacity != null) _capacityController.text = capacity.toString();
    }
  }

  @override
  void dispose() {
    _titleController.dispose();
    _descriptionController.dispose();
    _capacityController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final capacity = int.tryParse(_capacityController.text.trim());
    final success = await ref
        .read(organizationPostFormProvider(_params).notifier)
        .submit(
          title: _titleController.text,
          description: _descriptionController.text,
          capacity: capacity,
        );

    if (success && mounted) {
      Navigator.of(context).pop(true);
    }
  }

  Future<void> _cancelPost() async {
    final success = await ref
        .read(organizationPostFormProvider(_params).notifier)
        .cancelPost();
    if (success && mounted) {
      Navigator.of(context).pop(true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(organizationPostFormProvider(_params));
    final notifier = ref.read(organizationPostFormProvider(_params).notifier);
    final isNews = state.category == OrganizationPostCategory.news;

    return Scaffold(
      appBar: AppBar(title: Text('organizations.post_news_or_event'.tr())),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsetsDirectional.all(AppSpacing.lg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              SegmentedButton<OrganizationPostCategory>(
                segments: [
                  ButtonSegment(
                    value: OrganizationPostCategory.news,
                    label: Text('organizations.post_type_news'.tr()),
                  ),
                  ButtonSegment(
                    value: OrganizationPostCategory.event,
                    label: Text('organizations.post_type_event'.tr()),
                  ),
                ],
                selected: {state.category},
                onSelectionChanged: (selection) =>
                    notifier.setCategory(selection.first),
              ),
              const SizedBox(height: AppSpacing.lg),
              TextField(
                controller: _titleController,
                decoration: InputDecoration(
                  labelText: 'organizations.title_label'.tr(),
                ),
              ),
              const SizedBox(height: AppSpacing.md),
              TextField(
                controller: _descriptionController,
                maxLines: 4,
                decoration: InputDecoration(
                  labelText: 'organizations.description_label'.tr(),
                ),
              ),
              const SizedBox(height: AppSpacing.md),
              if (!isNews) ...[
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text('organizations.starts_at_label'.tr()),
                  subtitle: Text(state.startsAt?.toString() ?? ''),
                  trailing: const Icon(Icons.calendar_today),
                  onTap: () async {
                    final current = state.startsAt ?? DateTime.now();
                    final picked = await showDatePicker(
                      context: context,
                      initialDate: current,
                      firstDate: DateTime.now(),
                      lastDate: DateTime.now().add(const Duration(days: 730)),
                    );
                    if (picked != null) {
                      notifier.setStartsAt(picked);
                    }
                  },
                ),
                const SizedBox(height: AppSpacing.md),
                TextField(
                  controller: _capacityController,
                  keyboardType: TextInputType.number,
                  decoration: InputDecoration(
                    labelText: 'organizations.capacity_label'.tr(),
                  ),
                ),
                const SizedBox(height: AppSpacing.md),
              ],
              if (state.hasError)
                Padding(
                  padding: const EdgeInsetsDirectional.only(
                    bottom: AppSpacing.md,
                  ),
                  child: Text(
                    'error.generic'.tr(),
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
                ),
              AppButton(
                label: 'organizations.save'.tr(),
                onPressed: state.isSubmitting ? null : _submit,
                isLoading: state.isSubmitting,
              ),
              if (_isEditing) ...[
                const SizedBox(height: AppSpacing.sm),
                TextButton(
                  onPressed: state.isSubmitting ? null : _cancelPost,
                  child: Text('organizations.cancel_post'.tr()),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
