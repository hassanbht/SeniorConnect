// lib/features/discovery/presentation/discovery_screen.dart
//
// P2-41: Local Proximity Discovery Views (BR-GEO-05/06) — seniors/citizens
// see nearby charities/organizations and independent volunteers; volunteers
// additionally see open help requests they could take on. Results are
// always privacy-fuzzed server-side (see ProximityResult.isFuzzed) — this
// screen never shows exact coordinates, only distance + best-available name.

import 'package:dio/dio.dart';
import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../core/router/app_router.dart';
import '../../../shared/widgets/app_states.dart';
import '../data/discovery_repository.dart';

enum _DiscoveryTab { organizations, volunteers, requests }

class DiscoveryScreen extends StatefulWidget {
  const DiscoveryScreen({super.key, required this.apiClient});

  final ApiClient apiClient;

  @override
  State<DiscoveryScreen> createState() => _DiscoveryScreenState();
}

class _DiscoveryScreenState extends State<DiscoveryScreen> {
  late final DiscoveryRepository _repository =
      DiscoveryRepositoryImpl(widget.apiClient);

  bool _isLoadingLocation = true;
  MyLocation? _myLocation;

  _DiscoveryTab _tab = _DiscoveryTab.organizations;
  double _radiusKm = 10.0;

  bool _isLoadingResults = false;
  String? _errorKey;
  List<ProximityResult>? _results;

  @override
  void initState() {
    super.initState();
    _loadLocation();
  }

  Future<void> _loadLocation() async {
    setState(() => _isLoadingLocation = true);
    try {
      final location = await _repository.getMyLocation();
      if (!mounted) return;
      setState(() {
        _myLocation = location;
        _isLoadingLocation = false;
      });
      if (location != null) _loadResults();
    } catch (_) {
      if (mounted) {
        setState(() {
          _isLoadingLocation = false;
          _errorKey = 'errors.generic';
        });
      }
    }
  }

  Future<void> _loadResults() async {
    final location = _myLocation;
    if (location == null) return;

    setState(() {
      _isLoadingResults = true;
      _errorKey = null;
    });

    try {
      final results = await switch (_tab) {
        _DiscoveryTab.organizations => _repository.getNearbyOrganizations(
            location.latitude, location.longitude, _radiusKm),
        _DiscoveryTab.volunteers => _repository.getNearbyVolunteers(
            location.latitude, location.longitude, _radiusKm),
        _DiscoveryTab.requests => _repository.getNearbyRequests(
            location.latitude, location.longitude, _radiusKm),
      };
      if (mounted) {
        setState(() {
          _results = results;
          _isLoadingResults = false;
        });
      }
    } on DioException catch (e) {
      if (mounted) {
        setState(() {
          _isLoadingResults = false;
          _errorKey = mapDioError(e).l10nKey;
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _isLoadingResults = false;
          _errorKey = 'errors.generic';
        });
      }
    }
  }

  void _selectTab(_DiscoveryTab tab) {
    if (tab == _tab) return;
    setState(() {
      _tab = tab;
      _results = null;
    });
    _loadResults();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(title: Text('discovery.title'.tr())),
      body: SafeArea(
        child: _isLoadingLocation
            ? AppLoading(message: 'common.loading'.tr())
            : _myLocation == null
                ? AppEmptyState(
                    icon: Icons.location_off_outlined,
                    message: 'discovery.location_required'.tr(),
                    actionLabel: 'discovery.set_location_action'.tr(),
                    onAction: () => context.push(AppRoutes.profileEdit),
                  )
                : Center(
                    child: ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 640),
                      child: _buildBody(theme),
                    ),
                  ),
      ),
    );
  }

  Widget _buildBody(ThemeData theme) {
    return Column(
      children: [
        // Category chips
        Padding(
          padding: const EdgeInsetsDirectional.symmetric(
            horizontal: AppSpacing.lg,
            vertical: AppSpacing.sm,
          ),
          child: Wrap(
            spacing: AppSpacing.sm,
            runSpacing: AppSpacing.sm,
            children: [
              _TabChip(
                label: 'discovery.tab_organizations'.tr(),
                selected: _tab == _DiscoveryTab.organizations,
                onSelected: () => _selectTab(_DiscoveryTab.organizations),
              ),
              _TabChip(
                label: 'discovery.tab_volunteers'.tr(),
                selected: _tab == _DiscoveryTab.volunteers,
                onSelected: () => _selectTab(_DiscoveryTab.volunteers),
              ),
              _TabChip(
                label: 'discovery.tab_requests'.tr(),
                selected: _tab == _DiscoveryTab.requests,
                onSelected: () => _selectTab(_DiscoveryTab.requests),
              ),
            ],
          ),
        ),

        // Radius slider
        Padding(
          padding: const EdgeInsetsDirectional.symmetric(
            horizontal: AppSpacing.lg,
          ),
          child: Row(
            children: [
              Icon(Icons.near_me_outlined,
                  size: 20, color: theme.colorScheme.primary),
              const SizedBox(width: AppSpacing.sm),
              Text(
                'common.km_away'.tr(args: ['${_radiusKm.toInt()}']),
                style: theme.textTheme.labelLarge,
              ),
              Expanded(
                child: Slider(
                  value: _radiusKm,
                  min: 1,
                  max: 50,
                  divisions: 49,
                  onChanged: (v) => setState(() => _radiusKm = v),
                  onChangeEnd: (_) => _loadResults(),
                ),
              ),
            ],
          ),
        ),
        const Divider(height: 1),

        Expanded(
          child: AppAsyncView<List<ProximityResult>>(
            isLoading: _isLoadingResults,
            error: _errorKey?.tr(),
            data: _results,
            isEmpty: (data) => data.isEmpty,
            loadingMessage: 'common.loading'.tr(),
            retryLabel: 'common.retry'.tr(),
            onRetry: _loadResults,
            emptyBuilder: (context) => AppEmptyState(
              icon: Icons.explore_off_outlined,
              message: switch (_tab) {
                _DiscoveryTab.organizations => 'discovery.empty_organizations'.tr(),
                _DiscoveryTab.volunteers => 'discovery.empty_volunteers'.tr(),
                _DiscoveryTab.requests => 'discovery.empty_requests'.tr(),
              },
            ),
            builder: (context, results) => ListView.separated(
              padding: const EdgeInsetsDirectional.all(AppSpacing.lg),
              itemCount: results.length,
              separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.md),
              itemBuilder: (context, index) =>
                  _ResultCard(tab: _tab, result: results[index]),
            ),
          ),
        ),
      ],
    );
  }
}

class _TabChip extends StatelessWidget {
  const _TabChip({
    required this.label,
    required this.selected,
    required this.onSelected,
  });

  final String label;
  final bool selected;
  final VoidCallback onSelected;

  @override
  Widget build(BuildContext context) {
    return ChoiceChip(
      label: Text(label),
      selected: selected,
      onSelected: (_) => onSelected(),
    );
  }
}

class _ResultCard extends StatelessWidget {
  const _ResultCard({required this.tab, required this.result});

  final _DiscoveryTab tab;
  final ProximityResult result;

  IconData get _icon => switch (tab) {
        _DiscoveryTab.organizations => Icons.apartment_outlined,
        _DiscoveryTab.volunteers => Icons.volunteer_activism_outlined,
        _DiscoveryTab.requests => Icons.handshake_outlined,
      };

  String get _fallbackName => switch (tab) {
        _DiscoveryTab.organizations => 'discovery.organization_fallback_name'.tr(),
        _DiscoveryTab.volunteers => 'discovery.volunteer_fallback_name'.tr(),
        _DiscoveryTab.requests => 'discovery.request_fallback_name'.tr(),
      };

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final name =
        result.displayName.isNotEmpty ? result.displayName : _fallbackName;
    final location = result.bestAvailableLocation;

    return Card(
      elevation: 0,
      color: theme.colorScheme.surfaceContainerLow,
      shape: RoundedRectangleBorder(
        borderRadius: AppRadius.card,
        side: BorderSide(color: theme.colorScheme.outlineVariant),
      ),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Row(
          children: [
            Icon(_icon, color: theme.colorScheme.primary),
            const SizedBox(width: AppSpacing.md),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(name, style: theme.textTheme.titleMedium),
                  const SizedBox(height: AppSpacing.xxs),
                  Text(
                    location.isNotEmpty
                        ? '${'common.km_away'.tr(args: [result.distanceKm.toStringAsFixed(1)])} · $location'
                        : 'common.km_away'.tr(args: [result.distanceKm.toStringAsFixed(1)]),
                    style: theme.textTheme.bodyMedium?.copyWith(
                      color: theme.colorScheme.onSurfaceVariant,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
