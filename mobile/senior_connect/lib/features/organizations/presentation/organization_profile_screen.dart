// lib/features/organizations/presentation/organization_profile_screen.dart
//
// P2-25: Per-organization page — news and events, view-only for any
// authenticated user; staff (active Coordinator/Admin) additionally see a
// "manage" entry point into OrganizationPostFormScreen.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_states.dart';
import 'organization_post_form_screen.dart';

class OrganizationProfileScreen extends StatefulWidget {
  const OrganizationProfileScreen({
    super.key,
    required this.organizationId,
    required this.apiClient,
  });

  final String organizationId;
  final ApiClient apiClient;

  @override
  State<OrganizationProfileScreen> createState() =>
      _OrganizationProfileScreenState();
}

class _OrganizationProfileScreenState extends State<OrganizationProfileScreen> {
  bool _isLoading = true;
  Map<String, dynamic>? _organization;
  List<Map<String, dynamic>> _newsItems = [];
  List<Map<String, dynamic>> _events = [];
  bool _canManage = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _isLoading = true);
    try {
      final org = await widget.apiClient.get<Map<String, dynamic>>(
        '/api/v1/organizations/${widget.organizationId}',
      );

      final posts = await widget.apiClient.get<List<dynamic>>(
        '/api/v1/community/events',
        queryParameters: {'organizationId': widget.organizationId},
      );
      final postMaps = posts
          .map((e) => Map<String, dynamic>.from(e as Map))
          .toList();

      var canManage = false;
      try {
        final mine = await widget.apiClient.get<List<dynamic>>(
          '/api/v1/me/organizations',
        );
        canManage = mine.any(
          (m) =>
              Map<String, dynamic>.from(m as Map)['organizationId'] ==
              widget.organizationId,
        );
      } catch (_) {
        // Staff-entry-point visibility only — the server call is the real
        // gate, so a failure here just hides the button.
      }

      if (mounted) {
        setState(() {
          _organization = org;
          _newsItems = postMaps.where((e) => e['category'] == 'news').toList();
          _events = postMaps.where((e) => e['category'] != 'news').toList();
          _canManage = canManage;
          _isLoading = false;
        });
      }
    } catch (_) {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final orgName = _organization?['name'] as String? ?? '';

    return Scaffold(
      appBar: AppBar(title: Text(orgName)),
      floatingActionButton: _canManage
          ? FloatingActionButton.extended(
              onPressed: () async {
                final saved = await Navigator.of(context).push<bool>(
                  MaterialPageRoute<bool>(
                    builder: (_) => OrganizationPostFormScreen(
                      organizationId: widget.organizationId,
                      apiClient: widget.apiClient,
                    ),
                  ),
                );
                if (saved == true) _load();
              },
              icon: const Icon(Icons.add),
              label: Text('organizations.post_news_or_event'.tr()),
            )
          : null,
      body: SafeArea(
        child: _isLoading
            ? AppLoading(message: 'common.loading'.tr())
            : ListView(
                padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                children: [
                  if (_organization?['supportEmail'] != null ||
                      _organization?['supportPhone'] != null)
                    Padding(
                      padding: const EdgeInsetsDirectional.only(
                        bottom: AppSpacing.md,
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          if (_organization?['supportEmail'] != null)
                            Text(
                              _organization!['supportEmail'] as String,
                              style: theme.textTheme.bodyMedium,
                            ),
                          if (_organization?['supportPhone'] != null)
                            Text(
                              _organization!['supportPhone'] as String,
                              style: theme.textTheme.bodyMedium,
                            ),
                        ],
                      ),
                    ),
                  Text(
                    'organizations.news'.tr(),
                    style: theme.textTheme.titleLarge,
                  ),
                  const SizedBox(height: AppSpacing.sm),
                  if (_newsItems.isEmpty)
                    Padding(
                      padding: const EdgeInsetsDirectional.only(
                        bottom: AppSpacing.md,
                      ),
                      child: Text('organizations.no_posts'.tr()),
                    )
                  else
                    ..._newsItems.map((item) => _PostCard(item: item)),
                  const SizedBox(height: AppSpacing.lg),
                  Text(
                    'organizations.events'.tr(),
                    style: theme.textTheme.titleLarge,
                  ),
                  const SizedBox(height: AppSpacing.sm),
                  if (_events.isEmpty)
                    Text('organizations.no_posts'.tr())
                  else
                    ..._events.map((item) => _PostCard(item: item)),
                ],
              ),
      ),
    );
  }
}

class _PostCard extends StatelessWidget {
  const _PostCard({required this.item});

  final Map<String, dynamic> item;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final title = item['title'] as String? ?? '';
    final description = item['description'] as String? ?? '';
    final isCancelled = item['isCancelled'] as bool? ?? false;

    return Card(
      elevation: 1,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.md),
      ),
      margin: const EdgeInsetsDirectional.only(bottom: AppSpacing.sm),
      child: Padding(
        padding: const EdgeInsetsDirectional.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              title,
              style: theme.textTheme.titleMedium?.copyWith(
                decoration: isCancelled ? TextDecoration.lineThrough : null,
              ),
            ),
            if (description.isNotEmpty) ...[
              const SizedBox(height: AppSpacing.xs),
              Text(description, style: theme.textTheme.bodyMedium),
            ],
          ],
        ),
      ),
    );
  }
}
