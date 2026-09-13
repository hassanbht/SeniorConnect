// lib/features/auth/presentation/auth_screen.dart
//
// P1-27: Multi-provider authentication screen.
// Supports: Google Sign-In, ID Austria, Email + Password + Confirm, Phone OTP.
//
// BR-AUTH-01: Multi-provider tier for maximum reach
// BR-AUTH-02: Email+Password with confirmation and email verification
// BR-AUTH-03: NO password field on senior-facing screen — but allowed here since
//   one-tap passwordless alternatives (Google, ID Austria, SMS OTP) remain prominent
//
// Accessibility:
//  - 64dp touch targets on all buttons
//  - Semantic labels on all interactive elements
//  - RTL mirroring for Persian
//  - Senior Mode: larger text, larger touch targets

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:go_router/go_router.dart';

import '../../../core/design_system/app_spacing.dart';
import '../../../core/design_system/app_typography.dart';
import '../../../core/router/app_router.dart';
import '../../profile/presentation/profile_screen.dart';

class AuthScreen extends StatefulWidget {
  const AuthScreen({super.key});

  @override
  State<AuthScreen> createState() => _AuthScreenState();
}

class _AuthScreenState extends State<AuthScreen> with SingleTickerProviderStateMixin {
  late TabController _tabController;
  final _phoneController = TextEditingController();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _confirmPasswordController = TextEditingController();
  final _phoneFormKey = GlobalKey<FormState>();
  final _emailFormKey = GlobalKey<FormState>();
  bool _isPhoneLoading = false;
  bool _isEmailLoading = false;
  String? _phoneErrorKey;
  String? _emailErrorKey;
  bool _obscurePassword = true;
  bool _obscureConfirmPassword = true;
  bool _passwordsMatch = true;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 3, vsync: this);
    _passwordController.addListener(_checkPasswordMatch);
    _confirmPasswordController.addListener(_checkPasswordMatch);
  }

  @override
  void dispose() {
    _tabController.dispose();
    _phoneController.dispose();
    _emailController.dispose();
    _passwordController.dispose();
    _confirmPasswordController.dispose();
    super.dispose();
  }

  void _checkPasswordMatch() {
    setState(() {
      _passwordsMatch = _passwordController.text == _confirmPasswordController.text &&
          _passwordController.text.isNotEmpty &&
          _confirmPasswordController.text.isNotEmpty;
    });
  }

  Future<void> _submitPhone() async {
    if (!_phoneFormKey.currentState!.validate()) return;
    setState(() {
      _isPhoneLoading = true;
      _phoneErrorKey = null;
    });

    final phone = _phoneController.text.trim();

    try {
      // TODO P1-27: inject AuthRepository and call requestPhoneOtp
      // For now navigate to OTP screen directly
      if (mounted) {
        context.go(
          '${AppRoutes.otpVerify}?phone=${Uri.encodeComponent(phone)}',
        );
      }
    } catch (_) {
      setState(() => _phoneErrorKey = 'errors.generic');
    } finally {
      if (mounted) setState(() => _isPhoneLoading = false);
    }
  }

  Future<void> _submitEmail() async {
    if (!_emailFormKey.currentState!.validate()) return;
    if (!_passwordsMatch) {
      setState(() => _emailErrorKey = 'auth.register.password_mismatch');
      return;
    }
    if (_passwordController.text.length < 8) {
      setState(() => _emailErrorKey = 'auth.register.password_too_short');
      return;
    }

    setState(() {
      _isEmailLoading = true;
      _emailErrorKey = null;
    });

    try {
      // TODO P1-27: inject AuthRepository and call registerEmailPassword
      // For now show success
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('auth.register.email_verification_sent'.tr())),
        );
        context.go(AppRoutes.phoneEntry);
      }
    } catch (_) {
      setState(() => _emailErrorKey = 'errors.generic');
    } finally {
      if (mounted) setState(() => _isEmailLoading = false);
    }
  }

  void _googleSignIn() {
    // TODO P1-27: implement Google Sign-In flow
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text('Google Sign-In coming soon')),
    );
  }

  void _idAustriaSignIn() {
    // TODO P1-27: implement ID Austria OIDC flow
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text('ID Austria Sign-In coming soon')),
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final textTheme = theme.textTheme;
    final colorScheme = theme.colorScheme;
    final isSeniorMode = MediaQuery.of(context).textScaler.textScaleFactor > 1.3;

    return Scaffold(
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 480),
            child: Padding(
              padding: EdgeInsets.symmetric(
                horizontal: isSeniorMode ? AppSpacing.xl : AppSpacing.lg,
                vertical: AppSpacing.xl,
              ),
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

                  const SizedBox(height: 32),

                  // Tab bar for auth methods
                  TabBar(
                    controller: _tabController,
                    tabs: [
                      Tab(
                        icon: const Icon(Icons.phone_outlined),
                        text: 'auth.phone'.tr(),
                      ),
                      Tab(
                        icon: const Icon(Icons.email_outlined),
                        text: 'auth.login.email'.tr(),
                      ),
                      Tab(
                        icon: const Icon(Icons.smartphone_outlined),
                        text: 'auth.id_austria.button'.tr(),
                      ),
                    ],
                    labelStyle: textTheme.labelLarge?.copyWith(
                      fontWeight: FontWeight.w600,
                    ),
                    unselectedLabelStyle: textTheme.labelLarge,
                    indicatorSize: TabBarIndicatorSize.label,
                  ),

                  const SizedBox(height: 24),

                  // Tab views
                  Expanded(
                    child: TabBarView(
                      controller: _tabController,
                      children: [
                        // Phone OTP tab
                        _PhoneTab(
                          formKey: _phoneFormKey,
                          phoneController: _phoneController,
                          isLoading: _isPhoneLoading,
                          errorKey: _phoneErrorKey,
                          onSubmit: _submitPhone,
                        ),
                        // Email + Password tab
                        _EmailTab(
                          formKey: _emailFormKey,
                          emailController: _emailController,
                          passwordController: _passwordController,
                          confirmPasswordController: _confirmPasswordController,
                          obscurePassword: _obscurePassword,
                          obscureConfirmPassword: _obscureConfirmPassword,
                          passwordsMatch: _passwordsMatch,
                          isLoading: _isEmailLoading,
                          errorKey: _emailErrorKey,
                          onTogglePasswordVisibility: () => setState(() => _obscurePassword = !_obscurePassword),
                          onToggleConfirmPasswordVisibility: () => setState(() => _obscureConfirmPassword = !_obscureConfirmPassword),
                          onSubmit: _submitEmail,
                        ),
                        // ID Austria tab (placeholder)
                        _IdAustriaTab(
                          onGoogleSignIn: _googleSignIn,
                          onIdAustriaSignIn: _idAustriaSignIn,
                        ),
                      ],
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
    );
  }
}

class _PhoneTab extends StatelessWidget {
  const _PhoneTab({
    required this.formKey,
    required this.phoneController,
    required this.isLoading,
    required this.errorKey,
    required this.onSubmit,
  });

  final GlobalKey<FormState> formKey;
  final TextEditingController phoneController;
  final bool isLoading;
  final String? errorKey;
  final Future<void> Function() onSubmit;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final textTheme = theme.textTheme;
    final colorScheme = theme.colorScheme;
    final isSeniorMode = MediaQuery.of(context).textScaler.textScaleFactor > 1.3;

    return SingleChildScrollView(
      child: Form(
        key: formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const SizedBox(height: 16),

            // Phone field
            Semantics(
              label: 'auth.phone_field_label'.tr(),
              child: TextFormField(
                controller: phoneController,
                autofocus: true,
                keyboardType: TextInputType.phone,
                textInputAction: TextInputAction.done,
                inputFormatters: [
                  FilteringTextInputFormatter.allow(RegExp(r'[+\d\s\-()]')),
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
                  final digits = value.replaceAll(RegExp(r'\D'), '');
                  if (digits.length < 7) {
                    return 'auth.phone_invalid'.tr();
                  }
                  return null;
                },
                onFieldSubmitted: (_) => onSubmit(),
              ),
            ),

            if (errorKey != null) ...[
              const SizedBox(height: 12),
              Text(
                errorKey!.tr(),
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
                height: isSeniorMode ? 72 : 64,
                child: FilledButton.icon(
                  onPressed: isLoading ? null : onSubmit,
                  icon: isLoading
                      ? SizedBox(
                          width: isSeniorMode ? 24 : 20,
                          height: isSeniorMode ? 24 : 20,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            valueColor: AlwaysStoppedAnimation<Color>(colorScheme.onPrimary),
                          ),
                        )
                      : const Icon(Icons.arrow_forward),
                  label: Text(
                    'auth.request_code'.tr(),
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

class _EmailTab extends StatelessWidget {
  const _EmailTab({
    required this.formKey,
    required this.emailController,
    required this.passwordController,
    required this.confirmPasswordController,
    required this.obscurePassword,
    required this.obscureConfirmPassword,
    required this.passwordsMatch,
    required this.isLoading,
    required this.errorKey,
    required this.onTogglePasswordVisibility,
    required this.onToggleConfirmPasswordVisibility,
    required this.onSubmit,
  });

  final GlobalKey<FormState> formKey;
  final TextEditingController emailController;
  final TextEditingController passwordController;
  final TextEditingController confirmPasswordController;
  final bool obscurePassword;
  final bool obscureConfirmPassword;
  final bool passwordsMatch;
  final bool isLoading;
  final String? errorKey;
  final VoidCallback onTogglePasswordVisibility;
  final VoidCallback onToggleConfirmPasswordVisibility;
  final Future<void> Function() onSubmit;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final textTheme = theme.textTheme;
    final colorScheme = theme.colorScheme;
    final isSeniorMode = MediaQuery.of(context).textScaler.textScaleFactor > 1.3;

    return SingleChildScrollView(
      child: Form(
        key: formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const SizedBox(height: 16),

            // Email field
            Semantics(
              label: 'auth.register.email'.tr(),
              child: TextFormField(
                controller: emailController,
                autofocus: true,
                keyboardType: TextInputType.emailAddress,
                textInputAction: TextInputAction.next,
                decoration: InputDecoration(
                  labelText: 'auth.register.email'.tr(),
                  hintText: 'user@example.com',
                  prefixIcon: const Icon(Icons.email_outlined),
                  border: const OutlineInputBorder(),
                ),
                validator: (value) {
                  if (value == null || value.trim().isEmpty) {
                    return 'auth.register.email'.tr();
                  }
                  if (!RegExp(r'^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$').hasMatch(value.trim())) {
                    return 'auth.register.email_invalid'.tr();
                  }
                  return null;
                },
              ),
            ),

            const SizedBox(height: 16),

            // Password field
            Semantics(
              label: 'auth.register.password'.tr(),
              child: TextFormField(
                controller: passwordController,
                obscureText: obscurePassword,
                textInputAction: TextInputAction.next,
                decoration: InputDecoration(
                  labelText: 'auth.register.password'.tr(),
                  hintText: '••••••••',
                  prefixIcon: const Icon(Icons.lock_outline),
                  suffixIcon: IconButton(
                    icon: Icon(obscurePassword ? Icons.visibility_off : Icons.visibility),
                    onPressed: onTogglePasswordVisibility,
                    tooltip: obscurePassword ? 'Show password' : 'Hide password',
                  ),
                  border: const OutlineInputBorder(),
                ),
                validator: (value) {
                  if (value == null || value.isEmpty) {
                    return 'auth.register.password_required'.tr();
                  }
                  if (value.length < 8) {
                    return 'auth.register.password_too_short'.tr();
                  }
                  return null;
                },
              ),
            ),

            const SizedBox(height: 16),

            // Confirm password field
            Semantics(
              label: 'auth.register.confirm_password'.tr(),
              child: TextFormField(
                controller: confirmPasswordController,
                obscureText: obscureConfirmPassword,
                textInputAction: TextInputAction.done,
                decoration: InputDecoration(
                  labelText: 'auth.register.confirm_password'.tr(),
                  hintText: '••••••••',
                  prefixIcon: const Icon(Icons.lock_outline),
                  suffixIcon: IconButton(
                    icon: Icon(obscureConfirmPassword ? Icons.visibility_off : Icons.visibility),
                    onPressed: onToggleConfirmPasswordVisibility,
                    tooltip: obscureConfirmPassword ? 'Show password' : 'Hide password',
                  ),
                  border: const OutlineInputBorder(),
                  errorText: !passwordsMatch && confirmPasswordController.text.isNotEmpty
                      ? 'auth.register.password_mismatch'.tr()
                      : null,
                ),
                validator: (value) {
                  if (value == null || value.isEmpty) {
                    return 'auth.register.confirm_password_required'.tr();
                  }
                  if (value != passwordController.text) {
                    return 'auth.register.password_mismatch'.tr();
                  }
                  return null;
                },
                onFieldSubmitted: (_) => onSubmit(),
              ),
            ),

            if (errorKey != null) ...[
              const SizedBox(height: 12),
              Text(
                errorKey!.tr(),
                style: TextStyle(color: colorScheme.error),
                textAlign: TextAlign.center,
              ),
            ],

            const SizedBox(height: 24),

            // Submit button
            Semantics(
              button: true,
              label: 'auth.register.submit'.tr(),
              child: SizedBox(
                height: isSeniorMode ? 72 : 64,
                child: FilledButton.icon(
                  onPressed: isLoading ? null : onSubmit,
                  icon: isLoading
                      ? SizedBox(
                          width: isSeniorMode ? 24 : 20,
                          height: isSeniorMode ? 24 : 20,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            valueColor: AlwaysStoppedAnimation<Color>(colorScheme.onPrimary),
                          ),
                        )
                      : const Icon(Icons.person_add_alt_1),
                  label: Text(
                    'auth.register.submit'.tr(),
                    style: textTheme.titleMedium?.copyWith(
                      color: colorScheme.onPrimary,
                      fontSize: isSeniorMode ? 18 : null,
                    ),
                  ),
                ),
              ),
            ),

            const SizedBox(height: 16),

            // Helper text
            Text(
              'auth.register.email_verification_pending'.tr(),
              style: textTheme.bodySmall?.copyWith(
                color: colorScheme.onSurfaceVariant,
              ),
              textAlign: TextAlign.center,
            ),
          ],
        ),
      ),
    );
  }
}

class _IdAustriaTab extends StatelessWidget {
  const _IdAustriaTab({
    required this.onGoogleSignIn,
    required this.onIdAustriaSignIn,
  });

  final VoidCallback onGoogleSignIn;
  final VoidCallback onIdAustriaSignIn;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final textTheme = theme.textTheme;
    final colorScheme = theme.colorScheme;
    final isSeniorMode = MediaQuery.of(context).textScaler.textScaleFactor > 1.3;

    return SingleChildScrollView(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const SizedBox(height: 32),

          // Google Sign-In button
          Semantics(
            button: true,
            label: 'auth.google.button_semantic'.tr(),
            child: SizedBox(
              height: isSeniorMode ? 72 : 64,
              child: OutlinedButton.icon(
                onPressed: onGoogleSignIn,
                icon: Image.asset(
                  'assets/images/google_logo.png',
                  width: 24,
                  height: 24,
                  errorBuilder: (_, __, ___) => const Icon(Icons.g_mobiledata, size: 24),
                ),
                label: Text(
                  'auth.google.button'.tr(),
                  style: textTheme.titleMedium?.copyWith(
                    fontSize: isSeniorMode ? 18 : null,
                  ),
                ),
              ),
            ),
          ),

          const SizedBox(height: 16),

          // ID Austria button
          Semantics(
            button: true,
            label: 'auth.id_austria.button_semantic'.tr(),
            child: SizedBox(
              height: isSeniorMode ? 72 : 64,
              child: OutlinedButton.icon(
                onPressed: onIdAustriaSignIn,
                icon: const Icon(Icons.verified_user_outlined, size: 24),
                label: Text(
                  'auth.id_austria.button'.tr(),
                  style: textTheme.titleMedium?.copyWith(
                    fontSize: isSeniorMode ? 18 : null,
                  ),
                ),
              ),
            ),
          ),

          const SizedBox(height: 24),

          // Info text
          Text(
            'Mit ID Austria melden Sie sich mit Ihrer offiziellen österreichischen digitalen Identität an (eIDAS-konform). Dies erhöht Ihre Vertrauensstufe automatisch.',
            style: textTheme.bodyMedium?.copyWith(
              color: colorScheme.onSurfaceVariant,
            ),
            textAlign: TextAlign.center,
          ),
        ],
      ),
    );
  }
}