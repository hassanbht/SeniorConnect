// lib/features/help_requests/presentation/volunteer_feed_screen.dart
//
// P3-22: Volunteer opportunity feed.
//
// Constraints and Rules:
// - "In Ihrer Nähe" feed with proximity filters.
// - Contact details (phone/exact address) hidden until accepted (BR-COMM-04).
// - Ineligible cards greyed out with clear reason & path to eligibility in plain German.
// - Atomic accept handling (BR-HELP-03) with graceful 409 Conflict handling.
// - Full responsive layout and empty/loading/error states.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../core/router/app_router.dart';
import '../../../shared/widgets/app_button.dart';
import '../../../shared/widgets/app_states.dart';
import 'widgets/first_meeting_protocol_dialog.dart';

class HelpRequestFeedItem {
  const HelpRequestFeedItem({
    required this.id,
    required this.title,
    required this.categoryName,
    required this.distanceKm,
    required this.scheduledTime,
    required this.durationMinutes,
    required this.rowVersion,
    this.isEligible = true,
    this.ineligibleReason,
  });

  final String id;
  final String title;
  final String categoryName;
  final double distanceKm;
  final String scheduledTime;
  final int durationMinutes;
  final int rowVersion;
  final bool isEligible;
  final String? ineligibleReason;
}

class VolunteerFeedScreen extends StatefulWidget {
  const VolunteerFeedScreen({
    super.key,
    required this.apiClient,
  });

  final ApiClient apiClient;

  @override
  State<VolunteerFeedScreen> createState() => _VolunteerFeedScreenState();
}

class _VolunteerFeedScreenState extends State<VolunteerFeedScreen> {
  bool _isLoading = true;
  String? _errorMessage;
  List<HelpRequestFeedItem> _requests = [];
  double _maxDistance = 10.0;
  String? _acceptingId;

  @override
  void initState() {
    super.initState();
    _loadFeed();
  }

  Future<void> _loadFeed() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      // P3-22 fix: the purpose-built feed endpoint, not the raw request
      // list — it carries real distance/category/eligibility per BR-SAFETY,
      // computed server-side, never faked on the client.
      final data = await widget.apiClient.get<dynamic>(
        '/api/v1/matching/feed',
        queryParameters: {'radiusKm': _maxDistance},
      );

      if (data is! List) {
        throw const FormatException('Unexpected feed response shape');
      }

      _requests = data.map((item) {
        final m = item as Map<String, dynamic>;
        final startUtc = DateTime.tryParse(
          m['scheduledStartUtc'] as String? ?? '',
        )?.toLocal();

        return HelpRequestFeedItem(
          id: m['helpRequestId'] as String? ?? '',
          title: (m['categoryNameKey'] as String? ?? 'help.category.shopping').tr(),
          categoryName: (m['categoryNameKey'] as String? ?? 'help.category.shopping').tr(),
          distanceKm: (m['distanceKm'] as num?)?.toDouble() ?? 0,
          scheduledTime: startUtc == null
              ? ''
              : '${startUtc.day}.${startUtc.month}., ${startUtc.hour.toString().padLeft(2, '0')}:${startUtc.minute.toString().padLeft(2, '0')}',
          durationMinutes: (m['durationMinutes'] as num?)?.toInt() ?? 60,
          rowVersion: (m['rowVersion'] as num?)?.toInt() ?? 0,
          isEligible: m['isEligible'] as bool? ?? false,
          ineligibleReason: m['ineligibilityReason'] as String?,
        );
      }).toList();

      if (mounted) {
        setState(() => _isLoading = false);
      }
    } catch (_) {
      // P3-22 fix: a real failure is shown as a real error, never masked
      // behind fabricated demo cards.
      if (mounted) {
        setState(() {
          _isLoading = false;
          _errorMessage = 'errors.generic'.tr();
        });
      }
    }
  }

  Future<void> _acceptRequest(HelpRequestFeedItem item) async {
    setState(() => _acceptingId = item.id);

    try {
      await widget.apiClient.post<dynamic>(
        '/api/v1/help-requests/${item.id}:accept',
        data: {'expectedRowVersion': item.rowVersion},
      );

      if (mounted) {
        setState(() => _acceptingId = null);
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('help.status.assigned'.tr()),
            backgroundColor: Theme.of(context).colorScheme.primary,
          ),
        );
        context.push('${AppRoutes.activeAssignment}/${item.id}');
      }
    } catch (e) {
      if (mounted) {
        setState(() => _acceptingId = null);
        showDialog<void>(
          context: context,
          builder: (ctx) => AlertDialog(
            title: Text('errors.generic'.tr()),
            content: Text('errors.already_taken'.tr()),
            actions: [
              TextButton(
                onPressed: () {
                  Navigator.of(ctx).pop();
                  _loadFeed();
                },
                child: Text('common.close'.tr()),
              ),
            ],
          ),
        );
      }
    }
  }

  void _showIneligibleDialog(HelpRequestFeedItem item) {
    showDialog<void>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('trust.missing_title'.tr()),
        content: Text(item.ineligibleReason ?? ''),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(),
            child: Text('common.close'.tr()),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: Text('home.senior.my_activities'.tr()),
        actions: [
          IconButton(
            icon: const Icon(Icons.shield_outlined),
            tooltip: 'protocol.title'.tr(),
            onPressed: () => FirstMeetingProtocolDialog.show(context),
          ),
          IconButton(
            icon: const Icon(Icons.refresh),
            tooltip: 'common.retry'.tr(),
            onPressed: _loadFeed,
          ),
        ],
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 640),
            child: _buildBody(theme),
          ),
        ),
      ),
    );
  }

  Widget _buildBody(ThemeData theme) {
    if (_isLoading) {
      return AppLoading(message: 'common.loading'.tr());
    }

    if (_errorMessage != null) {
      return AppErrorView(
        message: _errorMessage!,
        retryLabel: 'common.retry'.tr(),
        onRetry: _loadFeed,
      );
    }

    if (_requests.isEmpty) {
      return AppEmptyState(
        icon: Icons.inbox_outlined,
        message: 'empty.no_requests'.tr(),
        actionLabel: 'common.retry'.tr(),
        onAction: _loadFeed,
      );
    }

    return Column(
      children: [
        // Filter bar
        Padding(
          padding: const EdgeInsetsDirectional.symmetric(
            horizontal: AppSpacing.lg,
            vertical: AppSpacing.sm,
          ),
          child: Row(
            children: [
              Icon(Icons.near_me, size: 20, color: theme.colorScheme.primary),
              const SizedBox(width: AppSpacing.sm),
              Text(
                'common.km_away'.tr(args: ['${_maxDistance.toInt()}']),
                style: theme.textTheme.labelLarge,
              ),
              Expanded(
                child: Slider(
                  value: _maxDistance,
                  min: 1,
                  max: 25,
                  divisions: 24,
                  onChanged: (v) => setState(() => _maxDistance = v),
                  onChangeEnd: (_) => _loadFeed(),
                ),
              ),
            ],
          ),
        ),
        const Divider(height: 1),

        // List of requests
        Expanded(
          child: ListView.separated(
            padding: const EdgeInsetsDirectional.all(AppSpacing.lg),
            itemCount: _requests.length,
            separatorBuilder: (_, __) => const SizedBox(height: AppSpacing.md),
            itemBuilder: (context, index) {
              final item = _requests[index];
              return _RequestCard(
                item: item,
                isAccepting: _acceptingId == item.id,
                onAccept: () => _acceptRequest(item),
                onIneligibleTap: () => _showIneligibleDialog(item),
              );
            },
          ),
        ),
      ],
    );
  }
}

class _RequestCard extends StatelessWidget {
  const _RequestCard({
    required this.item,
    required this.isAccepting,
    required this.onAccept,
    required this.onIneligibleTap,
  });

  final HelpRequestFeedItem item;
  final bool isAccepting;
  final VoidCallback onAccept;
  final VoidCallback onIneligibleTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final isEligible = item.isEligible;

    return Card(
      elevation: 0,
      color: isEligible
          ? theme.colorScheme.surfaceContainerLow
          : theme.colorScheme.surfaceContainerHighest.withValues(alpha: 0.5),
      shape: RoundedRectangleBorder(
        borderRadius: AppRadius.card,
        side: BorderSide(
          color: isEligible
              ? theme.colorScheme.outlineVariant
              : theme.colorScheme.outlineVariant.withValues(alpha: 0.4),
        ),
      ),
      child: Padding(
        padding: const EdgeInsetsDirectional.all(AppSpacing.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Container(
                  padding: const EdgeInsetsDirectional.symmetric(
                    horizontal: AppSpacing.sm,
                    vertical: AppSpacing.xs,
                  ),
                  decoration: BoxDecoration(
                    color: isEligible
                        ? theme.colorScheme.primaryContainer
                        : theme.colorScheme.surfaceContainerHigh,
                    borderRadius: AppRadius.chip,
                  ),
                  child: Text(
                    item.categoryName,
                    style: theme.textTheme.labelMedium?.copyWith(
                      color: isEligible
                          ? theme.colorScheme.onPrimaryContainer
                          : theme.colorScheme.onSurfaceVariant,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
                const Spacer(),
                Icon(Icons.location_on_outlined, size: 16, color: theme.colorScheme.outline),
                const SizedBox(width: AppSpacing.xs),
                Text(
                  'common.km_away'.tr(args: [item.distanceKm.toStringAsFixed(1)]),
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: theme.colorScheme.outline,
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.md),
            Text(
              item.title,
              style: theme.textTheme.titleMedium?.copyWith(
                fontWeight: FontWeight.bold,
                color: isEligible ? theme.colorScheme.onSurface : theme.colorScheme.outline,
              ),
            ),
            const SizedBox(height: AppSpacing.sm),
            Row(
              children: [
                Icon(Icons.access_time, size: 18, color: theme.colorScheme.onSurfaceVariant),
                const SizedBox(width: AppSpacing.xs),
                Text(
                  '${item.scheduledTime} (${item.durationMinutes} Min.)',
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: theme.colorScheme.onSurfaceVariant,
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.lg),

            if (isEligible)
              AppButton(
                label: 'help.status.offered'.tr(),
                icon: Icons.check,
                isLoading: isAccepting,
                onPressed: onAccept,
              )
            else
              OutlinedButton.icon(
                style: OutlinedButton.styleFrom(
                  minimumSize: const Size.fromHeight(48),
                ),
                icon: const Icon(Icons.info_outline),
                label: Text('trust.missing_title'.tr()),
                onPressed: onIneligibleTap,
              ),
          ],
        ),
      ),
    );
  }
}
