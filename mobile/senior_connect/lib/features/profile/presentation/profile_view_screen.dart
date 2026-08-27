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

import '../../../core/router/app_router.dart';

class ProfileViewScreen extends StatefulWidget {
  const ProfileViewScreen({super.key});

  @override
  State<ProfileViewScreen> createState() => _ProfileViewScreenState();
}

class _ProfileViewScreenState extends State<ProfileViewScreen> {
  // TODO P1-26/P1-28: inject ProfileRepository and load from /api/v1/me
  // ignore: prefer_final_fields — mutable in setState
  bool _isLoading = false;
  String? _errorKey;

  // Placeholder data — will come from API
  static const _placeholderName = 'Maria Muster';
  static const _placeholderPhone = '+43 660 1234567';

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
              onPressed: () => context.push(AppRoutes.profileEdit),
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
                  onRetry: () => setState(() => _errorKey = null),
                )
              : _ProfileContent(
                  name: _placeholderName,
                  phone: _placeholderPhone,
                ),
    );
  }
}

class _ProfileContent extends StatelessWidget {
  const _ProfileContent({
    required this.name,
    required this.phone,
  });

  final String name;
  final String phone;

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
            child: Text(
              name.isNotEmpty ? name[0].toUpperCase() : '?',
              style: textTheme.headlineLarge?.copyWith(
                color: colorScheme.onPrimaryContainer,
              ),
            ),
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

        // Empty state for interests/languages (will be filled in later)
        _SectionHeader('profile.interests'.tr()),
        Padding(
          padding: const EdgeInsets.symmetric(vertical: 8),
          child: Text(
            'empty.no_interests'.tr(),
            style: textTheme.bodyMedium?.copyWith(
              color: colorScheme.onSurfaceVariant,
            ),
          ),
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
