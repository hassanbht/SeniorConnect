// lib/features/auth/presentation/otp_verify_screen.dart
//
// P1-27: OTP code entry screen.
//
// BR-AUTH-04: 5-minute expiry, max 5 attempts (enforced server-side).
// BR-AUTH-03: No password — this is purely OTP code entry.
//
// UX goal: phone → code → in, under 30 seconds.
// The code field auto-submits when 6 digits are entered.

import 'package:dio/dio.dart';
import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:go_router/go_router.dart';

import '../../../core/network/api_client.dart';
import '../../../core/router/app_router.dart';
import '../data/auth_repository.dart';

/// Which flow this OTP screen is completing.
enum OtpPurpose {
  /// Phone OTP sign-in / auto-registration (default).
  login,

  /// In-profile "Verify Phone Number" (P1-13b). On success, pops with `true`
  /// instead of navigating home.
  phoneVerification,
}

class OtpVerifyScreen extends StatefulWidget {
  const OtpVerifyScreen({
    super.key,
    required this.phone,
    required this.apiClient,
    this.purpose = OtpPurpose.login,
  });

  final String phone;
  final ApiClient apiClient;
  final OtpPurpose purpose;

  @override
  State<OtpVerifyScreen> createState() => _OtpVerifyScreenState();
}

class _OtpVerifyScreenState extends State<OtpVerifyScreen> {
  late final AuthRepository _authRepository = AuthRepositoryImpl(widget.apiClient);

  final _codeController = TextEditingController();
  bool _isLoading = false;
  String? _errorKey;
  int _remainingAttempts = 5;
  int _secondsLeft = 300; // 5 minutes

  @override
  void initState() {
    super.initState();
    _startCountdown();
  }

  void _startCountdown() {
    Future.delayed(const Duration(seconds: 1), () {
      if (!mounted) return;
      if (_secondsLeft > 0) {
        setState(() => _secondsLeft--);
        _startCountdown();
      }
    });
  }

  @override
  void dispose() {
    _codeController.dispose();
    super.dispose();
  }

  Future<void> _verify(String code) async {
    if (code.length != 6 || _isLoading) return;
    setState(() {
      _isLoading = true;
      _errorKey = null;
    });

    try {
      switch (widget.purpose) {
        case OtpPurpose.login:
          await _authRepository.verifyPhoneOtp(widget.phone, code);
          if (mounted) context.go(AppRoutes.home);
        case OtpPurpose.phoneVerification:
          await _authRepository.verifyProfilePhone(widget.phone, code);
          if (mounted) context.pop(true);
      }
    } catch (e) {
      setState(() {
        _errorKey = e is DioException ? mapDioError(e).l10nKey : 'errors.generic';
        if (_remainingAttempts > 0) _remainingAttempts--;
        _isLoading = false;
      });
    }
  }

  Future<void> _resend() async {
    setState(() {
      _secondsLeft = 300;
      _remainingAttempts = 5;
      _codeController.clear();
      _errorKey = null;
    });

    try {
      switch (widget.purpose) {
        case OtpPurpose.login:
          await _authRepository.requestPhoneOtp(widget.phone);
        case OtpPurpose.phoneVerification:
          await _authRepository.requestProfilePhoneVerification(widget.phone);
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _errorKey = e is DioException ? mapDioError(e).l10nKey : 'errors.generic';
        });
      }
    }
  }

  String get _timerText {
    final m = _secondsLeft ~/ 60;
    final s = _secondsLeft % 60;
    return '${m.toString().padLeft(2, '0')}:${s.toString().padLeft(2, '0')}';
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final textTheme = theme.textTheme;
    final colorScheme = theme.colorScheme;
    final expired = _secondsLeft == 0;

    return Scaffold(
      appBar: AppBar(
        leading: Semantics(
          label: 'semantic.back_button'.tr(),
          child: const BackButton(),
        ),
        title: Text(
          (widget.purpose == OtpPurpose.phoneVerification
                  ? 'auth.phone_verification.title'
                  : 'auth.verify_phone.title')
              .tr(),
        ),
        elevation: 0,
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 480),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 32),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  // Description
                  Text(
                    (widget.purpose == OtpPurpose.phoneVerification
                            ? 'auth.phone_verification.description'
                            : 'auth.verify_phone.description')
                        .tr(namedArgs: {'phone': widget.phone}),
                    style: textTheme.bodyLarge,
                    textAlign: TextAlign.center,
                  ),

                  const SizedBox(height: 32),

                  // 6-digit code field — auto-submits on full entry
                  Semantics(
                    label: 'auth.otp_field_label'.tr(),
                    child: TextFormField(
                      controller: _codeController,
                      autofocus: true,
                      keyboardType: TextInputType.number,
                      textInputAction: TextInputAction.done,
                      textAlign: TextAlign.center,
                      maxLength: 6,
                      style: textTheme.headlineMedium?.copyWith(
                        letterSpacing: 8,
                        fontWeight: FontWeight.w700,
                      ),
                      inputFormatters: [
                        FilteringTextInputFormatter.digitsOnly,
                      ],
                      decoration: InputDecoration(
                        counterText: '',
                        hintText: '------',
                        border: const OutlineInputBorder(),
                        enabled: !expired && _remainingAttempts > 0,
                      ),
                      onChanged: (value) {
                        if (value.length == 6) _verify(value);
                      },
                    ),
                  ),

                  const SizedBox(height: 16),

                  // Timer + attempts
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text(
                        _timerText,
                        style: textTheme.bodyMedium?.copyWith(
                          color: expired
                              ? colorScheme.error
                              : colorScheme.onSurfaceVariant,
                          fontVariations: const [FontVariation('wght', 600)],
                        ),
                      ),
                      if (_remainingAttempts < 5)
                        Text(
                          'auth.otp_attempts_left'
                              .tr(namedArgs: {'n': '$_remainingAttempts'}),
                          style: TextStyle(color: colorScheme.error),
                        ),
                    ],
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

                  // Submit button (also available for users who don't auto-submit)
                  if (!expired && _remainingAttempts > 0)
                    Semantics(
                      button: true,
                      label: 'auth.verify_semantic'.tr(),
                      child: SizedBox(
                        height: 64,
                        child: FilledButton(
                          onPressed:
                              _isLoading ? null : () => _verify(_codeController.text),
                          child: _isLoading
                              ? const CircularProgressIndicator()
                              : Text('auth.verify_phone.submit'.tr()),
                        ),
                      ),
                    ),

                  const SizedBox(height: 16),

                  // Resend
                  TextButton(
                    onPressed: expired ? _resend : null,
                    child: Text('auth.verify_phone.resend'.tr()),
                  ),

                  if (_remainingAttempts == 0)
                    Padding(
                      padding: const EdgeInsets.only(top: 16),
                      child: Text(
                        'auth.otp_too_many_attempts'.tr(),
                        style: TextStyle(color: colorScheme.error),
                        textAlign: TextAlign.center,
                      ),
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
