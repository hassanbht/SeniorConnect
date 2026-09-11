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

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_button.dart';

enum _PostCategory { news, event }

class OrganizationPostFormScreen extends StatefulWidget {
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
  State<OrganizationPostFormScreen> createState() =>
      _OrganizationPostFormScreenState();
}

class _OrganizationPostFormScreenState
    extends State<OrganizationPostFormScreen> {
  final _titleController = TextEditingController();
  final _descriptionController = TextEditingController();
  final _capacityController = TextEditingController();
  _PostCategory _category = _PostCategory.event;
  DateTime _startsAt = DateTime.now().add(const Duration(days: 7));
  bool _isSubmitting = false;
  bool _hasError = false;

  bool get _isEditing => widget.existingPost != null;

  @override
  void initState() {
    super.initState();
    final existing = widget.existingPost;
    if (existing != null) {
      _titleController.text = existing['title'] as String? ?? '';
      _descriptionController.text = existing['description'] as String? ?? '';
      _category = existing['category'] == 'news'
          ? _PostCategory.news
          : _PostCategory.event;
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
    setState(() {
      _isSubmitting = true;
      _hasError = false;
    });

    try {
      final category = _category == _PostCategory.news ? 'news' : 'general';
      final capacity = int.tryParse(_capacityController.text.trim());

      if (_isEditing) {
        await widget.apiClient.put<Map<String, dynamic>>(
          '/api/v1/community/events/${widget.existingPost!['id']}',
          data: {
            'title': _titleController.text.trim(),
            'description': _descriptionController.text.trim(),
            'category': category,
            'capacity': capacity,
          },
        );
      } else {
        final startsAt = _category == _PostCategory.news
            ? DateTime.now()
            : _startsAt;
        await widget.apiClient.post<Map<String, dynamic>>(
          '/api/v1/community/events',
          data: {
            'title': _titleController.text.trim(),
            'description': _descriptionController.text.trim(),
            'category': category,
            'organizationId': widget.organizationId,
            'startsAtUtc': startsAt.toUtc().toIso8601String(),
            'endsAtUtc': startsAt
                .toUtc()
                .add(const Duration(hours: 2))
                .toIso8601String(),
            'capacity': capacity,
          },
        );
      }

      if (mounted) Navigator.of(context).pop(true);
    } catch (_) {
      if (mounted) {
        setState(() {
          _hasError = true;
          _isSubmitting = false;
        });
      }
    }
  }

  Future<void> _cancelPost() async {
    final existing = widget.existingPost;
    if (existing == null) return;

    setState(() => _isSubmitting = true);
    try {
      await widget.apiClient.post<dynamic>(
        '/api/v1/community/events/${existing['id']}:cancel',
        data: {'reason': 'Cancelled by organization staff'},
      );
      if (mounted) Navigator.of(context).pop(true);
    } catch (_) {
      if (mounted) setState(() => _isSubmitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final isNews = _category == _PostCategory.news;

    return Scaffold(
      appBar: AppBar(title: Text('organizations.post_news_or_event'.tr())),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsetsDirectional.all(AppSpacing.lg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              SegmentedButton<_PostCategory>(
                segments: [
                  ButtonSegment(
                    value: _PostCategory.news,
                    label: Text('organizations.post_type_news'.tr()),
                  ),
                  ButtonSegment(
                    value: _PostCategory.event,
                    label: Text('organizations.post_type_event'.tr()),
                  ),
                ],
                selected: {_category},
                onSelectionChanged: (selection) =>
                    setState(() => _category = selection.first),
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
                  subtitle: Text(_startsAt.toString()),
                  trailing: const Icon(Icons.calendar_today),
                  onTap: () async {
                    final picked = await showDatePicker(
                      context: context,
                      initialDate: _startsAt,
                      firstDate: DateTime.now(),
                      lastDate: DateTime.now().add(const Duration(days: 730)),
                    );
                    if (picked != null) setState(() => _startsAt = picked);
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
              if (_hasError)
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
                onPressed: _isSubmitting ? null : _submit,
                isLoading: _isSubmitting,
              ),
              if (_isEditing) ...[
                const SizedBox(height: AppSpacing.sm),
                TextButton(
                  onPressed: _isSubmitting ? null : _cancelPost,
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
