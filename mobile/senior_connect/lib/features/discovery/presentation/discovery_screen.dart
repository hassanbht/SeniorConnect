// lib/features/discovery/presentation/discovery_screen.dart
//
// P2-41: Local Proximity Discovery Views (BR-GEO-05/06) — seniors/citizens
// see nearby charities/organizations and independent volunteers; volunteers
// additionally see open help requests they could take on. Results are
// always privacy-fuzzed server-side (see ProximityResult.isFuzzed) — this
// screen never shows exact coordinates, only distance + best-available name.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../core/router/app_router.dart';
import '../../../shared/widgets/app_states.dart';
import '../application/discovery_notifier.dart';
import '../data/discovery_repository.dart';

class DiscoveryScreen extends ConsumerWidget {
  const DiscoveryScreen({super.key, required this.apiClient});

  final ApiClient apiClient;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final state = ref.watch(discoveryProvider(apiClient));
    final notifier = ref.read(discoveryProvider(apiClient).notifier);

    return Scaffold(
      appBar: AppBar(title: Text('discovery.title'.tr())),
      body: SafeArea(
        child: state.isLoadingLocation
            ? AppLoading(message: 'common.loading'.tr())
            : state.myLocation == null
                ? AppEmptyState(
                    icon: Icons.location_off_outlined,
                    message: 'discovery.location_required'.tr(),
                    actionLabel: 'discovery.set_location_action'.tr(),
                    onAction: () => context.push(AppRoutes.profileEdit),
                  )
                : Center(
                    child: ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 640),
                      child: _buildBody(theme, state, notifier),
                    ),
                  ),
      ),
    );
  }

  Widget _buildBody(
    ThemeData theme,
    DiscoveryState state,
    DiscoveryNotifier notifier,
  ) {
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
                selected: state.tab == DiscoveryTab.organizations,
                onSelected: () => notifier.setTab(DiscoveryTab.organizations),
              ),
              _TabChip(
                label: 'discovery.tab_volunteers'.tr(),
                selected: state.tab == DiscoveryTab.volunteers,
                onSelected: () => notifier.setTab(DiscoveryTab.volunteers),
              ),
              _TabChip(
                label: 'discovery.tab_requests'.tr(),
                selected: state.tab == DiscoveryTab.requests,
                onSelected: () => notifier.setTab(DiscoveryTab.requests),
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
                'common.km_away'.tr(args: ['${state.radiusKm.toInt()}']),
                style: theme.textTheme.labelLarge,
              ),
              Expanded(
                child: Slider(
                  value: state.radiusKm,
                  min: 1,
                  max: 50,
                  divisions: 49,
                  onChanged: (v) => notifier.setRadius(v),
                  onChangeEnd: (_) => notifier.loadResults(),
                ),
              ),
            ],
          ),
        ),
        const Divider(height: 1),

        Expanded(
          child: AppAsyncView<List<ProximityResult>>(
            isLoading: state.isLoadingResults,
            error: state.errorKey?.tr(),
            data: state.results,
            isEmpty: (data) => data.isEmpty,
            loadingMessage: 'common.loading'.tr(),
            retryLabel: 'common.retry'.tr(),
            onRetry: () => notifier.loadResults(),
            emptyBuilder: (context) => AppEmptyState(
              icon: Icons.explore_off_outlined,
              message: switch (state.tab) {
                DiscoveryTab.organizations =>
                  'discovery.empty_organizations'.tr(),
                DiscoveryTab.volunteers =>
                  'discovery.empty_volunteers'.tr(),
                DiscoveryTab.requests =>
                  'discovery.empty_requests'.tr(),
              },
            ),
            builder: (context, results) => ListView.separated(
              padding: const EdgeInsetsDirectional.all(AppSpacing.lg),
              itemCount: results.length,
              separatorBuilder: (_, _) =>
                  const SizedBox(height: AppSpacing.md),
              itemBuilder: (context, index) =>
                  _ResultCard(tab: state.tab, result: results[index]),
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

  final DiscoveryTab tab;
  final ProximityResult result;

  IconData get _icon => switch (tab) {
        DiscoveryTab.organizations => Icons.apartment_outlined,
        DiscoveryTab.volunteers => Icons.volunteer_activism_outlined,
        DiscoveryTab.requests => Icons.handshake_outlined,
      };

  String get _fallbackName => switch (tab) {
        DiscoveryTab.organizations =>
          'discovery.organization_fallback_name'.tr(),
        DiscoveryTab.volunteers => 'discovery.volunteer_fallback_name'.tr(),
        DiscoveryTab.requests => 'discovery.request_fallback_name'.tr(),
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
