// lib/features/profile/presentation/profile_edit_screen.dart
//
// P1-28: Profile edit screen with Austrian address cascade, map lookup,
// and in-profile phone verification.
//
// Features:
//  - Basic info (name, email)
//  - Mobile phone with "Verify Phone Number" button
//  - Austrian address: cascading dropdowns (Bundesland → Bezirk → Gemeinde → PLZ/Ort)
//  - "Lookup Address on Map" button with interactive map preview
//  - Coordinates persisted to profile
//
// Accessibility:
//  - 64dp touch targets (72dp in Senior Mode)
//  - Semantic labels on all interactive elements
//  - RTL mirroring for Persian
//  - Text scaling 1.0 / 1.5 / 2.0 without overflow
//  - Light + Dark + High Contrast support

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:image_picker/image_picker.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../core/router/app_router.dart';
import '../application/profile_edit_notifier.dart';

class ProfileEditScreen extends ConsumerStatefulWidget {
  const ProfileEditScreen({super.key, required this.apiClient});

  final ApiClient apiClient;

  @override
  ConsumerState<ProfileEditScreen> createState() => _ProfileEditScreenState();
}

class _ProfileEditScreenState extends ConsumerState<ProfileEditScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _emailController = TextEditingController();
  final _phoneController = TextEditingController();
  final _streetController = TextEditingController();
  final _plzController = TextEditingController();
  final _cityController = TextEditingController();

  bool _initialized = false;

  @override
  void dispose() {
    _nameController.dispose();
    _emailController.dispose();
    _phoneController.dispose();
    _streetController.dispose();
    _plzController.dispose();
    _cityController.dispose();
    super.dispose();
  }

  void _syncUserToControllers(ProfileEditState state) {
    if (!_initialized && state.user != null) {
      _nameController.text = state.user!.displayName;
      _emailController.text = state.user!.email ?? '';
      _phoneController.text = state.user!.phone ?? '';
      _initialized = true;
    }
  }

  Future<void> _pickAndUploadPhoto() async {
    final source = await showModalBottomSheet<ImageSource>(
      context: context,
      builder: (sheetContext) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: const Icon(Icons.photo_camera_outlined),
              title: Text('profile.take_photo'.tr()),
              onTap: () => Navigator.pop(sheetContext, ImageSource.camera),
            ),
            ListTile(
              leading: const Icon(Icons.photo_library_outlined),
              title: Text('profile.choose_from_gallery'.tr()),
              onTap: () => Navigator.pop(sheetContext, ImageSource.gallery),
            ),
          ],
        ),
      ),
    );
    if (source == null || !mounted) return;

    final picked = await ImagePicker().pickImage(
      source: source,
      imageQuality: 85,
    );
    if (picked == null || !mounted) return;

    await ref
        .read(profileEditProvider(widget.apiClient).notifier)
        .uploadPhoto(picked.path);
  }

  void _onGemeindeChanged(String? code) {
    final notifier = ref.read(profileEditProvider(widget.apiClient).notifier);
    final gemeinde = notifier.onGemeindeChanged(code);
    if (gemeinde == null) {
      _plzController.clear();
      _cityController.clear();
    } else {
      _plzController.text = gemeinde.postalCode;
      _cityController.text = gemeinde.name;
    }
  }

  Future<void> _lookupByPlz(String plz) async {
    final notifier = ref.read(profileEditProvider(widget.apiClient).notifier);
    try {
      final matches = await notifier.geographyRepository.lookupByPlz(plz);
      if (!mounted || matches.isEmpty) return;
      final match = matches.first;
      _cityController.text = match.name;
      notifier.setCoordinates(match.latitude, match.longitude);
    } catch (_) {
      // Best-effort convenience lookup
    }
  }

  Future<void> _requestPhoneVerification() async {
    final phone = _phoneController.text.trim();
    final notifier = ref.read(profileEditProvider(widget.apiClient).notifier);
    try {
      await notifier.authRepository.requestProfilePhoneVerification(phone);
      if (!mounted) return;

      final verified = await context.push<bool>(
        '${AppRoutes.otpVerify}?phone=${Uri.encodeComponent(phone)}&purpose=phone_verification',
      );
      if (mounted && verified == true) {
        notifier.setPhoneVerified(true);
      }
    } catch (_) {
      notifier.setErrorKey('errors.generic');
    }
  }

  Future<void> _changePhoneNumber() async {
    final newPhoneController = TextEditingController();
    final newPhone = await showDialog<String>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text('profile.change_phone_semantic'.tr()),
        content: TextFormField(
          controller: newPhoneController,
          autofocus: true,
          keyboardType: TextInputType.phone,
          inputFormatters: [
            FilteringTextInputFormatter.allow(RegExp(r'[+\d\s\-()]')),
          ],
          decoration: InputDecoration(
            labelText: 'profile.phone'.tr(),
            hintText: '+43 660 1234567',
            border: const OutlineInputBorder(),
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext),
            child: Text('common.cancel'.tr()),
          ),
          FilledButton(
            onPressed: () =>
                Navigator.pop(dialogContext, newPhoneController.text.trim()),
            child: Text('common.confirm'.tr()),
          ),
        ],
      ),
    );
    newPhoneController.dispose();

    if (newPhone == null || newPhone.isEmpty || !mounted) return;

    final notifier = ref.read(profileEditProvider(widget.apiClient).notifier);
    try {
      await notifier.authRepository.initiatePhoneChange(newPhone);
      if (!mounted) return;
      await context.push(
        '${AppRoutes.otpVerify}?phone=${Uri.encodeComponent(newPhone)}&purpose=phone_change',
      );
    } catch (_) {
      notifier.setErrorKey('errors.generic');
    }
  }

  Future<void> _geocodeAddress() async {
    final notifier = ref.read(profileEditProvider(widget.apiClient).notifier);
    if (_streetController.text.trim().isEmpty ||
        _plzController.text.trim().isEmpty ||
        _cityController.text.trim().isEmpty) {
      notifier.setErrorKey('profile.address_incomplete');
      return;
    }

    final fullAddress =
        '${_streetController.text.trim()}, ${_plzController.text.trim()} ${_cityController.text.trim()}';

    final result = await notifier.geocodeAddress(fullAddress);
    if (!mounted || result == null) return;
    _showMapPreview(result.latitude, result.longitude);
  }

  void _showMapPreview(double lat, double lon) {
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      builder: (context) => _MapPreviewSheet(
        latitude: lat,
        longitude: lon,
        address:
            '${_streetController.text}, ${_plzController.text} ${_cityController.text}',
        onConfirm: () {
          Navigator.pop(context);
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text('profile.location_saved'.tr())),
          );
        },
      ),
    );
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;
    final notifier = ref.read(profileEditProvider(widget.apiClient).notifier);
    final success = await notifier.save(displayName: _nameController.text.trim());
    if (success && mounted) {
      Navigator.of(context).pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final textTheme = theme.textTheme;
    final colorScheme = theme.colorScheme;
    final isSeniorMode =
        MediaQuery.of(context).textScaler.textScaleFactor > 1.3;
    final buttonHeight = isSeniorMode
        ? AppTouch.buttonHeightSenior
        : AppTouch.buttonHeightStandard;

    final state = ref.watch(profileEditProvider(widget.apiClient));
    final notifier = ref.read(profileEditProvider(widget.apiClient).notifier);
    _syncUserToControllers(state);

    if (state.isLoading) {
      return Scaffold(
        appBar: AppBar(title: Text('profile.edit'.tr())),
        body: const Center(child: CircularProgressIndicator()),
      );
    }

    return Scaffold(
      appBar: AppBar(
        title: Text('profile.edit'.tr()),
        actions: [
          Semantics(
            button: true,
            label: 'common.save'.tr(),
            child: TextButton(
              onPressed: state.isSaving ? null : _save,
              child: state.isSaving
                  ? SizedBox(
                      width: isSeniorMode ? 24 : 20,
                      height: isSeniorMode ? 24 : 20,
                      child: const CircularProgressIndicator(strokeWidth: 2),
                    )
                  : Text('common.save'.tr()),
            ),
          ),
        ],
      ),
      body: Form(
        key: _formKey,
        child: ListView(
          padding: EdgeInsets.symmetric(
            horizontal: isSeniorMode
                ? AppSpacing.pageHSenior
                : AppSpacing.pageH,
            vertical: AppSpacing.lg,
          ),
          children: [
            // Profile photo
            Center(
              child: Semantics(
                button: true,
                label: 'profile.change_photo_semantic'.tr(),
                child: GestureDetector(
                  onTap: state.isUploadingPhoto ? null : _pickAndUploadPhoto,
                  child: Stack(
                    children: [
                      CircleAvatar(
                        radius: 48,
                        backgroundColor: colorScheme.primaryContainer,
                        backgroundImage: state.photoUrl != null
                            ? NetworkImage(
                                '${widget.apiClient.baseUrl}${state.photoUrl}',
                              )
                            : null,
                        child: state.photoUrl == null
                            ? Icon(
                                Icons.person,
                                size: 48,
                                color: colorScheme.onPrimaryContainer,
                              )
                            : null,
                      ),
                      if (state.isUploadingPhoto)
                        const Positioned.fill(
                          child: Center(child: CircularProgressIndicator()),
                        )
                      else
                        Positioned(
                          right: 0,
                          bottom: 0,
                          child: CircleAvatar(
                            radius: 16,
                            backgroundColor: colorScheme.primary,
                            child: Icon(
                              Icons.camera_alt,
                              size: 16,
                              color: colorScheme.onPrimary,
                            ),
                          ),
                        ),
                    ],
                  ),
                ),
              ),
            ),

            const SizedBox(height: 24),

            // Section 1: Basic Info
            _SectionHeader(title: 'profile.basic_info'.tr()),
            const SizedBox(height: 16),

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

            const SizedBox(height: 24),

            // Section 2: Mobile Phone
            _SectionHeader(title: 'profile.phone'.tr()),
            const SizedBox(height: 16),

            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: TextFormField(
                    controller: _phoneController,
                    keyboardType: TextInputType.phone,
                    readOnly: state.phoneVerified,
                    inputFormatters: [
                      FilteringTextInputFormatter.allow(RegExp(r'[+\d\s\-()]')),
                    ],
                    decoration: InputDecoration(
                      labelText: 'profile.phone'.tr(),
                      hintText: '+43 660 1234567',
                      prefixIcon: const Icon(Icons.phone_outlined),
                      border: const OutlineInputBorder(),
                      suffixIcon: state.phoneVerified
                          ? Icon(Icons.verified, color: colorScheme.primary)
                          : null,
                    ),
                    validator: (value) {
                      if (value == null || value.trim().isEmpty) {
                        return 'profile.phone_required'.tr();
                      }
                      final digits = value.replaceAll(RegExp(r'\D'), '');
                      if (digits.length < 7) {
                        return 'profile.phone_invalid'.tr();
                      }
                      return null;
                    },
                  ),
                ),
                const SizedBox(width: 12),
                if (!state.phoneVerified)
                  Semantics(
                    button: true,
                    label: 'profile.verify_phone_semantic'.tr(),
                    child: SizedBox(
                      height: buttonHeight,
                      child: OutlinedButton.icon(
                        onPressed: state.isVerifyingPhone
                            ? null
                            : _requestPhoneVerification,
                        icon: state.isVerifyingPhone
                            ? SizedBox(
                                width: isSeniorMode ? 24 : 20,
                                height: isSeniorMode ? 24 : 20,
                                child: const CircularProgressIndicator(
                                  strokeWidth: 2,
                                ),
                              )
                            : const Icon(Icons.verified_user_outlined),
                        label: Text('profile.verify_phone_button'.tr()),
                      ),
                    ),
                  )
                else
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      Chip(
                        avatar: Icon(
                          Icons.check_circle,
                          color: colorScheme.primary,
                        ),
                        label: Text('profile.phone_verified'.tr()),
                      ),
                      Semantics(
                        button: true,
                        label: 'profile.change_phone_semantic'.tr(),
                        child: TextButton(
                          onPressed: state.isVerifyingPhone
                              ? null
                              : _changePhoneNumber,
                          child: Text('profile.change_phone_button'.tr()),
                        ),
                      ),
                    ],
                  ),
              ],
            ),

            const SizedBox(height: 24),

            // Section 3: Austrian Address
            _SectionHeader(title: 'profile.address'.tr()),
            const SizedBox(height: 16),

            // Street
            TextFormField(
              controller: _streetController,
              decoration: InputDecoration(
                labelText: 'profile.street'.tr(),
                hintText: 'Dorfplatz 2',
                border: const OutlineInputBorder(),
              ),
              validator: (value) {
                if (value == null || value.trim().isEmpty) {
                  return 'profile.street_required'.tr();
                }
                return null;
              },
            ),

            const SizedBox(height: 16),

            // PLZ
            TextFormField(
              controller: _plzController,
              keyboardType: TextInputType.number,
              inputFormatters: [
                FilteringTextInputFormatter.digitsOnly,
                LengthLimitingTextInputFormatter(4),
              ],
              decoration: InputDecoration(
                labelText: 'profile.plz'.tr(),
                hintText: '6175',
                prefixIcon: const Icon(Icons.local_post_office_outlined),
                border: const OutlineInputBorder(),
              ),
              onChanged: (value) {
                if (value.length == 4) _lookupByPlz(value);
              },
            ),

            const SizedBox(height: 16),

            // Bundesland dropdown
            DropdownButtonFormField<String>(
              value: state.selectedBundesland,
              decoration: InputDecoration(
                labelText: 'profile.bundesland'.tr(),
                border: const OutlineInputBorder(),
                prefixIcon: const Icon(Icons.map_outlined),
              ),
              items: state.bundeslaender
                  .map(
                    (b) => DropdownMenuItem(value: b.code, child: Text(b.name)),
                  )
                  .toList(),
              onChanged: notifier.onBundeslandChanged,
              validator: (value) {
                if (value == null || value.isEmpty) {
                  return 'profile.bundesland_required'.tr();
                }
                return null;
              },
            ),

            const SizedBox(height: 16),

            // Bezirk dropdown
            DropdownButtonFormField<String>(
              value: state.selectedBezirk,
              decoration: InputDecoration(
                labelText: 'profile.bezirk'.tr(),
                border: const OutlineInputBorder(),
                prefixIcon: const Icon(Icons.location_city_outlined),
              ),
              items: state.bezirke
                  .map(
                    (b) => DropdownMenuItem(value: b.code, child: Text(b.name)),
                  )
                  .toList(),
              onChanged: state.bezirke.isNotEmpty ? notifier.onBezirkChanged : null,
              validator: (value) {
                if (state.bezirke.isNotEmpty && (value == null || value.isEmpty)) {
                  return 'profile.bezirk_required'.tr();
                }
                return null;
              },
            ),

            const SizedBox(height: 16),

            // Gemeinde dropdown
            DropdownButtonFormField<String>(
              value: state.selectedGemeinde,
              decoration: InputDecoration(
                labelText: 'profile.gemeinde'.tr(),
                border: const OutlineInputBorder(),
                prefixIcon: const Icon(Icons.location_on_outlined),
              ),
              items: state.gemeinden
                  .map(
                    (g) => DropdownMenuItem(
                      value: g.code,
                      child: Text('${g.name} (${g.postalCode})'),
                    ),
                  )
                  .toList(),
              onChanged: state.gemeinden.isNotEmpty ? _onGemeindeChanged : null,
              validator: (value) {
                if (state.gemeinden.isNotEmpty && (value == null || value.isEmpty)) {
                  return 'profile.gemeinde_required'.tr();
                }
                return null;
              },
            ),

            const SizedBox(height: 16),

            // City (auto-filled from Gemeinde)
            TextFormField(
              controller: _cityController,
              decoration: InputDecoration(
                labelText: 'profile.city'.tr(),
                border: const OutlineInputBorder(),
                prefixIcon: const Icon(Icons.location_city_outlined),
              ),
            ),

            const SizedBox(height: 16),

            // Lookup address on map button
            Semantics(
              button: true,
              label: 'profile.lookup_address_semantic'.tr(),
              child: SizedBox(
                height: buttonHeight,
                child: OutlinedButton.icon(
                  onPressed: state.isGeocoding ? null : _geocodeAddress,
                  icon: state.isGeocoding
                      ? SizedBox(
                          width: isSeniorMode ? 24 : 20,
                          height: isSeniorMode ? 24 : 20,
                          child: const CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.map_outlined),
                  label: Text(
                    'profile.lookup_address'.tr(),
                    style: textTheme.titleMedium?.copyWith(
                      fontSize: isSeniorMode ? 18 : null,
                    ),
                  ),
                ),
              ),
            ),

            const SizedBox(height: 24),

            // Coordinates display (read-only)
            if (state.latitude != null && state.longitude != null) ...[
              _SectionHeader(title: 'profile.coordinates'.tr()),
              const SizedBox(height: 16),
              Row(
                children: [
                  Expanded(
                    child: _CoordinateCard(
                      label: 'profile.latitude'.tr(),
                      value: state.latitude!.toStringAsFixed(6),
                      icon: Icons.north,
                    ),
                  ),
                  const SizedBox(width: 16),
                  Expanded(
                    child: _CoordinateCard(
                      label: 'profile.longitude'.tr(),
                      value: state.longitude!.toStringAsFixed(6),
                      icon: Icons.east,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 24),
            ],

            // Section 4: Interests
            _SectionHeader(title: 'profile.interests'.tr()),
            const SizedBox(height: 8),
            if (state.interestCatalog.isEmpty)
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
                children: state.interestCatalog.map((interest) {
                  final selected = state.selectedInterestIds.contains(interest.id);
                  return FilterChip(
                    label: Text(interest.nameKey.tr()),
                    selected: selected,
                    onSelected: (value) => notifier.toggleInterest(interest.id, value),
                  );
                }).toList(),
              ),

            const SizedBox(height: 24),

            if (state.errorKey != null) ...[
              const SizedBox(height: 16),
              Text(
                state.errorKey!.tr(),
                style: TextStyle(color: colorScheme.error),
                textAlign: TextAlign.center,
              ),
            ],

            const SizedBox(height: 32),

            // Save button (large, accessible)
            Semantics(
              button: true,
              label: 'common.save'.tr(),
              child: SizedBox(
                height: buttonHeight,
                child: FilledButton(
                  onPressed: state.isSaving ? null : _save,
                  child: state.isSaving
                      ? SizedBox(
                          width: isSeniorMode ? 24 : 20,
                          height: isSeniorMode ? 24 : 20,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            valueColor: AlwaysStoppedAnimation<Color>(
                              colorScheme.onPrimary,
                            ),
                          ),
                        )
                      : Text(
                          'common.save'.tr(),
                          style: textTheme.titleMedium?.copyWith(
                            color: colorScheme.onPrimary,
                            fontSize: isSeniorMode ? 18 : null,
                          ),
                        ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _SectionHeader extends StatelessWidget {
  const _SectionHeader({required this.title});

  final String title;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    return Text(
      title,
      style: textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w600),
    );
  }
}

class _CoordinateCard extends StatelessWidget {
  const _CoordinateCard({
    required this.label,
    required this.value,
    required this.icon,
  });

  final String label;
  final String value;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;
    final textTheme = theme.textTheme;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(icon, size: 20, color: colorScheme.primary),
                const SizedBox(width: 8),
                Text(
                  label,
                  style: textTheme.labelMedium?.copyWith(
                    color: colorScheme.onSurfaceVariant,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            SelectableText(
              value,
              style: textTheme.titleMedium?.copyWith(fontFamily: 'monospace'),
            ),
          ],
        ),
      ),
    );
  }
}

class _MapPreviewSheet extends StatelessWidget {
  const _MapPreviewSheet({
    required this.latitude,
    required this.longitude,
    required this.address,
    required this.onConfirm,
  });

  final double latitude;
  final double longitude;
  final String address;
  final VoidCallback onConfirm;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final textTheme = theme.textTheme;
    final colorScheme = theme.colorScheme;
    final isSeniorMode =
        MediaQuery.of(context).textScaler.textScaleFactor > 1.3;
    final buttonHeight = isSeniorMode
        ? AppTouch.buttonHeightSenior
        : AppTouch.buttonHeightStandard;

    return DraggableScrollableSheet(
      initialChildSize: 0.7,
      maxChildSize: 0.9,
      minChildSize: 0.5,
      expand: false,
      builder: (context, scrollController) {
        return Container(
          padding: const EdgeInsets.all(AppSpacing.lg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      'profile.map_preview'.tr(),
                      style: textTheme.titleLarge?.copyWith(
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                  IconButton(
                    icon: const Icon(Icons.close),
                    onPressed: () => Navigator.pop(context),
                  ),
                ],
              ),
              const SizedBox(height: 16),
              Expanded(
                child: Container(
                  decoration: BoxDecoration(
                    color: colorScheme.surfaceContainerHighest,
                    borderRadius: BorderRadius.circular(AppRadius.md),
                  ),
                  child: Stack(
                    alignment: Alignment.center,
                    children: [
                      Icon(
                        Icons.map,
                        size: 96,
                        color: colorScheme.outline.withAlpha(50),
                      ),
                      Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          Icon(
                            Icons.location_pin,
                            size: 48,
                            color: colorScheme.error,
                          ),
                          const SizedBox(height: 8),
                          Text(
                            address,
                            style: textTheme.bodyMedium,
                            textAlign: TextAlign.center,
                          ),
                          const SizedBox(height: 4),
                          Text(
                            '${latitude.toStringAsFixed(4)}, ${longitude.toStringAsFixed(4)}',
                            style: textTheme.bodySmall?.copyWith(
                              color: colorScheme.outline,
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 16),
              Semantics(
                button: true,
                label: 'common.confirm'.tr(),
                child: SizedBox(
                  height: buttonHeight,
                  child: FilledButton(
                    onPressed: onConfirm,
                    child: Text(
                      'common.confirm'.tr(),
                      style: textTheme.titleMedium?.copyWith(
                        color: colorScheme.onPrimary,
                      ),
                    ),
                  ),
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}
