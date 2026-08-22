// lib/features/profile/presentation/profile_edit_screen.dart
//
// P1-28: Profile edit screen — lets the user update their display name
// and email. Interests, languages and availability will be added in sub-steps.
//
// PATCH /api/v1/me — only changed fields are sent (partial update).

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';

class ProfileEditScreen extends StatefulWidget {
  const ProfileEditScreen({super.key});

  @override
  State<ProfileEditScreen> createState() => _ProfileEditScreenState();
}

class _ProfileEditScreenState extends State<ProfileEditScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController(text: 'Maria Muster'); // pre-filled from /me
  final _emailController = TextEditingController();
  bool _isSaving = false;
  String? _errorKey;

  @override
  void dispose() {
    _nameController.dispose();
    _emailController.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() {
      _isSaving = true;
      _errorKey = null;
    });

    try {
      // TODO P1-26/P1-28: call profileRepository.updateProfile(name, email)
      // PATCH /api/v1/me with only changed fields
      if (mounted) Navigator.of(context).pop();
    } catch (_) {
      setState(() => _errorKey = 'errors.generic');
    } finally {
      if (mounted) setState(() => _isSaving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    final colorScheme = Theme.of(context).colorScheme;

    return Scaffold(
      appBar: AppBar(
        title: Text('profile.edit'.tr()),
        actions: [
          Semantics(
            button: true,
            label: 'common.save'.tr(),
            child: TextButton(
              onPressed: _isSaving ? null : _save,
              child: _isSaving
                  ? const SizedBox(
                      width: 20,
                      height: 20,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : Text('common.save'.tr()),
            ),
          ),
        ],
      ),
      body: Form(
        key: _formKey,
        child: ListView(
          padding: const EdgeInsets.all(24),
          children: [
            // Display name
            TextFormField(
              controller: _nameController,
              textCapitalization: TextCapitalization.words,
              decoration: InputDecoration(
                labelText: 'profile.display_name'.tr(),
                border: const OutlineInputBorder(),
              ),
              validator: (value) {
                if (value == null || value.trim().isEmpty) {
                  return 'profile.name_required'.tr();
                }
                return null;
              },
            ),

            const SizedBox(height: 16),

            // Email (optional)
            TextFormField(
              controller: _emailController,
              keyboardType: TextInputType.emailAddress,
              decoration: InputDecoration(
                labelText: 'profile.email'.tr(),
                hintText: 'muster@example.at',
                border: const OutlineInputBorder(),
                helperText: 'profile.email_optional'.tr(),
              ),
              validator: (value) {
                if (value == null || value.isEmpty) return null; // optional
                final emailRegex = RegExp(r'^[^@]+@[^@]+\.[^@]+$');
                if (!emailRegex.hasMatch(value)) {
                  return 'profile.email_invalid'.tr();
                }
                return null;
              },
            ),

            if (_errorKey != null) ...[
              const SizedBox(height: 16),
              Text(
                _errorKey!.tr(),
                style: TextStyle(color: colorScheme.error),
              ),
            ],

            const SizedBox(height: 32),

            // Interests placeholder
            Text('profile.interests'.tr(),
                style: textTheme.titleMedium),
            const SizedBox(height: 8),
            Text(
              'profile.interests_coming_soon'.tr(),
              style: textTheme.bodyMedium?.copyWith(
                color: colorScheme.onSurfaceVariant,
              ),
            ),

            const SizedBox(height: 24),

            // Languages placeholder
            Text('profile.languages'.tr(),
                style: textTheme.titleMedium),
            const SizedBox(height: 8),
            Text(
              'profile.languages_coming_soon'.tr(),
              style: textTheme.bodyMedium?.copyWith(
                color: colorScheme.onSurfaceVariant,
              ),
            ),

            const SizedBox(height: 32),

            // Save button (large, accessible)
            SizedBox(
              height: 56,
              child: FilledButton(
                onPressed: _isSaving ? null : _save,
                child: Text('common.save'.tr()),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
