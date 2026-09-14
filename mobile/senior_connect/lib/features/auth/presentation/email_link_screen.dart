// lib/features/auth/presentation/email_link_screen.dart
//
// P1-11: Email magic-link (OTP-style) passwordless sign-in — for users with
// no phone number and no password. Two steps: request code -> enter code.

import 'package:dio/dio.dart';
import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:go_router/go_router.dart';

import '../../../core/network/api_client.dart';
import '../../../core/router/app_router.dart';
import '../data/auth_repository.dart';

class EmailLinkScreen extends StatefulWidget {
  const EmailLinkScreen({super.key, required this.apiClient});

  final ApiClient apiClient;

  @override
  State<EmailLinkScreen> createState() => _EmailLinkScreenState();
}

class _EmailLinkScreenState extends State<EmailLinkScreen> {
  late final AuthRepository _authRepository = AuthRepositoryImpl(widget.apiClient);

  final _emailFormKey = GlobalKey<FormState>();
  final _emailController = TextEditingController();
  final _codeController = TextEditingController();

  bool _codeSent = false;
  bool _isLoading = false;
  String? _errorKey;

  @override
  void dispose() {
    _emailController.dispose();
    _codeController.dispose();
    super.dispose();
  }

  String _errorKeyFor(Object error) =>
      error is DioException ? mapDioError(error).l10nKey : 'errors.generic';

  Future<void> _requestCode() async {
    if (!_emailFormKey.currentState!.validate()) return;
    setState(() {
      _isLoading = true;
      _errorKey = null;
    });

    try {
      await _authRepository.requestEmailMagicLink(_emailController.text.trim());
      if (mounted) setState(() => _codeSent = true);
    } catch (e) {
      setState(() => _errorKey = _errorKeyFor(e));
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  Future<void> _verifyCode(String code) async {
    if (code.length != 6 || _isLoading) return;
    setState(() {
      _isLoading = true;
      _errorKey = null;
    });

    try {
      await _authRepository.verifyEmailMagicLink(_emailController.text.trim(), code);
      if (mounted) context.go(AppRoutes.home);
    } catch (e) {
      setState(() => _errorKey = _errorKeyFor(e));
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
              child: _codeSent ? _buildCodeStep(textTheme, colorScheme) : _buildEmailStep(textTheme, colorScheme),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildEmailStep(TextTheme textTheme, ColorScheme colorScheme) {
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
              if (!RegExp(r'^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$').hasMatch(value.trim())) {
                return 'auth.register.email_invalid'.tr();
              }
              return null;
            },
            onFieldSubmitted: (_) => _requestCode(),
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
          SizedBox(
            height: 64,
            child: FilledButton(
              onPressed: _isLoading ? null : _requestCode,
              child: _isLoading
                  ? const CircularProgressIndicator()
                  : Text('auth.email_link.request'.tr()),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildCodeStep(TextTheme textTheme, ColorScheme colorScheme) {
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
            style: textTheme.headlineMedium?.copyWith(letterSpacing: 8, fontWeight: FontWeight.w700),
            inputFormatters: [FilteringTextInputFormatter.digitsOnly],
            decoration: const InputDecoration(counterText: '', hintText: '------', border: OutlineInputBorder()),
            onChanged: (value) {
              if (value.length == 6) _verifyCode(value);
            },
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
        if (_isLoading) const Center(child: CircularProgressIndicator()),
        TextButton(
          onPressed: _isLoading
              ? null
              : () => setState(() {
                    _codeSent = false;
                    _codeController.clear();
                  }),
          child: Text('auth.verify_phone.resend'.tr()),
        ),
      ],
    );
  }
}
