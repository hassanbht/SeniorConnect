// lib/features/auth/presentation/email_link_screen.dart
//
// P1-11: Email magic-link (OTP-style) passwordless sign-in — for users with
// no phone number and no password. Two steps: request code -> enter code.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/network/api_client.dart';
import '../../../core/router/app_router.dart';
import '../application/email_link_notifier.dart';

class EmailLinkScreen extends ConsumerStatefulWidget {
  const EmailLinkScreen({super.key, required this.apiClient});

  final ApiClient apiClient;

  @override
  ConsumerState<EmailLinkScreen> createState() => _EmailLinkScreenState();
}

class _EmailLinkScreenState extends ConsumerState<EmailLinkScreen> {
  final _emailFormKey = GlobalKey<FormState>();
  final _emailController = TextEditingController();
  final _codeController = TextEditingController();

  @override
  void dispose() {
    _emailController.dispose();
    _codeController.dispose();
    super.dispose();
  }

  Future<void> _requestCode() async {
    if (!_emailFormKey.currentState!.validate()) return;
    final notifier = ref.read(emailLinkProvider(widget.apiClient).notifier);
    await notifier.requestCode(_emailController.text.trim());
  }

  Future<void> _verifyCode(String code) async {
    if (code.length != 6) return;
    final notifier = ref.read(emailLinkProvider(widget.apiClient).notifier);
    final success = await notifier.verifyCode(_emailController.text.trim(), code);
    if (success && mounted) {
      context.go(AppRoutes.home);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final textTheme = theme.textTheme;
    final colorScheme = theme.colorScheme;
    final emailState = ref.watch(emailLinkProvider(widget.apiClient));

    return Scaffold(
      appBar: AppBar(
        leading: Semantics(
          label: 'semantic.back_button'.tr(),
          child: const BackButton(),
        ),
        title: Text('auth.email_link.title'.tr()),
        elevation: 0,
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 480),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 32),
              child: emailState.codeSent
                  ? _buildCodeStep(textTheme, colorScheme, emailState)
                  : _buildEmailStep(textTheme, colorScheme, emailState),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildEmailStep(
    TextTheme textTheme,
    ColorScheme colorScheme,
    EmailLinkState emailState,
  ) {
    return Form(
      key: _emailFormKey,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            'auth.email_link.description'.tr(),
            style: textTheme.bodyLarge,
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: 32),
          TextFormField(
            controller: _emailController,
            autofocus: true,
            keyboardType: TextInputType.emailAddress,
            textInputAction: TextInputAction.done,
            decoration: InputDecoration(
              labelText: 'auth.email_link.email_label'.tr(),
              hintText: 'user@example.com',
              prefixIcon: const Icon(Icons.email_outlined),
              border: const OutlineInputBorder(),
            ),
            validator: (value) {
              if (value == null || value.trim().isEmpty) {
                return 'auth.email_link.email_label'.tr();
              }
              if (!RegExp(r'^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$')
                  .hasMatch(value.trim())) {
                return 'auth.register.email_invalid'.tr();
              }
              return null;
            },
            onFieldSubmitted: (_) => _requestCode(),
          ),
          if (emailState.errorKey != null) ...[
            const SizedBox(height: 12),
            Text(
              emailState.errorKey!.tr(),
              style: TextStyle(color: colorScheme.error),
              textAlign: TextAlign.center,
            ),
          ],
          const SizedBox(height: 24),
          SizedBox(
            height: 64,
            child: FilledButton(
              onPressed: emailState.isLoading ? null : _requestCode,
              child: emailState.isLoading
                  ? const CircularProgressIndicator()
                  : Text('auth.email_link.request'.tr()),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildCodeStep(
    TextTheme textTheme,
    ColorScheme colorScheme,
    EmailLinkState emailState,
  ) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          'auth.email_link.sent'.tr(),
          style: textTheme.bodyLarge,
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: 32),
        Semantics(
          label: 'auth.email_link.code_label'.tr(),
          child: TextFormField(
            controller: _codeController,
            autofocus: true,
            keyboardType: TextInputType.number,
            textAlign: TextAlign.center,
            maxLength: 6,
            style: textTheme.headlineMedium?.copyWith(
              letterSpacing: 8,
              fontWeight: FontWeight.w700,
            ),
            inputFormatters: [FilteringTextInputFormatter.digitsOnly],
            decoration: const InputDecoration(
              counterText: '',
              hintText: '------',
              border: OutlineInputBorder(),
            ),
            onChanged: (value) {
              if (value.length == 6) _verifyCode(value);
            },
          ),
        ),
        if (emailState.errorKey != null) ...[
          const SizedBox(height: 12),
          Text(
            emailState.errorKey!.tr(),
            style: TextStyle(color: colorScheme.error),
            textAlign: TextAlign.center,
          ),
        ],
        const SizedBox(height: 24),
        if (emailState.isLoading)
          const Center(child: CircularProgressIndicator()),
        TextButton(
          onPressed: emailState.isLoading
              ? null
              : () {
                  ref
                      .read(emailLinkProvider(widget.apiClient).notifier)
                      .requestCode(_emailController.text.trim());
                  _codeController.clear();
                },
          child: Text('auth.verify_phone.resend'.tr()),
        ),
      ],
    );
  }
}
