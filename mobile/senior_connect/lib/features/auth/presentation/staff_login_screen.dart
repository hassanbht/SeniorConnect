// lib/features/auth/presentation/staff_login_screen.dart
//
// P1-12: Organization staff / platform admin login — password + optional
// TOTP authenticator code. NOT the senior/volunteer-facing flow (BR-AUTH-02).

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/network/api_client.dart';
import '../../../core/router/app_router.dart';
import '../application/staff_login_notifier.dart';

class StaffLoginScreen extends ConsumerStatefulWidget {
  const StaffLoginScreen({super.key, required this.apiClient});

  final ApiClient apiClient;

  @override
  ConsumerState<StaffLoginScreen> createState() => _StaffLoginScreenState();
}

class _StaffLoginScreenState extends ConsumerState<StaffLoginScreen> {
  final _formKey = GlobalKey<FormState>();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _totpController = TextEditingController();
  final _obscurePassword = ValueNotifier<bool>(true);

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    _totpController.dispose();
    _obscurePassword.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    final notifier = ref.read(staffLoginProvider(widget.apiClient).notifier);
    final success = await notifier.submit(
      email: _emailController.text.trim(),
      password: _passwordController.text,
      totpCode: _totpController.text.trim().isEmpty
          ? null
          : _totpController.text.trim(),
    );
    if (success && mounted) {
      context.go(AppRoutes.home);
    }
  }

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final staffState = ref.watch(staffLoginProvider(widget.apiClient));

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
                    ValueListenableBuilder<bool>(
                      valueListenable: _obscurePassword,
                      builder: (context, obscure, _) {
                        return TextFormField(
                          controller: _passwordController,
                          obscureText: obscure,
                          textInputAction: staffState.totpRequired
                              ? TextInputAction.next
                              : TextInputAction.done,
                          decoration: InputDecoration(
                            labelText: 'auth.staff.password_label'.tr(),
                            prefixIcon: const Icon(Icons.lock_outline),
                            suffixIcon: IconButton(
                              icon: Icon(obscure
                                  ? Icons.visibility_off
                                  : Icons.visibility),
                              onPressed: () =>
                                  _obscurePassword.value = !_obscurePassword.value,
                            ),
                            border: const OutlineInputBorder(),
                          ),
                          validator: (value) =>
                              (value == null || value.isEmpty)
                                  ? 'auth.register.password_required'.tr()
                                  : null,
                          onFieldSubmitted: staffState.totpRequired
                              ? null
                              : (_) => _submit(),
                        );
                      },
                    ),
                    if (staffState.totpRequired) ...[
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
                    if (staffState.errorKey != null) ...[
                      const SizedBox(height: 12),
                      Text(
                        staffState.errorKey!.tr(),
                        style: TextStyle(color: colorScheme.error),
                        textAlign: TextAlign.center,
                      ),
                    ],
                    const SizedBox(height: 24),
                    SizedBox(
                      height: 64,
                      child: FilledButton(
                        onPressed: staffState.isLoading ? null : _submit,
                        child: staffState.isLoading
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
