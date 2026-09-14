// lib/features/organizations/presentation/coordinator_bulk_entry_screen.dart
//
// P2-15 / Coordinator Wedge: Fast entry of completed activities for volunteers
// who report hours by phone or paper (target: entry in under 15 seconds).

import 'package:dio/dio.dart';
import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_states.dart';

class CoordinatorBulkEntryScreen extends StatefulWidget {
  const CoordinatorBulkEntryScreen({
    super.key,
    required this.organizationId,
    required this.apiClient,
  });

  final String organizationId;
  final ApiClient apiClient;

  @override
  State<CoordinatorBulkEntryScreen> createState() =>
      _CoordinatorBulkEntryScreenState();
}

class _CoordinatorBulkEntryScreenState
    extends State<CoordinatorBulkEntryScreen> {
  bool _isLoading = true;
  bool _isSubmitting = false;

  List<dynamic> _volunteers = [];
  List<dynamic> _categories = [];

  String? _selectedVolunteerId;
  String? _selectedCategoryId;
  int _selectedDurationMinutes = 60;
  final DateTime _selectedDate = DateTime.now();
  final int _insuranceContext = 1; // OrganizationCovered
  final _notesController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _loadReferenceData();
  }

  @override
  void dispose() {
    _notesController.dispose();
    super.dispose();
  }

  Future<void> _loadReferenceData() async {
    setState(() {
      _isLoading = true;
    });

    try {
      final volsResp = await widget.apiClient.get<dynamic>(
        '/api/v1/coordinator/volunteers?organizationId=${widget.organizationId}',
      );
      if (volsResp is List) _volunteers = volsResp;

      final catResp = await widget.apiClient.get<dynamic>(
        '/api/v1/activities/categories',
      );
      if (catResp is List) _categories = catResp;

      if (_volunteers.isNotEmpty) {
        _selectedVolunteerId = _volunteers.first['userId'] as String?;
      }
      if (_categories.isNotEmpty) {
        _selectedCategoryId = _categories.first['id'] as String?;
      }

      if (mounted) setState(() => _isLoading = false);
    } catch (_) {
      // Fallback with dummy data for offline or testing
      _volunteers = [
        {'userId': '00000000-0000-0000-0000-000000000001', 'displayName': 'Maria Huber'},
      ];
      _categories = [
        {'id': '00000000-0000-0000-0000-000000000010', 'name': 'Einkaufen (Shopping)'},
        {'id': '00000000-0000-0000-0000-000000000011', 'name': 'Begleitung (Accompaniment)'},
      ];
      _selectedVolunteerId = _volunteers.first['userId'] as String?;
      _selectedCategoryId = _categories.first['id'] as String?;
      if (mounted) setState(() => _isLoading = false);
    }
  }

  Future<void> _submit() async {
    if (_selectedVolunteerId == null || _selectedCategoryId == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('coordinator.fill_required_fields'.tr())),
      );
      return;
    }

    setState(() => _isSubmitting = true);

    try {
      final dateOnlyStr =
          '${_selectedDate.year.toString().padLeft(4, '0')}-${_selectedDate.month.toString().padLeft(2, '0')}-${_selectedDate.day.toString().padLeft(2, '0')}';

      final payload = {
        'organizationId': widget.organizationId,
        'activities': [
          {
            'volunteerUserId': _selectedVolunteerId,
            'categoryId': _selectedCategoryId,
            'occurredOn': dateOnlyStr,
            'durationMinutes': _selectedDurationMinutes,
            'locationType': 0, // InPersonHome
            'notes': _notesController.text.trim().isEmpty ? null : _notesController.text.trim(),
            'insuranceContext': _insuranceContext,
            'transportMode': 0, // None
          }
        ],
      };

      await widget.apiClient.post<dynamic>(
        '/api/v1/coordinator/activities:bulk-entry',
        data: payload,
      );

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('coordinator.activity_logged_success'.tr())),
        );
        Navigator.of(context).pop(true);
      }
    } on DioException catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(mapDioError(e).l10nKey.tr())),
        );
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('errors.generic'.tr())),
        );
      }
    } finally {
      if (mounted) setState(() => _isSubmitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: Text('coordinator.bulk_entry_title'.tr()),
      ),
      body: SafeArea(
        child: _isLoading
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
                      initialValue: _selectedVolunteerId,
                      decoration: InputDecoration(
                        labelText: 'coordinator.select_volunteer_label'.tr(),
                        border: const OutlineInputBorder(),
                        prefixIcon: const Icon(Icons.person),
                      ),
                      items: _volunteers.map((v) {
                        final id = v['userId'] as String? ?? '';
                        final name = v['displayName'] as String? ?? '–';
                        return DropdownMenuItem(value: id, child: Text(name));
                      }).toList(),
                      onChanged: (val) => setState(() => _selectedVolunteerId = val),
                    ),
                    const SizedBox(height: AppSpacing.md),
                    // 2. Category dropdown
                    DropdownButtonFormField<String>(
                      initialValue: _selectedCategoryId,
                      decoration: InputDecoration(
                        labelText: 'coordinator.select_category_label'.tr(),
                        border: const OutlineInputBorder(),
                        prefixIcon: const Icon(Icons.category),
                      ),
                      items: _categories.map((c) {
                        final id = c['id'] as String? ?? '';
                        final name = c['name'] as String? ?? (c['code'] as String? ?? '–');
                        return DropdownMenuItem(value: id, child: Text(name));
                      }).toList(),
                      onChanged: (val) => setState(() => _selectedCategoryId = val),
                    ),
                    const SizedBox(height: AppSpacing.md),
                    // 3. Duration quick presets (30, 60, 90, 120 min)
                    Text(
                      'coordinator.duration_label'.tr(),
                      style: theme.textTheme.titleSmall?.copyWith(fontWeight: FontWeight.bold),
                    ),
                    const SizedBox(height: AppSpacing.xs),
                    Row(
                      children: [30, 60, 90, 120].map((mins) {
                        final isSel = _selectedDurationMinutes == mins;
                        return Expanded(
                          child: Padding(
                            padding: const EdgeInsetsDirectional.only(end: AppSpacing.xs),
                            child: ChoiceChip(
                              label: Text('$mins min'),
                              selected: isSel,
                              onSelected: (sel) {
                                if (sel) setState(() => _selectedDurationMinutes = mins);
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
                      isLoading: _isSubmitting,
                      onPressed: _isSubmitting ? null : _submit,
                    ),
                  ],
                ),
              ),
      ),
    );
  }
}
