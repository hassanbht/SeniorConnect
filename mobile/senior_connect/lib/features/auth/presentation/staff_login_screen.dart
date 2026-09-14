// lib/features/auth/presentation/staff_login_screen.dart
//
// P1-12: Organization staff / platform admin login — password + optional
// TOTP authenticator code. NOT the senior/volunteer-facing flow (BR-AUTH-02).

import 'package:dio/dio.dart';
import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:go_router/go_router.dart';

import '../../../core/network/api_client.dart';
import '../../../core/router/app_router.dart';
import '../data/auth_repository.dart';

class StaffLoginScreen extends StatefulWidget {
  const StaffLoginScreen({super.key, required this.apiClient});

  final ApiClient apiClient;

  @override
  State<StaffLoginScreen> createState() => _StaffLoginScreenState();
}

class _StaffLoginScreenState extends State<StaffLoginScreen> {
  late final AuthRepository _authRepository = AuthRepositoryImpl(widget.apiClient);

  final _formKey = GlobalKey<FormState>();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _totpController = TextEditingController();

  bool _obscurePassword = true;
  bool _isLoading = false;
  bool _totpRequired = false;
  String? _errorKey;

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    _totpController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() {
      _isLoading = true;
      _errorKey = null;
    });

    try {
      await _authRepository.staffLogin(
        email: _emailController.text.trim(),
        password: _passwordController.text,
        totpCode: _totpController.text.trim().isEmpty ? null : _totpController.text.trim(),
      );
      if (mounted) context.go(AppRoutes.home);
    } on DioException catch (e) {
      final code = (e.response?.data is Map) ? (e.response?.data['code'] as String?) : null;
      if (code == 'TOTP_CODE_REQUIRED') {
        setState(() {
          _totpRequired = true;
          _errorKey = 'auth.staff.totp_required';
        });
      } else {
        setState(() => _errorKey = mapDioError(e).l10nKey);
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
    final colorScheme = theme.colorScheme;

    return Scaffold(
      appBar: AppBar(
        leading: Semantics(
          label: 'semantic.back_button'.tr(),
          child: const BackButton(),
        ),
        title: Text('auth.staff.title'.tr()),
        elevation: 0,
      ),
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
                    TextFormField(
                      controller: _emailController,
                      autofocus: true,
                      keyboardType: TextInputType.emailAddress,
                      textInputAction: TextInputAction.next,
                      decoration: InputDecoration(
                        labelText: 'auth.staff.email_label'.tr(),
                        prefixIcon: const Icon(Icons.email_outlined),
                        border: const OutlineInputBorder(),
                      ),
                      validator: (value) => (value == null || value.trim().isEmpty)
                          ? 'auth.staff.email_label'.tr()
                          : null,
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _passwordController,
                      obscureText: _obscurePassword,
                      textInputAction:
                          _totpRequired ? TextInputAction.next : TextInputAction.done,
                      decoration: InputDecoration(
                        labelText: 'auth.staff.password_label'.tr(),
                        prefixIcon: const Icon(Icons.lock_outline),
                        suffixIcon: IconButton(
                          icon: Icon(_obscurePassword ? Icons.visibility_off : Icons.visibility),
                          onPressed: () => setState(() => _obscurePassword = !_obscurePassword),
                        ),
                        border: const OutlineInputBorder(),
                      ),
                      validator: (value) => (value == null || value.isEmpty)
                          ? 'auth.register.password_required'.tr()
                          : null,
                      onFieldSubmitted: _totpRequired ? null : (_) => _submit(),
                    ),
                    if (_totpRequired) ...[
                      const SizedBox(height: 16),
                      TextFormField(
                        controller: _totpController,
                        autofocus: true,
                        keyboardType: TextInputType.number,
                        textInputAction: TextInputAction.done,
                        inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                        decoration: InputDecoration(
                          labelText: 'auth.staff.totp_label'.tr(),
                          helperText: 'auth.staff.totp_hint'.tr(),
                          prefixIcon: const Icon(Icons.verified_user_outlined),
                          border: const OutlineInputBorder(),
                        ),
                        onFieldSubmitted: (_) => _submit(),
                      ),
                    ],
                    if (_errorKey != null) ...[
                      const SizedBox(height: 12),
                      Text(
                        _errorKey!.tr(),
                        style: TextStyle(color: colorScheme.error),
                        textAlign: TextAlign.center,
                      ),
                    ],
                    const SizedBox(height: 24),
                    SizedBox(
                      height: 64,
                      child: FilledButton(
                        onPressed: _isLoading ? null : _submit,
                        child: _isLoading
                            ? const CircularProgressIndicator()
                            : Text('auth.staff.submit'.tr()),
                      ),
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
