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

import 'package:dio/dio.dart';
import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:go_router/go_router.dart';

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../core/router/app_router.dart';
import '../data/auth_repository.dart';

class AuthScreen extends StatefulWidget {
  const AuthScreen({super.key, required this.apiClient});

  final ApiClient apiClient;

  @override
  State<AuthScreen> createState() => _AuthScreenState();
}

class _AuthScreenState extends State<AuthScreen> with SingleTickerProviderStateMixin {
  late final AuthRepository _authRepository = AuthRepositoryImpl(widget.apiClient);

  late TabController _tabController;
  final _phoneController = TextEditingController();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _confirmPasswordController = TextEditingController();
  final _phoneFormKey = GlobalKey<FormState>();
  final _emailFormKey = GlobalKey<FormState>();
  bool _isPhoneLoading = false;
  bool _isEmailLoading = false;
  bool _isGoogleLoading = false;
  bool _isIdAustriaLoading = false;
  String? _phoneErrorKey;
  String? _emailErrorKey;
  bool _obscurePassword = true;
  bool _obscureConfirmPassword = true;
  bool _passwordsMatch = true;
  bool _isLoginMode = false;

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

  String _errorKeyFor(Object error) {
    if (error is DioException) return mapDioError(error).l10nKey;
    return 'errors.generic';
  }

  Future<void> _submitPhone() async {
    if (!_phoneFormKey.currentState!.validate()) return;
    setState(() {
      _isPhoneLoading = true;
      _phoneErrorKey = null;
    });

    final phone = _phoneController.text.trim();

    try {
      await _authRepository.requestPhoneOtp(phone);
      if (mounted) {
        context.push(
          '${AppRoutes.otpVerify}?phone=${Uri.encodeComponent(phone)}&purpose=login',
        );
      }
    } catch (e) {
      setState(() => _phoneErrorKey = _errorKeyFor(e));
    } finally {
      if (mounted) setState(() => _isPhoneLoading = false);
    }
  }

  void _toggleEmailMode() {
    setState(() {
      _isLoginMode = !_isLoginMode;
      _emailErrorKey = null;
      _confirmPasswordController.clear();
    });
  }

  Future<void> _submitEmail() async {
    if (!_emailFormKey.currentState!.validate()) return;

    if (!_isLoginMode) {
      if (!_passwordsMatch) {
        setState(() => _emailErrorKey = 'auth.register.password_mismatch');
        return;
      }
      if (_passwordController.text.length < 8) {
        setState(() => _emailErrorKey = 'auth.register.password_too_short');
        return;
      }
    }

    setState(() {
      _isEmailLoading = true;
      _emailErrorKey = null;
    });

    final email = _emailController.text.trim();
    final password = _passwordController.text;

    try {
      if (_isLoginMode) {
        await _authRepository.emailPasswordLogin(email: email, password: password);
        if (mounted) context.go(AppRoutes.home);
      } else {
        await _authRepository.registerEmailPassword(
          email: email,
          password: password,
          confirmPassword: _confirmPasswordController.text,
        );
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text('auth.register.email_verification_sent'.tr())),
          );
          setState(() {
            _isLoginMode = true;
            _passwordController.clear();
            _confirmPasswordController.clear();
          });
        }
      }
    } catch (e) {
      setState(() => _emailErrorKey = _errorKeyFor(e));
    } finally {
      if (mounted) setState(() => _isEmailLoading = false);
    }
  }

  Future<void> _googleSignIn() async {
    setState(() => _isGoogleLoading = true);
    try {
      // Backend validator is a local-development stub (ADR-021 §3, "staging
      // mock providers for local development") until real Google OAuth
      // credentials are provisioned for this project.
      final devToken = 'dev-google-${DateTime.now().microsecondsSinceEpoch}';
      await _authRepository.googleSignIn(devToken);
      if (mounted) context.go(AppRoutes.home);
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(_errorKeyFor(e).tr())),
        );
      }
    } finally {
      if (mounted) setState(() => _isGoogleLoading = false);
    }
  }

  Future<void> _idAustriaSignIn() async {
    setState(() => _isIdAustriaLoading = true);
    try {
      // Backend eIDAS client is a local-development stub (ADR-021 §3) until
      // the real ID Austria federation endpoints are integrated.
      final devCode = 'dev-id-austria-${DateTime.now().microsecondsSinceEpoch}';
      await _authRepository.idAustriaSignIn(code: devCode);
      if (mounted) context.go(AppRoutes.home);
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(_errorKeyFor(e).tr())),
        );
      }
    } finally {
      if (mounted) setState(() => _isIdAustriaLoading = false);
    }
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
                          isLoginMode: _isLoginMode,
                          onToggleMode: _toggleEmailMode,
                          onTogglePasswordVisibility: () => setState(() => _obscurePassword = !_obscurePassword),
                          onToggleConfirmPasswordVisibility: () => setState(() => _obscureConfirmPassword = !_obscureConfirmPassword),
                          onSubmit: _submitEmail,
                        ),
                        // Google / ID Austria tab
                        _IdAustriaTab(
                          onGoogleSignIn: _googleSignIn,
                          onIdAustriaSignIn: _idAustriaSignIn,
                          isGoogleLoading: _isGoogleLoading,
                          isIdAustriaLoading: _isIdAustriaLoading,
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
    required this.isLoginMode,
    required this.onToggleMode,
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
  final bool isLoginMode;
  final VoidCallback onToggleMode;
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
                onFieldSubmitted: isLoginMode ? (_) => onSubmit() : null,
                validator: (value) {
                  if (value == null || value.isEmpty) {
                    return 'auth.register.password_required'.tr();
                  }
                  if (!isLoginMode && value.length < 8) {
                    return 'auth.register.password_too_short'.tr();
                  }
                  return null;
                },
              ),
            ),

            if (!isLoginMode) ...[
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
                    if (isLoginMode) return null;
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
            ],

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
              label: (isLoginMode ? 'auth.login.submit' : 'auth.register.submit').tr(),
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
                      : Icon(isLoginMode ? Icons.login : Icons.person_add_alt_1),
                  label: Text(
                    (isLoginMode ? 'auth.login.submit' : 'auth.register.submit').tr(),
                    style: textTheme.titleMedium?.copyWith(
                      color: colorScheme.onPrimary,
                      fontSize: isSeniorMode ? 18 : null,
                    ),
                  ),
                ),
              ),
            ),

            const SizedBox(height: 12),

            // Toggle between register and login
            TextButton(
              onPressed: onToggleMode,
              child: Text(
                (isLoginMode ? 'auth.login.switch_to_register' : 'auth.login.switch_to_login').tr(),
              ),
            ),

            if (!isLoginMode) ...[
              const SizedBox(height: 4),
              Text(
                'auth.register.email_verification_pending'.tr(),
                style: textTheme.bodySmall?.copyWith(
                  color: colorScheme.onSurfaceVariant,
                ),
                textAlign: TextAlign.center,
              ),
            ],
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
    required this.isGoogleLoading,
    required this.isIdAustriaLoading,
  });

  final VoidCallback onGoogleSignIn;
  final VoidCallback onIdAustriaSignIn;
  final bool isGoogleLoading;
  final bool isIdAustriaLoading;

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
                onPressed: isGoogleLoading ? null : onGoogleSignIn,
                icon: isGoogleLoading
                    ? SizedBox(
                        width: isSeniorMode ? 24 : 20,
                        height: isSeniorMode ? 24 : 20,
                        child: const CircularProgressIndicator(strokeWidth: 2),
                      )
                    : Image.asset(
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
                onPressed: isIdAustriaLoading ? null : onIdAustriaSignIn,
                icon: isIdAustriaLoading
                    ? SizedBox(
                        width: isSeniorMode ? 24 : 20,
                        height: isSeniorMode ? 24 : 20,
                        child: const CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.verified_user_outlined, size: 24),
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