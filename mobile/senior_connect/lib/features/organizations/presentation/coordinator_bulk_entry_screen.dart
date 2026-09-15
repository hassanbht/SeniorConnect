// lib/features/organizations/presentation/coordinator_bulk_entry_screen.dart
//
// P2-15 / Coordinator Wedge: Fast entry of completed activities for volunteers
// who report hours by phone or paper (target: entry in under 15 seconds).

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_states.dart';
import '../application/coordinator_bulk_entry_notifier.dart';

class CoordinatorBulkEntryScreen extends ConsumerStatefulWidget {
  const CoordinatorBulkEntryScreen({
    super.key,
    required this.organizationId,
    required this.apiClient,
  });

  final String organizationId;
  final ApiClient apiClient;

  @override
  ConsumerState<CoordinatorBulkEntryScreen> createState() =>
      _CoordinatorBulkEntryScreenState();
}

class _CoordinatorBulkEntryScreenState
    extends ConsumerState<CoordinatorBulkEntryScreen> {
  final _notesController = TextEditingController();

  CoordinatorBulkEntryParams get _params => CoordinatorBulkEntryParams(
        organizationId: widget.organizationId,
        apiClient: widget.apiClient,
      );

  @override
  void dispose() {
    _notesController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final error = await ref
        .read(coordinatorBulkEntryProvider(_params).notifier)
        .submit(notes: _notesController.text);

    if (!mounted) return;

    if (error == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('coordinator.activity_logged_success'.tr())),
      );
      Navigator.of(context).pop(true);
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(error.tr())),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final state = ref.watch(coordinatorBulkEntryProvider(_params));
    final notifier =
        ref.read(coordinatorBulkEntryProvider(_params).notifier);

    return Scaffold(
      appBar: AppBar(
        title: Text('coordinator.bulk_entry_title'.tr()),
      ),
      body: SafeArea(
        child: state.isLoading
            ? AppLoading(message: 'common.loading'.tr())
            : SingleChildScrollView(
                padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text(
                      'coordinator.bulk_entry_subtitle'.tr(),
                      style: theme.textTheme.bodyMedium?.copyWith(
                        color: theme.colorScheme.onSurfaceVariant,
                      ),
                    ),
                    const SizedBox(height: AppSpacing.md),
                    // 1. Volunteer dropdown
                    DropdownButtonFormField<String>(
                      initialValue: state.selectedVolunteerId,
                      decoration: InputDecoration(
                        labelText: 'coordinator.select_volunteer_label'.tr(),
                        border: const OutlineInputBorder(),
                        prefixIcon: const Icon(Icons.person),
                      ),
                      items: state.volunteers.map((v) {
                        final id = v['userId'] as String? ?? '';
                        final name = v['displayName'] as String? ?? '–';
                        return DropdownMenuItem(value: id, child: Text(name));
                      }).toList(),
                      onChanged: (val) => notifier.setSelectedVolunteer(val),
                    ),
                    const SizedBox(height: AppSpacing.md),
                    // 2. Category dropdown
                    DropdownButtonFormField<String>(
                      initialValue: state.selectedCategoryId,
                      decoration: InputDecoration(
                        labelText: 'coordinator.select_category_label'.tr(),
                        border: const OutlineInputBorder(),
                        prefixIcon: const Icon(Icons.category),
                      ),
                      items: state.categories.map((c) {
                        final id = c['id'] as String? ?? '';
                        final name = c['name'] as String? ??
                            (c['code'] as String? ?? '–');
                        return DropdownMenuItem(value: id, child: Text(name));
                      }).toList(),
                      onChanged: (val) => notifier.setSelectedCategory(val),
                    ),
                    const SizedBox(height: AppSpacing.md),
                    // 3. Duration quick presets (30, 60, 90, 120 min)
                    Text(
                      'coordinator.duration_label'.tr(),
                      style: theme.textTheme.titleSmall?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    const SizedBox(height: AppSpacing.xs),
                    Row(
                      children: [30, 60, 90, 120].map((mins) {
                        final isSel = state.selectedDurationMinutes == mins;
                        return Expanded(
                          child: Padding(
                            padding: const EdgeInsetsDirectional.only(
                              end: AppSpacing.xs,
                            ),
                            child: ChoiceChip(
                              label: Text('$mins min'),
                              selected: isSel,
                              onSelected: (sel) {
                                if (sel) notifier.setSelectedDuration(mins);
                              },
                            ),
                          ),
                        );
                      }).toList(),
                    ),
                    const SizedBox(height: AppSpacing.md),
                    // 4. Notes
                    TextField(
                      controller: _notesController,
                      decoration: InputDecoration(
                        labelText: 'coordinator.notes_label'.tr(),
                        border: const OutlineInputBorder(),
                        prefixIcon: const Icon(Icons.note_alt_outlined),
                      ),
                      maxLines: 2,
                    ),
                    const SizedBox(height: AppSpacing.xl),
                    // Submit button
                    AppButton(
                      label: 'coordinator.log_activity_submit'.tr(),
                      icon: Icons.check_circle_outline,
                      isLoading: state.isSubmitting,
                      onPressed: state.isSubmitting ? null : _submit,
                    ),
                  ],
                ),
              ),
      ),
    );
  }
}
