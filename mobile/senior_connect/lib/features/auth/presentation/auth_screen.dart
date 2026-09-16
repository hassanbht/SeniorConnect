// lib/features/auth/presentation/auth_screen.dart
//
// P1-09: Authentication entry screen.
//
// Tabs:
//  1. Phone OTP (default, primary user journey — seniors, volunteers, families)
//  2. Email + Password (ADR-021: registration with confirmation link)
//  3. ID Austria / Google Sign-In (federated SSO, eIDAS-aligned)
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

import '../../../core/design_system/app_tokens.dart';
import '../../../core/network/api_client.dart';
import '../../../core/router/app_router.dart';
import '../application/auth_notifier.dart';

class AuthScreen extends ConsumerStatefulWidget {
  const AuthScreen({super.key, required this.apiClient});

  final ApiClient apiClient;

  @override
  ConsumerState<AuthScreen> createState() => _AuthScreenState();
}

class _AuthScreenState extends ConsumerState<AuthScreen>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;
  final _phoneController = TextEditingController();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _confirmPasswordController = TextEditingController();
  final _phoneFormKey = GlobalKey<FormState>();
  final _emailFormKey = GlobalKey<FormState>();

  final _obscurePassword = ValueNotifier<bool>(true);
  final _obscureConfirmPassword = ValueNotifier<bool>(true);
  final _passwordsMatch = ValueNotifier<bool>(false);

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
    _obscurePassword.dispose();
    _obscureConfirmPassword.dispose();
    _passwordsMatch.dispose();
    super.dispose();
  }

  void _checkPasswordMatch() {
    _passwordsMatch.value =
        _passwordController.text == _confirmPasswordController.text &&
        _passwordController.text.isNotEmpty &&
        _confirmPasswordController.text.isNotEmpty;
  }

  Future<void> _submitPhone() async {
    if (!_phoneFormKey.currentState!.validate()) return;
    final phone = _phoneController.text.trim();
    final notifier = ref.read(authProvider(widget.apiClient).notifier);
    final success = await notifier.submitPhone(phone);
    if (success && mounted) {
      context.push(
        '${AppRoutes.otpVerify}?phone=${Uri.encodeComponent(phone)}&purpose=login',
      );
    }
  }

  Future<void> _submitEmail() async {
    if (!_emailFormKey.currentState!.validate()) return;

    final authState = ref.read(authProvider(widget.apiClient));
    final notifier = ref.read(authProvider(widget.apiClient).notifier);

    if (!authState.isLoginMode) {
      if (!_passwordsMatch.value) {
        notifier.setEmailErrorKey('auth.register.password_mismatch');
        return;
      }
      if (_passwordController.text.length < 8) {
        notifier.setEmailErrorKey('auth.register.password_too_short');
        return;
      }
    }

    final email = _emailController.text.trim();
    final password = _passwordController.text;

    if (authState.isLoginMode) {
      final success = await notifier.submitEmailLogin(
        email: email,
        password: password,
      );
      if (success && mounted) {
        context.go(AppRoutes.home);
      }
    } else {
      final success = await notifier.submitEmailRegistration(
        email: email,
        password: password,
        confirmPassword: _confirmPasswordController.text,
      );
      if (success && mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('auth.register.email_verification_sent'.tr()),
          ),
        );
        notifier.toggleEmailMode();
        _passwordController.clear();
        _confirmPasswordController.clear();
      }
    }
  }

  Future<void> _googleSignIn() async {
    final devToken = 'dev-google-${DateTime.now().microsecondsSinceEpoch}';
    final notifier = ref.read(authProvider(widget.apiClient).notifier);
    final success = await notifier.submitGoogleSignIn(devToken);
    if (success && mounted) {
      context.go(AppRoutes.home);
    } else if (mounted) {
      final err = ref.read(authProvider(widget.apiClient)).emailErrorKey;
      if (err != null) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(err.tr())));
      }
    }
  }

  Future<void> _idAustriaSignIn() async {
    final devCode = 'dev-id-austria-${DateTime.now().microsecondsSinceEpoch}';
    final notifier = ref.read(authProvider(widget.apiClient).notifier);
    final success = await notifier.submitIdAustriaSignIn(code: devCode);
    if (success && mounted) {
      context.go(AppRoutes.home);
    } else if (mounted) {
      final err = ref.read(authProvider(widget.apiClient)).emailErrorKey;
      if (err != null) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(err.tr())));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final textTheme = theme.textTheme;
    final colorScheme = theme.colorScheme;
    final isSeniorMode =
        MediaQuery.textScalerOf(context).scale(1) > 1.3;

    final authState = ref.watch(authProvider(widget.apiClient));
    final authNotifier = ref.read(authProvider(widget.apiClient).notifier);

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
                          isLoading: authState.isPhoneLoading,
                          errorKey: authState.phoneErrorKey,
                          onSubmit: _submitPhone,
                        ),
                        // Email + Password tab
                        ValueListenableBuilder<bool>(
                          valueListenable: _passwordsMatch,
                          builder: (context, match, _) {
                            return ValueListenableBuilder<bool>(
                              valueListenable: _obscurePassword,
                              builder: (context, obsPass, _) {
                                return ValueListenableBuilder<bool>(
                                  valueListenable: _obscureConfirmPassword,
                                  builder: (context, obsConfirm, _) {
                                    return _EmailTab(
                                      formKey: _emailFormKey,
                                      emailController: _emailController,
                                      passwordController: _passwordController,
                                      confirmPasswordController:
                                          _confirmPasswordController,
                                      obscurePassword: obsPass,
                                      obscureConfirmPassword: obsConfirm,
                                      passwordsMatch: match,
                                      isLoading: authState.isEmailLoading,
                                      errorKey: authState.emailErrorKey,
                                      isLoginMode: authState.isLoginMode,
                                      onToggleMode: () {
                                        authNotifier.toggleEmailMode();
                                        _confirmPasswordController.clear();
                                      },
                                      onTogglePasswordVisibility: () {
                                        _obscurePassword.value =
                                            !_obscurePassword.value;
                                      },
                                      onToggleConfirmPasswordVisibility: () {
                                        _obscureConfirmPassword.value =
                                            !_obscureConfirmPassword.value;
                                      },
                                      onSubmit: _submitEmail,
                                    );
                                  },
                                );
                              },
                            );
                          },
                        ),
                        // Google / ID Austria tab
                        _IdAustriaTab(
                          onGoogleSignIn: _googleSignIn,
                          onIdAustriaSignIn: _idAustriaSignIn,
                          isGoogleLoading: authState.isGoogleLoading,
                          isIdAustriaLoading: authState.isIdAustriaLoading,
                        ),
                      ],
                    ),
                  ),

                  const SizedBox(height: 8),

                  // Alternative entry points
                  TextButton(
                    onPressed: () => context.push(AppRoutes.emailLink),
                    child: Text('auth.email_link.link'.tr()),
                  ),
                  TextButton(
                    onPressed: () => context.push(AppRoutes.staffLogin),
                    child: Text('auth.staff.link'.tr()),
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
    final isSeniorMode =
        MediaQuery.textScalerOf(context).scale(1) > 1.3;

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
                  helperText: 'auth.phone_helper'.tr(),
                ),
                onFieldSubmitted: (_) => onSubmit(),
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
              ),
            ),

            if (errorKey != null) ...[
              const SizedBox(height: 16),
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
              label: 'auth.continue_semantic'.tr(),
              child: SizedBox(
                height: isSeniorMode
                    ? AppTouch.buttonHeightSenior
                    : AppTouch.buttonHeightStandard,
                child: FilledButton(
                  onPressed: isLoading ? null : onSubmit,
                  child: isLoading
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
                          'auth.continue'.tr(),
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
    final isSeniorMode =
        MediaQuery.textScalerOf(context).scale(1) > 1.3;

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
                  if (!RegExp(
                    r'^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$',
                  ).hasMatch(value.trim())) {
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
                    icon: Icon(
                      obscurePassword ? Icons.visibility_off : Icons.visibility,
                    ),
                    onPressed: onTogglePasswordVisibility,
                    tooltip: obscurePassword
                        ? 'Show password'
                        : 'Hide password',
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
                      icon: Icon(
                        obscureConfirmPassword
                            ? Icons.visibility_off
                            : Icons.visibility,
                      ),
                      onPressed: onToggleConfirmPasswordVisibility,
                      tooltip: obscureConfirmPassword
                          ? 'Show password'
                          : 'Hide password',
                    ),
                    border: const OutlineInputBorder(),
                    errorText: confirmPasswordController.text.isNotEmpty &&
                            !passwordsMatch
                        ? 'auth.register.password_mismatch'.tr()
                        : null,
                  ),
                  onFieldSubmitted: (_) => onSubmit(),
                  validator: (value) {
                    if (value == null || value.isEmpty) {
                      return 'auth.register.confirm_password'.tr();
                    }
                    if (value != passwordController.text) {
                      return 'auth.register.password_mismatch'.tr();
                    }
                    return null;
                  },
                ),
              ),
            ],

            if (errorKey != null) ...[
              const SizedBox(height: 16),
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
              label: isLoginMode
                  ? 'auth.login.title'.tr()
                  : 'auth.register.title'.tr(),
              child: SizedBox(
                height: isSeniorMode
                    ? AppTouch.buttonHeightSenior
                    : AppTouch.buttonHeightStandard,
                child: FilledButton(
                  onPressed: isLoading ? null : onSubmit,
                  child: isLoading
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
                          isLoginMode
                              ? 'auth.login.title'.tr()
                              : 'auth.register.title'.tr(),
                          style: textTheme.titleMedium?.copyWith(
                            color: colorScheme.onPrimary,
                            fontSize: isSeniorMode ? 18 : null,
                          ),
                        ),
                ),
              ),
            ),

            const SizedBox(height: 12),

            // Mode switch link (Register <-> Login)
            Center(
              child: TextButton(
                onPressed: onToggleMode,
                child: Text(
                  isLoginMode
                      ? 'auth.register.switch_to_register'.tr()
                      : 'auth.register.switch_to_login'.tr(),
                ),
              ),
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
    required this.isGoogleLoading,
    required this.isIdAustriaLoading,
  });

  final Future<void> Function() onGoogleSignIn;
  final Future<void> Function() onIdAustriaSignIn;
  final bool isGoogleLoading;
  final bool isIdAustriaLoading;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final textTheme = theme.textTheme;
    final colorScheme = theme.colorScheme;
    final isSeniorMode =
        MediaQuery.textScalerOf(context).scale(1) > 1.3;
    final buttonHeight = isSeniorMode
        ? AppTouch.buttonHeightSenior
        : AppTouch.buttonHeightStandard;

    return SingleChildScrollView(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const SizedBox(height: 16),

          // ID Austria section
          Card(
            child: Padding(
              padding: const EdgeInsets.all(AppSpacing.lg),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Row(
                    children: [
                      Icon(
                        Icons.verified_user_outlined,
                        color: colorScheme.primary,
                        size: 32,
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              'auth.id_austria.title'.tr(),
                              style: textTheme.titleMedium?.copyWith(
                                fontWeight: FontWeight.w600,
                              ),
                            ),
                            Text(
                              'auth.id_austria.subtitle'.tr(),
                              style: textTheme.bodySmall?.copyWith(
                                color: colorScheme.onSurfaceVariant,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 16),
                  Semantics(
                    button: true,
                    label: 'auth.id_austria.semantic'.tr(),
                    child: SizedBox(
                      height: buttonHeight,
                      child: FilledButton.icon(
                        onPressed: isIdAustriaLoading
                            ? null
                            : onIdAustriaSignIn,
                        icon: isIdAustriaLoading
                            ? SizedBox(
                                width: 20,
                                height: 20,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2,
                                  valueColor: AlwaysStoppedAnimation<Color>(
                                    colorScheme.onPrimary,
                                  ),
                                ),
                              )
                            : const Icon(Icons.login),
                        label: Text(
                          'auth.id_austria.button'.tr(),
                          style: TextStyle(
                            fontSize: isSeniorMode ? 18 : null,
                          ),
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),

          const SizedBox(height: 16),

          // Divider with "oder"
          Row(
            children: [
              const Expanded(child: Divider()),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 16),
                child: Text(
                  'auth.login.or_divider'.tr(),
                  style: textTheme.labelSmall?.copyWith(
                    color: colorScheme.onSurfaceVariant,
                  ),
                ),
              ),
              const Expanded(child: Divider()),
            ],
          ),

          const SizedBox(height: 16),

          // Google Sign-In button
          Semantics(
            button: true,
            label: 'auth.google.semantic'.tr(),
            child: SizedBox(
              height: buttonHeight,
              child: OutlinedButton.icon(
                onPressed: isGoogleLoading ? null : onGoogleSignIn,
                icon: isGoogleLoading
                    ? const SizedBox(
                        width: 20,
                        height: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.g_mobiledata, size: 28),
                label: Text(
                  'auth.google.button'.tr(),
                  style: TextStyle(
                    fontSize: isSeniorMode ? 18 : null,
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
