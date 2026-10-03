// lib/features/community/presentation/create_group_screen.dart

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_button.dart';
import '../application/create_group_notifier.dart';
import '../domain/group_enums.dart';

class CreateGroupScreen extends ConsumerStatefulWidget {
  const CreateGroupScreen({super.key, required this.apiClient});

  final ApiClient apiClient;

  @override
  ConsumerState<CreateGroupScreen> createState() => _CreateGroupScreenState();
}

class _CreateGroupScreenState extends ConsumerState<CreateGroupScreen> {
  final _nameController = TextEditingController();
  final _descriptionController = TextEditingController();
  final _cityController = TextEditingController();
  final _regionController = TextEditingController();
  final _languageController = TextEditingController();

  @override
  void dispose() {
    _nameController.dispose();
    _descriptionController.dispose();
    _cityController.dispose();
    _regionController.dispose();
    _languageController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final name = _nameController.text.trim();
    final description = _descriptionController.text.trim();
    if (name.isEmpty || description.isEmpty) return;

    final group = await ref
        .read(createGroupProvider(widget.apiClient).notifier)
        .submit(
          name: name,
          description: description,
          city: _cityController.text,
          region: _regionController.text,
          primaryLanguageCode: _languageController.text,
        );

    if (group != null && mounted) {
      context.pushReplacementNamed(
        'group-detail',
        pathParameters: {'id': group.id},
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final state = ref.watch(createGroupProvider(widget.apiClient));
    final notifier = ref.read(createGroupProvider(widget.apiClient).notifier);

    return Scaffold(
      appBar: AppBar(title: Text('community.group.create_title'.tr())),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: EdgeInsetsDirectional.symmetric(
            horizontal: context.pagePadding,
            vertical: AppSpacing.lg,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              TextField(
                controller: _nameController,
                decoration: InputDecoration(
                  labelText: 'community.group.name_label'.tr(),
                  border: const OutlineInputBorder(),
                ),
              ),
              const SizedBox(height: AppSpacing.md),
              TextField(
                controller: _descriptionController,
                maxLines: 4,
                decoration: InputDecoration(
                  labelText: 'community.group.description_label'.tr(),
                  border: const OutlineInputBorder(),
                ),
              ),
              const SizedBox(height: AppSpacing.md),
              DropdownButtonFormField<GroupType>(
                initialValue: state.type,
                isExpanded: true,
                decoration: InputDecoration(
                  labelText: 'community.group.type_label'.tr(),
                  border: const OutlineInputBorder(),
                ),
                items: GroupType.values
                    .map(
                      (t) => DropdownMenuItem(
                        value: t,
                        child: Text(
                          'community.group.type_${t.name}'.tr(),
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                    )
                    .toList(),
                onChanged: (value) {
                  if (value != null) notifier.setType(value);
                },
              ),
              const SizedBox(height: AppSpacing.md),
              Text(
                'community.group.visibility_label'.tr(),
                style: theme.textTheme.titleSmall,
              ),
              const SizedBox(height: AppSpacing.xs),
              SegmentedButton<GroupVisibility>(
                segments: [
                  ButtonSegment(
                    value: GroupVisibility.public,
                    label: Text('community.group.visibility_public'.tr()),
                  ),
                  ButtonSegment(
                    value: GroupVisibility.private,
                    label: Text('community.group.visibility_private'.tr()),
                  ),
                ],
                selected: {
                  state.visibility == GroupVisibility.organizationPrivate
                      ? GroupVisibility.private
                      : state.visibility,
                },
                onSelectionChanged: (selection) =>
                    notifier.setVisibility(selection.first),
              ),
              const SizedBox(height: AppSpacing.lg),
              Text(
                'community.group.location_section'.tr(),
                style: theme.textTheme.titleMedium,
              ),
              const SizedBox(height: AppSpacing.sm),
              TextField(
                controller: _cityController,
                decoration: InputDecoration(
                  labelText: 'community.group.city_label'.tr(),
                  border: const OutlineInputBorder(),
                ),
              ),
              const SizedBox(height: AppSpacing.md),
              TextField(
                controller: _regionController,
                decoration: InputDecoration(
                  labelText: 'community.group.region_label'.tr(),
                  border: const OutlineInputBorder(),
                ),
              ),
              const SizedBox(height: AppSpacing.md),
              TextField(
                controller: _languageController,
                decoration: InputDecoration(
                  labelText: 'community.group.language_label'.tr(),
                  border: const OutlineInputBorder(),
                ),
              ),
              if (state.error != null) ...[
                const SizedBox(height: AppSpacing.md),
                Text(
                  state.error!.tr(),
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: theme.colorScheme.error,
                  ),
                ),
              ],
              const SizedBox(height: AppSpacing.xl),
              AppButton(
                label: 'community.group.create_submit'.tr(),
                isLoading: state.isSubmitting,
                onPressed: _submit,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
