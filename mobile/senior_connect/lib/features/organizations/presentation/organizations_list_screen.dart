// lib/features/organizations/presentation/organizations_list_screen.dart
//
// P2-25: Organization directory — entry point into each org's profile page.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../shared/widgets/app_states.dart';
import 'organization_profile_screen.dart';

class OrganizationsListScreen extends StatefulWidget {
  const OrganizationsListScreen({super.key, required this.apiClient});

  final ApiClient apiClient;

  @override
  State<OrganizationsListScreen> createState() =>
      _OrganizationsListScreenState();
}

class _OrganizationsListScreenState extends State<OrganizationsListScreen> {
  bool _isLoading = true;
  List<Map<String, dynamic>> _organizations = [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _isLoading = true);
    try {
      final response = await widget.apiClient.get<List<dynamic>>(
        '/api/v1/organizations',
      );
      if (mounted) {
        setState(() {
          _organizations = response
              .map((e) => Map<String, dynamic>.from(e as Map))
              .toList();
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

    return Scaffold(
      appBar: AppBar(title: Text('organizations.directory_title'.tr())),
      body: SafeArea(
        child: _isLoading
            ? AppLoading(message: 'common.loading'.tr())
            : _organizations.isEmpty
            ? AppEmptyState(
                icon: Icons.apartment_outlined,
                message: 'organizations.empty'.tr(),
              )
            : ListView.separated(
                padding: const EdgeInsetsDirectional.all(AppSpacing.md),
                itemCount: _organizations.length,
                separatorBuilder: (_, _) =>
                    const SizedBox(height: AppSpacing.sm),
                itemBuilder: (context, index) {
                  final org = _organizations[index];
                  final id = org['id'] as String? ?? '';
                  final name = org['name'] as String? ?? '';
                  return Card(
                    elevation: 1,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(AppRadius.md),
                    ),
                    child: ListTile(
                      title: Text(name, style: theme.textTheme.titleMedium),
                      trailing: const Icon(Icons.chevron_right),
                      onTap: id.isEmpty
                          ? null
                          : () => Navigator.of(context).push(
                              MaterialPageRoute<void>(
                                builder: (_) => OrganizationProfileScreen(
                                  organizationId: id,
                                  apiClient: widget.apiClient,
                                ),
                              ),
                            ),
                    ),
                  );
                },
              ),
      ),
    );
  }
}
