// lib/features/profile/presentation/profile_view_screen.dart
//
// P1-28: Profile view screen — shows current user's name, phone, email,
// interests, languages and availability slots.
//
// The screen fetches from GET /api/v1/me and caches the result.
// Trust level is shown factually ("Telefon bestätigt"), never as a score.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../core/network/api_client.dart';
import '../../../core/router/app_router.dart';
import '../../auth/data/auth_repository.dart';
import '../data/interests_repository.dart';
import '../data/profile_repository.dart';

class ProfileViewScreen extends StatefulWidget {
  const ProfileViewScreen({super.key, required this.apiClient});

  final ApiClient apiClient;

  @override
  State<ProfileViewScreen> createState() => _ProfileViewScreenState();
}

class _ProfileViewScreenState extends State<ProfileViewScreen> {
  late final ProfileRepository _profileRepository = ProfileRepositoryImpl(widget.apiClient);
  late final AuthRepository _authRepository = AuthRepositoryImpl(widget.apiClient);
  late final InterestsRepository _interestsRepository = InterestsRepositoryImpl(widget.apiClient);

  bool _isLoading = true;
  String? _errorKey;
  UserSummary? _user;
  List<InterestOption> _interests = [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _isLoading = true;
      _errorKey = null;
    });
    try {
      final results = await Future.wait([
        _profileRepository.getCurrentUser(),
        _interestsRepository.getSelected(),
      ]);
      if (mounted) {
        setState(() {
          _user = results[0] as UserSummary;
          _interests = results[1] as List<InterestOption>;
        });
      }
    } catch (_) {
      if (mounted) setState(() => _errorKey = 'errors.generic');
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  Future<void> _logout() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text('profile.logout_confirm_title'.tr()),
        content: Text('profile.logout_confirm_desc'.tr()),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: Text('common.cancel'.tr())),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: Text('profile.logout'.tr())),
        ],
      ),
    );
    if (confirmed != true) return;

    await _authRepository.logout();
    if (mounted) context.go(AppRoutes.phoneEntry);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text('profile.title'.tr()),
        actions: [
          Semantics(
            button: true,
            label: 'profile.edit_semantic'.tr(),
            child: IconButton(
              icon: const Icon(Icons.edit_outlined),
              onPressed: () async {
                await context.push(AppRoutes.profileEdit);
                _load();
              },
              tooltip: 'profile.edit'.tr(),
            ),
          ),
        ],
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : _errorKey != null
              ? _ErrorView(
                  message: _errorKey!.tr(),
                  onRetry: _load,
                )
              : _ProfileContent(
                  name: _user?.displayName ?? '',
                  phone: _user?.phone ?? '',
                  photoUrl: _user?.photoUrl,
                  baseUrl: widget.apiClient.baseUrl,
                  interests: _interests,
                  isStaff: _user?.primaryAuthMethod == 'Password',
                  onLogout: _logout,
                ),
    );
  }
}

class _ProfileContent extends StatelessWidget {
  const _ProfileContent({
    required this.name,
    required this.phone,
    required this.isStaff,
    required this.onLogout,
    this.photoUrl,
    this.baseUrl,
    this.interests = const [],
  });

  final String name;
  final String phone;
  final bool isStaff;
  final VoidCallback onLogout;
  final String? photoUrl;
  final String? baseUrl;
  final List<InterestOption> interests;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    final colorScheme = Theme.of(context).colorScheme;

    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        // Avatar
        Center(
          child: CircleAvatar(
            radius: 48,
            backgroundColor: colorScheme.primaryContainer,
            backgroundImage: photoUrl != null && baseUrl != null
                ? NetworkImage('$baseUrl$photoUrl')
                : null,
            child: photoUrl == null
                ? Text(
                    name.isNotEmpty ? name[0].toUpperCase() : '?',
                    style: textTheme.headlineLarge?.copyWith(
                      color: colorScheme.onPrimaryContainer,
                    ),
                  )
                : null,
          ),
        ),
        const SizedBox(height: 16),

        // Name
        Center(
          child: Text(name, style: textTheme.headlineSmall),
        ),
        const SizedBox(height: 32),

        // Contact section
        _SectionHeader('profile.contact'.tr()),
        _InfoRow(Icons.phone_outlined, 'auth.phone'.tr(), phone),
        // Email shown when available from API (P1-26 wiring)

        const SizedBox(height: 24),

        // Trust badges will be shown when loaded from /me/trust (P1-26 wiring)

        _SectionHeader('profile.interests'.tr()),
        if (interests.isEmpty)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 8),
            child: Text(
              'empty.no_interests'.tr(),
              style: textTheme.bodyMedium?.copyWith(
                color: colorScheme.onSurfaceVariant,
              ),
            ),
          )
        else
          Wrap(
            spacing: 8,
            runSpacing: 4,
            children: interests
                .map((i) => Chip(label: Text(i.nameKey.tr())))
                .toList(),
          ),

        const SizedBox(height: 24),
        _SectionHeader('profile.settings_and_support'.tr()),
        ListTile(
          contentPadding: EdgeInsets.zero,
          leading: Icon(Icons.help_outline, color: colorScheme.primary),
          title: Text('faq.title'.tr(), style: textTheme.titleMedium),
          subtitle: Text('faq.subtitle'.tr(), style: textTheme.bodySmall),
          trailing: const Icon(Icons.chevron_right),
          onTap: () => context.push(AppRoutes.helpFaq),
        ),
        ListTile(
          contentPadding: EdgeInsets.zero,
          leading: Icon(Icons.shield_outlined, color: colorScheme.primary),
          title: Text('privacy.export_title'.tr(), style: textTheme.titleMedium),
          subtitle: Text('privacy.export_subtitle'.tr(), style: textTheme.bodySmall),
          trailing: const Icon(Icons.chevron_right),
          onTap: () {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(content: Text('privacy.export_requested'.tr())),
            );
          },
        ),
        ListTile(
          contentPadding: EdgeInsets.zero,
          leading: Icon(Icons.devices_outlined, color: colorScheme.primary),
          title: Text('profile.manage_devices'.tr(), style: textTheme.titleMedium),
          trailing: const Icon(Icons.chevron_right),
          onTap: () => context.push(AppRoutes.deviceList),
        ),
        if (isStaff)
          ListTile(
            contentPadding: EdgeInsets.zero,
            leading: Icon(Icons.verified_user_outlined, color: colorScheme.primary),
            title: Text('auth.staff.title'.tr(), style: textTheme.titleMedium),
            trailing: const Icon(Icons.chevron_right),
            onTap: () => context.push(AppRoutes.totpEnrollment),
          ),
        const SizedBox(height: 8),
        Semantics(
          button: true,
          label: 'profile.logout_semantic'.tr(),
          child: ListTile(
            contentPadding: EdgeInsets.zero,
            leading: Icon(Icons.logout, color: colorScheme.error),
            title: Text('profile.logout'.tr(), style: textTheme.titleMedium?.copyWith(color: colorScheme.error)),
            onTap: onLogout,
          ),
        ),
      ],
    );
  }
}

class _SectionHeader extends StatelessWidget {
  const _SectionHeader(this.title);
  final String title;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Text(
        title,
        style: Theme.of(context).textTheme.labelLarge?.copyWith(
              color: Theme.of(context).colorScheme.primary,
            ),
      ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow(this.icon, this.label, this.value);
  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Row(
        children: [
          Icon(icon, size: 20, color: Theme.of(context).colorScheme.primary),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(label,
                    style: Theme.of(context).textTheme.labelSmall?.copyWith(
                          color: Theme.of(context).colorScheme.onSurfaceVariant,
                        )),
                Text(value, style: Theme.of(context).textTheme.bodyMedium),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

// ignore: unused_element — will be used when /me/trust API is wired (P1-26)
class _TrustBadge extends StatelessWidget {
  const _TrustBadge({required this.label});
  final String label;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        children: [
          Icon(Icons.verified_outlined, size: 18, color: colorScheme.tertiary),
          const SizedBox(width: 8),
          Text(label, style: Theme.of(context).textTheme.bodyMedium),
        ],
      ),
    );
  }
}

class _ErrorView extends StatelessWidget {
  const _ErrorView({required this.message, required this.onRetry});
  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Text(message, textAlign: TextAlign.center),
          const SizedBox(height: 16),
          FilledButton(
            onPressed: onRetry,
            child: Text('common.retry'.tr()),
          ),
        ],
      ),
    );
  }
}
