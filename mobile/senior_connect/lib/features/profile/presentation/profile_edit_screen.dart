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
import 'package:go_router/go_router.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../core/router/app_router.dart';
import '../../auth/data/auth_repository.dart';

class ProfileEditScreen extends StatefulWidget {
  const ProfileEditScreen({super.key, required this.apiClient});

  final ApiClient apiClient;

  @override
  State<ProfileEditScreen> createState() => _ProfileEditScreenState();
}

class _ProfileEditScreenState extends State<ProfileEditScreen> {
  late final AuthRepository _authRepository = AuthRepositoryImpl(widget.apiClient);

  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController(text: 'Maria Muster');
  final _emailController = TextEditingController();
  final _phoneController = TextEditingController();
  final _streetController = TextEditingController();
  final _plzController = TextEditingController();
  final _cityController = TextEditingController();

  String? _selectedBundesland;
  String? _selectedBezirk;
  String? _selectedGemeinde;
  List<Map<String, dynamic>> _bundeslaender = [];
  List<Map<String, dynamic>> _bezirke = [];
  List<Map<String, dynamic>> _gemeinden = [];

  bool _isSaving = false;
  bool _isVerifyingPhone = false;
  bool _isGeocoding = false;
  bool _phoneVerified = false;
  String? _errorKey;
  double? _latitude;
  double? _longitude;
  String? _gemeindeCode;

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

  Future<void> _loadBundeslaender() async {
    // TODO P1-28: call reference API GET /reference/austria/bundeslaender
    // For now, mock data for pilot region
    setState(() {
      _bundeslaender = [
        {'code': '7', 'name': 'Tirol'},
        {'code': '9', 'name': 'Wien'},
        {'code': '1', 'name': 'Burgenland'},
        {'code': '2', 'name': 'Kärnten'},
        {'code': '3', 'name': 'Niederösterreich'},
        {'code': '4', 'name': 'Oberösterreich'},
        {'code': '5', 'name': 'Salzburg'},
        {'code': '6', 'name': 'Steiermark'},
        {'code': '8', 'name': 'Vorarlberg'},
      ];
    });
  }

  Future<void> _onBundeslandChanged(String? code) async {
    if (code == null) {
      setState(() {
        _selectedBundesland = null;
        _selectedBezirk = null;
        _selectedGemeinde = null;
        _bezirke = [];
        _gemeinden = [];
      });
      return;
    }

    setState(() {
      _selectedBundesland = code;
      _selectedBezirk = null;
      _selectedGemeinde = null;
      _bezirke = [];
      _gemeinden = [];
    });

    // TODO P1-28: call GET /reference/austria/bezirke?bundeslandCode=$code
    // Mock for pilot region
    if (code == '7') {
      setState(() {
        _bezirke = [
          {'code': '701', 'name': 'Innsbruck'},
          {'code': '703', 'name': 'Innsbruck-Land'},
          {'code': '704', 'name': 'Imst'},
          {'code': '705', 'name': 'Kitzbühel'},
          {'code': '706', 'name': 'Kufstein'},
          {'code': '707', 'name': 'Landeck'},
          {'code': '708', 'name': 'Lienz'},
          {'code': '709', 'name': 'Reutte'},
          {'code': '710', 'name': 'Schwaz'},
        ];
      });
    }
  }

  Future<void> _onBezirkChanged(String? code) async {
    if (code == null) {
      setState(() {
        _selectedBezirk = null;
        _selectedGemeinde = null;
        _gemeinden = [];
      });
      return;
    }

    setState(() {
      _selectedBezirk = code;
      _selectedGemeinde = null;
      _gemeinden = [];
    });

    // TODO P1-28: call GET /reference/austria/gemeinden?bezirkCode=$code
    // Mock for Innsbruck-Land
    if (code == '703') {
      setState(() {
        _gemeinden = [
          {'code': '70320', 'name': 'Kematen in Tirol', 'plz': '6175', 'lat': 47.2600, 'lon': 11.2433},
          {'code': '70321', 'name': 'Zirl', 'plz': '6170', 'lat': 47.2719, 'lon': 11.2336},
          {'code': '70322', 'name': 'Völs', 'plz': '6176', 'lat': 47.2481, 'lon': 11.3092},
          {'code': '70323', 'name': 'Axams', 'plz': '6094', 'lat': 47.2358, 'lon': 11.2778},
          {'code': '70324', 'name': 'Götzens', 'plz': '6091', 'lat': 47.2475, 'lon': 11.3475},
        ];
      });
    } else if (code == '701') {
      setState(() {
        _gemeinden = [
          {'code': '70101', 'name': 'Innsbruck', 'plz': '6020', 'lat': 47.2692, 'lon': 11.4041},
        ];
      });
    }
  }

  void _onGemeindeChanged(String? code) {
    if (code == null) {
      setState(() {
        _selectedGemeinde = null;
        _plzController.clear();
        _cityController.clear();
        _latitude = null;
        _longitude = null;
        _gemeindeCode = null;
      });
      return;
    }

    final gemeinde = _gemeinden.firstWhere((g) => g['code'] == code);
    setState(() {
      _selectedGemeinde = code;
      _plzController.text = gemeinde['plz'] as String;
      _cityController.text = gemeinde['name'] as String;
      _latitude = gemeinde['lat'] as double;
      _longitude = gemeinde['lon'] as double;
      _gemeindeCode = gemeinde['code'] as String;
    });
  }

  Future<void> _requestPhoneVerification() async {
    final phone = _phoneController.text.trim();
    setState(() {
      _isVerifyingPhone = true;
      _errorKey = null;
    });

    try {
      await _authRepository.requestProfilePhoneVerification(phone);
      if (!mounted) return;

      final verified = await context.push<bool>(
        '${AppRoutes.otpVerify}?phone=${Uri.encodeComponent(phone)}&purpose=phone_verification',
      );
      if (mounted && verified == true) {
        setState(() => _phoneVerified = true);
      }
    } catch (_) {
      if (mounted) setState(() => _errorKey = 'errors.generic');
    } finally {
      if (mounted) setState(() => _isVerifyingPhone = false);
    }
  }

  Future<void> _geocodeAddress() async {
    if (_streetController.text.trim().isEmpty ||
        _plzController.text.trim().isEmpty ||
        _cityController.text.trim().isEmpty) {
      setState(() => _errorKey = 'profile.address_incomplete');
      return;
    }

    setState(() => _isGeocoding = true);
    // TODO P1-28: call POST /reference/geocode-address
    await Future.delayed(const Duration(seconds: 1));
    
    // Mock: use existing coordinates if available
    if (_latitude != null && _longitude != null) {
      setState(() {
        _isGeocoding = false;
        _errorKey = null;
      });
      if (mounted) {
        _showMapPreview();
      }
    }
  }

  void _showMapPreview() {
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      builder: (context) => _MapPreviewSheet(
        latitude: _latitude!,
        longitude: _longitude!,
        address: '${_streetController.text}, ${_plzController.text} ${_cityController.text}',
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
    setState(() {
      _isSaving = true;
      _errorKey = null;
    });

    try {
      // TODO P1-28: call profileRepository.updateProfile with all fields
      if (mounted) Navigator.of(context).pop();
    } catch (_) {
      setState(() => _errorKey = 'errors.generic');
    } finally {
      if (mounted) setState(() => _isSaving = false);
    }
  }

  @override
  void initState() {
    super.initState();
    _loadBundeslaender();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final textTheme = theme.textTheme;
    final colorScheme = theme.colorScheme;
    final isSeniorMode = MediaQuery.of(context).textScaler.textScaleFactor > 1.3;
    final minTouchTarget = isSeniorMode ? AppTouch.minSenior : AppTouch.minStandard;
    final buttonHeight = isSeniorMode ? AppTouch.buttonHeightSenior : AppTouch.buttonHeightStandard;

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
                  ? SizedBox(
                      width: isSeniorMode ? 24 : 20,
                      height: isSeniorMode ? 24 : 20,
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
          padding: EdgeInsets.symmetric(
            horizontal: isSeniorMode ? AppSpacing.pageHSenior : AppSpacing.pageH,
            vertical: AppSpacing.lg,
          ),
          children: [
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
                    inputFormatters: [
                      FilteringTextInputFormatter.allow(RegExp(r'[+\d\s\-()]')),
                    ],
                    decoration: InputDecoration(
                      labelText: 'profile.phone'.tr(),
                      hintText: '+43 660 1234567',
                      prefixIcon: const Icon(Icons.phone_outlined),
                      border: const OutlineInputBorder(),
                      suffixIcon: _phoneVerified
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
                if (!_phoneVerified)
                  Semantics(
                    button: true,
                    label: 'profile.verify_phone_semantic'.tr(),
                    child: SizedBox(
                      height: buttonHeight,
                      child: OutlinedButton.icon(
                        onPressed: _isVerifyingPhone ? null : _requestPhoneVerification,
                        icon: _isVerifyingPhone
                            ? SizedBox(
                                width: isSeniorMode ? 24 : 20,
                                height: isSeniorMode ? 24 : 20,
                                child: CircularProgressIndicator(strokeWidth: 2),
                              )
                            : const Icon(Icons.verified_user_outlined),
                        label: Text('profile.verify_phone_button'.tr()),
                      ),
                    ),
                  )
                else
                  Semantics(
                    label: 'profile.phone_verified'.tr(),
                    child: Container(
                      height: buttonHeight,
                      alignment: Alignment.center,
                      child: Chip(
                        avatar: Icon(Icons.check_circle, color: colorScheme.primary),
                        label: Text('profile.phone_verified'.tr()),
                      ),
                    ),
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
                LengthLimitingTextInputFormatter(5),
              ],
              decoration: InputDecoration(
                labelText: 'profile.plz'.tr(),
                hintText: '6175',
                prefixIcon: const Icon(Icons.local_post_office_outlined),
                border: const OutlineInputBorder(),
              ),
              onChanged: (value) {
                if (value.length == 5) {
                  // TODO: auto-lookup PLZ
                }
              },
            ),

            const SizedBox(height: 16),

            // Bundesland dropdown
            DropdownButtonFormField<String>(
              value: _selectedBundesland,
              decoration: InputDecoration(
                labelText: 'profile.bundesland'.tr(),
                border: const OutlineInputBorder(),
                prefixIcon: const Icon(Icons.map_outlined),
              ),
              items: _bundeslaender.map((b) => DropdownMenuItem(
                value: b['code'] as String,
                child: Text(b['name'] as String),
              )).toList(),
              onChanged: _onBundeslandChanged,
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
              value: _selectedBezirk,
              decoration: InputDecoration(
                labelText: 'profile.bezirk'.tr(),
                border: const OutlineInputBorder(),
                prefixIcon: const Icon(Icons.location_city_outlined),
              ),
              items: _bezirke.map((b) => DropdownMenuItem(
                value: b['code'] as String,
                child: Text(b['name'] as String),
              )).toList(),
              onChanged: _bezirke.isNotEmpty ? _onBezirkChanged : null,
              validator: (value) {
                if (_bezirke.isNotEmpty && (value == null || value.isEmpty)) {
                  return 'profile.bezirk_required'.tr();
                }
                return null;
              },
            ),

            const SizedBox(height: 16),

            // Gemeinde dropdown
            DropdownButtonFormField<String>(
              value: _selectedGemeinde,
              decoration: InputDecoration(
                labelText: 'profile.gemeinde'.tr(),
                border: const OutlineInputBorder(),
                prefixIcon: const Icon(Icons.location_on_outlined),
              ),
              items: _gemeinden.map((g) => DropdownMenuItem(
                value: g['code'] as String,
                child: Text('${g['name']} (${g['plz']})'),
              )).toList(),
              onChanged: _gemeinden.isNotEmpty ? _onGemeindeChanged : null,
              validator: (value) {
                if (_gemeinden.isNotEmpty && (value == null || value.isEmpty)) {
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
                  onPressed: _isGeocoding ? null : _geocodeAddress,
                  icon: _isGeocoding
                      ? SizedBox(
                          width: isSeniorMode ? 24 : 20,
                          height: isSeniorMode ? 24 : 20,
                          child: CircularProgressIndicator(strokeWidth: 2),
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
            if (_latitude != null && _longitude != null) ...[
              _SectionHeader(title: 'profile.coordinates'.tr()),
              const SizedBox(height: 16),
              Row(
                children: [
                  Expanded(
                    child: _CoordinateCard(
                      label: 'profile.latitude'.tr(),
                      value: _latitude!.toStringAsFixed(6),
                      icon: Icons.north,
                    ),
                  ),
                  const SizedBox(width: 16),
                  Expanded(
                    child: _CoordinateCard(
                      label: 'profile.longitude'.tr(),
                      value: _longitude!.toStringAsFixed(6),
                      icon: Icons.east,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 24),
            ],

            if (_errorKey != null) ...[
              const SizedBox(height: 16),
              Text(
                _errorKey!.tr(),
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
                  onPressed: _isSaving ? null : _save,
                  child: _isSaving
                      ? SizedBox(
                          width: isSeniorMode ? 24 : 20,
                          height: isSeniorMode ? 24 : 20,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            valueColor: AlwaysStoppedAnimation<Color>(colorScheme.onPrimary),
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
      style: textTheme.titleLarge?.copyWith(
        fontWeight: FontWeight.w600,
      ),
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
                Text(label, style: textTheme.labelMedium?.copyWith(
                  color: colorScheme.onSurfaceVariant,
                )),
              ],
            ),
            const SizedBox(height: 8),
            SelectableText(
              value,
              style: textTheme.titleMedium?.copyWith(
                fontFamily: 'monospace',
              ),
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
    final isSeniorMode = MediaQuery.of(context).textScaler.textScaleFactor > 1.3;
    final buttonHeight = isSeniorMode ? AppTouch.buttonHeightSenior : AppTouch.buttonHeightStandard;

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
                    onPressed: () => Navigator.pop(context),
                    icon: const Icon(Icons.close),
                  ),
                ],
              ),
              const SizedBox(height: 16),
              Text(address, style: textTheme.bodyMedium),
              const SizedBox(height: 16),
              // Map placeholder - in real implementation use flutter_map or google_maps_flutter
              Expanded(
                child: Container(
                  decoration: BoxDecoration(
                    border: Border.all(color: colorScheme.outline),
                    borderRadius: AppRadius.card,
                  ),
                  child: Center(
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(Icons.map_outlined, size: 64, color: colorScheme.onSurfaceVariant),
                        const SizedBox(height: 16),
                        Text(
                          'Interactive map preview\n(Lat: ${latitude.toStringAsFixed(4)}, Lng: ${longitude.toStringAsFixed(4)})',
                          textAlign: TextAlign.center,
                          style: textTheme.bodyMedium?.copyWith(
                            color: colorScheme.onSurfaceVariant,
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
              const SizedBox(height: 24),
              Semantics(
                button: true,
                label: 'profile.confirm_location'.tr(),
                child: SizedBox(
                  width: double.infinity,
                  child: FilledButton(
                    onPressed: onConfirm,
                    style: FilledButton.styleFrom(
                      minimumSize: Size.fromHeight(buttonHeight),
                    ),
                    child: Text('profile.confirm_location'.tr()),
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