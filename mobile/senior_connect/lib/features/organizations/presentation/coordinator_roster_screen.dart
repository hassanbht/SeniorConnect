// lib/features/organizations/presentation/coordinator_roster_screen.dart
//
// P2-27 / BR-ROSTER-01: Volunteer Roster with behavioral status classification
// (Active, Dormant, Inactive, NeverActivated) and one-tap reactivation (P2-19).

import 'package:dio/dio.dart';
import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_states.dart';
import '../../../shared/widgets/app_status.dart';

class CoordinatorRosterScreen extends StatefulWidget {
  const CoordinatorRosterScreen({
    super.key,
    required this.organizationId,
    required this.apiClient,
  });

  final String organizationId;
  final ApiClient apiClient;

  @override
  State<CoordinatorRosterScreen> createState() => _CoordinatorRosterScreenState();
}

class _CoordinatorRosterScreenState extends State<CoordinatorRosterScreen> {
  bool _isLoading = true;
  String? _errorMessage;
  List<dynamic> _allVolunteers = [];
  String _selectedStatusFilter = 'all';
  String _searchQuery = '';

  @override
  void initState() {
    super.initState();
    _loadRoster();
  }

  Future<void> _loadRoster() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final resp = await widget.apiClient.get<dynamic>(
        '/api/v1/coordinator/volunteers?organizationId=${widget.organizationId}',
      );

      if (resp is List) {
        _allVolunteers = resp;
      }

      if (mounted) setState(() => _isLoading = false);
    } on DioException catch (e) {
      if (mounted) {
        setState(() {
          _isLoading = false;
          _errorMessage = mapDioError(e).l10nKey.tr();
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _isLoading = false;
          _errorMessage = 'errors.generic'.tr();
        });
      }
    }
  }

  Future<void> _reactivate(String volunteerUserId) async {
    try {
      await widget.apiClient.post<dynamic>(
        '/api/v1/coordinator/volunteers/$volunteerUserId:reactivate',
        data: {'notes': 'coordinator.reactivated_by_coordinator'.tr()},
      );

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('coordinator.volunteer_reactivated'.tr())),
        );
        _loadRoster();
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
    }
  }

  List<dynamic> get _filteredVolunteers {
    return _allVolunteers.where((v) {
      final m = v as Map<String, dynamic>;
      final status = m['rosterStatus'] as String? ?? '';
      final name = (m['displayName'] as String? ?? '').toLowerCase();
      final phone = (m['phone'] as String? ?? '').toLowerCase();

      final matchesStatus = _selectedStatusFilter == 'all' ||
          status.toLowerCase() == _selectedStatusFilter.toLowerCase();
      final matchesSearch = _searchQuery.isEmpty ||
          name.contains(_searchQuery.toLowerCase()) ||
          phone.contains(_searchQuery.toLowerCase());

      return matchesStatus && matchesSearch;
    }).toList();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: Text('coordinator.roster_title'.tr()),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            tooltip: 'common.retry'.tr(),
            onPressed: _loadRoster,
          ),
        ],
      ),
      body: SafeArea(
        child: _isLoading
            ? AppLoading(message: 'common.loading'.tr())
            : _errorMessage != null
                ? AppErrorView(
                    message: _errorMessage!,
                    retryLabel: 'common.retry'.tr(),
                    onRetry: _loadRoster,
                  )
                : Column(
                    children: [
                      Padding(
                        padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                        child: Column(
                          children: [
                            TextField(
                              decoration: InputDecoration(
                                prefixIcon: const Icon(Icons.search),
                                hintText: 'coordinator.search_volunteers_hint'.tr(),
                                border: OutlineInputBorder(
                                  borderRadius: BorderRadius.circular(AppRadius.md),
                                ),
                                isDense: true,
                              ),
                              onChanged: (val) => setState(() => _searchQuery = val.trim()),
                            ),
                            const SizedBox(height: AppSpacing.sm),
                            SingleChildScrollView(
                              scrollDirection: Axis.horizontal,
                              child: Row(
                                children: [
                                  _filterChip('all', 'coordinator.status_all'.tr()),
                                  const SizedBox(width: AppSpacing.xs),
                                  _filterChip('Active', 'coordinator.status_active'.tr()),
                                  const SizedBox(width: AppSpacing.xs),
                                  _filterChip('Dormant', 'coordinator.status_dormant'.tr()),
                                  const SizedBox(width: AppSpacing.xs),
                                  _filterChip('Inactive', 'coordinator.status_inactive'.tr()),
                                  const SizedBox(width: AppSpacing.xs),
                                  _filterChip('NeverActivated', 'coordinator.status_never_activated'.tr()),
                                ],
                              ),
                            ),
                          ],
                        ),
                      ),
                      const Divider(height: 1),
                      Expanded(
                        child: _filteredVolunteers.isEmpty
                            ? AppEmptyState(
                                icon: Icons.people_outline,
                                message: 'coordinator.no_volunteers_found'.tr(),
                              )
                            : ListView.separated(
                                padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                                itemCount: _filteredVolunteers.length,
                                separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.sm),
                                itemBuilder: (context, index) {
                                  final v = _filteredVolunteers[index] as Map<String, dynamic>;
                                  return _VolunteerCard(
                                    volunteer: v,
                                    theme: theme,
                                    onReactivate: () => _reactivate(v['userId'] as String),
                                  );
                                },
                              ),
                      ),
                    ],
                  ),
      ),
    );
  }

  Widget _filterChip(String filterKey, String label) {
    final isSelected = _selectedStatusFilter == filterKey;
    return ChoiceChip(
      label: Text(label),
      selected: isSelected,
      onSelected: (selected) {
        if (selected) setState(() => _selectedStatusFilter = filterKey);
      },
    );
  }
}

class _VolunteerCard extends StatelessWidget {
  const _VolunteerCard({
    required this.volunteer,
    required this.theme,
    required this.onReactivate,
  });

  final Map<String, dynamic> volunteer;
  final ThemeData theme;
  final VoidCallback onReactivate;

  @override
  Widget build(BuildContext context) {
    final name = volunteer['displayName'] as String? ?? '–';
    final phone = volunteer['phone'] as String?;
    final email = volunteer['email'] as String?;
    final status = volunteer['rosterStatus'] as String? ?? 'NeverActivated';
    final totalHours = volunteer['totalHoursLogged'] as int? ?? 0;
    final lastDate = volunteer['lastActivityDate'] as String?;

    AppStatusTone chipTone = AppStatusTone.neutral;
    if (status == 'Active') chipTone = AppStatusTone.success;
    if (status == 'Dormant') chipTone = AppStatusTone.pending;
    if (status == 'Inactive') chipTone = AppStatusTone.error;

    final isReactivatable = status == 'Dormant' || status == 'Inactive';

    return Card(
      child: Padding(
        padding: const EdgeInsetsDirectional.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Expanded(
                  child: Text(
                    name,
                    style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold),
                  ),
                ),
                AppStatusChip(tone: chipTone, label: status),
              ],
            ),
            const SizedBox(height: AppSpacing.xs),
            if (phone != null) ...[
              Row(
                children: [
                  const Icon(Icons.phone_outlined, size: 16),
                  const SizedBox(width: AppSpacing.xs),
                  Text(phone, style: theme.textTheme.bodySmall),
                ],
              ),
              const SizedBox(height: 2),
            ],
            if (email != null) ...[
              Row(
                children: [
                  const Icon(Icons.email_outlined, size: 16),
                  const SizedBox(width: AppSpacing.xs),
                  Text(email, style: theme.textTheme.bodySmall),
                ],
              ),
              const SizedBox(height: 2),
            ],
            const SizedBox(height: AppSpacing.sm),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  'coordinator.hours_logged'.tr(args: ['$totalHours']),
                  style: theme.textTheme.bodyMedium?.copyWith(fontWeight: FontWeight.w600),
                ),
                Text(
                  lastDate != null
                      ? 'coordinator.last_active'.tr(args: [lastDate])
                      : 'coordinator.never_active'.tr(),
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: theme.colorScheme.onSurfaceVariant,
                  ),
                ),
              ],
            ),
            if (isReactivatable) ...[
              const SizedBox(height: AppSpacing.sm),
              Align(
                alignment: AlignmentDirectional.centerEnd,
                child: AppButton(
                  label: 'coordinator.reactivate_action'.tr(),
                  icon: Icons.refresh,
                  variant: AppButtonVariant.tonal,
                  onPressed: onReactivate,
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
