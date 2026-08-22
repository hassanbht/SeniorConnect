// lib/features/auth/presentation/phone_entry_screen.dart
//
// P1-27: Phone number entry screen.
//
// BR-AUTH-03: NO password field on senior/volunteer screens.
// This screen only collects phone number → requests OTP code via SMS.
//
// Accessibility:
//  - 64dp touch target on submit button
//  - Semantic labels on all interactive elements
//  - Keyboard type: phone
//  - autofocus on the field

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:go_router/go_router.dart';

import '../../../core/router/app_router.dart';

class PhoneEntryScreen extends StatefulWidget {
  const PhoneEntryScreen({super.key});

  @override
  State<PhoneEntryScreen> createState() => _PhoneEntryScreenState();
}

class _PhoneEntryScreenState extends State<PhoneEntryScreen> {
  final _formKey = GlobalKey<FormState>();
  final _phoneController = TextEditingController();
  bool _isLoading = false;
  String? _errorKey;

  @override
  void dispose() {
    _phoneController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() {
      _isLoading = true;
      _errorKey = null;
    });

    final phone = _phoneController.text.trim();

    try {
      // TODO P1-27: inject AuthRepository and call requestPhoneOtp
      // For now navigate to OTP screen directly (will be wired in P1-26)
      if (mounted) {
        context.go(
          '${AppRoutes.otpVerify}?phone=${Uri.encodeComponent(phone)}',
        );
      }
    } catch (_) {
      setState(() => _errorKey = 'errors.generic');
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final textTheme = theme.textTheme;
    final colorScheme = theme.colorScheme;

    return Scaffold(
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 480),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 32),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const Spacer(),

                    // Logo / app name
                    Text(
                      'SeniorConnect',
                      style: textTheme.displaySmall?.copyWith(
                        color: colorScheme.primary,
                        fontWeight: FontWeight.w700,
                      ),
                      textAlign: TextAlign.center,
                    ),
                    const SizedBox(height: 8),
                    Text(
                      'app.tagline'.tr(),
                      style: textTheme.bodyLarge?.copyWith(
                        color: colorScheme.onSurfaceVariant,
                      ),
                      textAlign: TextAlign.center,
                    ),

                    const SizedBox(height: 48),

                    // Phone field — NO password field (BR-AUTH-03)
                    Semantics(
                      label: 'auth.phone_field_label'.tr(),
                      child: TextFormField(
                        controller: _phoneController,
                        autofocus: true,
                        keyboardType: TextInputType.phone,
                        textInputAction: TextInputAction.done,
                        inputFormatters: [
                          FilteringTextInputFormatter.allow(
                            RegExp(r'[+\d\s\-()]'),
                          ),
                        ],
                        decoration: InputDecoration(
                          labelText: 'auth.phone'.tr(),
                          hintText: '+43 660 1234567',
                          prefixIcon: const Icon(Icons.phone_outlined),
                          border: const OutlineInputBorder(),
                        ),
                        validator: (value) {
                          if (value == null || value.trim().isEmpty) {
                            return 'auth.phone_required'.tr();
                          }
                          // Basic check: starts with + and has enough digits
                          final digits = value.replaceAll(RegExp(r'\D'), '');
                          if (digits.length < 7) {
                            return 'auth.phone_invalid'.tr();
                          }
                          return null;
                        },
                        onFieldSubmitted: (_) => _submit(),
                      ),
                    ),

                    if (_errorKey != null) ...[
                      const SizedBox(height: 12),
                      Text(
                        _errorKey!.tr(),
                        style: TextStyle(color: colorScheme.error),
                        textAlign: TextAlign.center,
                      ),
                    ],

                    const SizedBox(height: 24),

                    // Submit button — 64dp tall (senior-accessible)
                    Semantics(
                      button: true,
                      label: 'auth.request_code_semantic'.tr(),
                      child: SizedBox(
                        height: 64,
                        child: FilledButton.icon(
                          onPressed: _isLoading ? null : _submit,
                          icon: _isLoading
                              ? const SizedBox(
                                  width: 20,
                                  height: 20,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2,
                                  ),
                                )
                              : const Icon(Icons.arrow_forward),
                          label: Text(
                            'auth.request_code'.tr(),
                            style: textTheme.titleMedium?.copyWith(
                              color: colorScheme.onPrimary,
                            ),
                          ),
                        ),
                      ),
                    ),

                    const Spacer(flex: 2),

                    // Privacy note
                    Text(
                      'auth.privacy_note'.tr(),
                      style: textTheme.bodySmall?.copyWith(
                        color: colorScheme.onSurfaceVariant,
                      ),
                      textAlign: TextAlign.center,
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
