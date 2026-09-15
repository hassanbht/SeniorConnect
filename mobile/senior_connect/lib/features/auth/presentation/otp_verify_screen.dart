// lib/features/auth/presentation/otp_verify_screen.dart
//
// P1-27: OTP code entry screen.
//
// BR-AUTH-04: 5-minute expiry, max 5 attempts (enforced server-side).
// BR-AUTH-03: No password — this is purely OTP code entry.
//
// UX goal: phone → code → in, under 30 seconds.
// The code field auto-submits when 6 digits are entered.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/network/api_client.dart';
import '../../../core/router/app_router.dart';
import '../application/otp_notifier.dart';

/// Which flow this OTP screen is completing.
enum OtpPurpose {
  /// Phone OTP sign-in / auto-registration (default).
  login,

  /// In-profile "Verify Phone Number" (P1-13b). On success, pops with `true`
  /// instead of navigating home.
  phoneVerification,

  /// Changing an already-verified phone number (P1-13, BR-AUTH-06). Success
  /// revokes every session server-side, so the client must clear local
  /// tokens and return to sign-in rather than navigating home.
  phoneChange,
}

class OtpVerifyScreen extends ConsumerStatefulWidget {
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
  ConsumerState<OtpVerifyScreen> createState() => _OtpVerifyScreenState();
}

class _OtpVerifyScreenState extends ConsumerState<OtpVerifyScreen> {
  final _codeController = TextEditingController();

  OtpParams get _params => OtpParams(
        apiClient: widget.apiClient,
        phone: widget.phone,
        purpose: widget.purpose,
      );

  @override
  void dispose() {
    _codeController.dispose();
    super.dispose();
  }

  Future<void> _handleVerify(String code) async {
    final notifier = ref.read(otpProvider(_params).notifier);
    final success = await notifier.verify(code);
    if (!mounted || !success) return;

    switch (widget.purpose) {
      case OtpPurpose.login:
        context.go(AppRoutes.home);
      case OtpPurpose.phoneVerification:
        context.pop(true);
      case OtpPurpose.phoneChange:
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('auth.phone_change.signed_out_notice'.tr())),
        );
        context.go(AppRoutes.phoneEntry);
    }
  }

  String _formatTimerText(int secondsLeft) {
    final m = secondsLeft ~/ 60;
    final s = secondsLeft % 60;
    return '${m.toString().padLeft(2, '0')}:${s.toString().padLeft(2, '0')}';
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final textTheme = theme.textTheme;
    final colorScheme = theme.colorScheme;

    final otpState = ref.watch(otpProvider(_params));
    final notifier = ref.read(otpProvider(_params).notifier);
    final expired = otpState.secondsLeft == 0;

    return Scaffold(
      appBar: AppBar(
        leading: Semantics(
          label: 'semantic.back_button'.tr(),
          child: const BackButton(),
        ),
        title: Text(
          switch (widget.purpose) {
            OtpPurpose.phoneVerification => 'auth.phone_verification.title'.tr(),
            OtpPurpose.phoneChange => 'auth.phone_change.title'.tr(),
            OtpPurpose.login => 'auth.verify_phone.title'.tr(),
          },
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
                    switch (widget.purpose) {
                      OtpPurpose.phoneVerification =>
                        'auth.phone_verification.description'.tr(namedArgs: {'phone': widget.phone}),
                      OtpPurpose.phoneChange =>
                        'auth.phone_change.description'.tr(namedArgs: {'phone': widget.phone}),
                      OtpPurpose.login =>
                        'auth.verify_phone.description'.tr(namedArgs: {'phone': widget.phone}),
                    },
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
                      textAlign: TextAlign.center,
                      style: textTheme.headlineMedium?.copyWith(
                        letterSpacing: 12,
                        fontWeight: FontWeight.bold,
                      ),
                      maxLength: 6,
                      inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                      decoration: InputDecoration(
                        hintText: '------',
                        counterText: '',
                        border: const OutlineInputBorder(),
                        errorText: otpState.errorKey != null ? otpState.errorKey!.tr() : null,
                      ),
                      onChanged: (value) {
                        if (value.length == 6) {
                          _handleVerify(value);
                        }
                      },
                    ),
                  ),

                  const SizedBox(height: 16),

                  // Attempts remaining indicator
                  if (otpState.remainingAttempts < 5)
                    Text(
                      'auth.attempts_remaining'.tr(namedArgs: {'count': '${otpState.remainingAttempts}'}),
                      style: textTheme.bodySmall?.copyWith(color: colorScheme.error),
                      textAlign: TextAlign.center,
                    ),

                  const SizedBox(height: 24),

                  // Submit button
                  Semantics(
                    button: true,
                    label: 'auth.verify_button_semantic'.tr(),
                    child: SizedBox(
                      height: 48,
                      child: FilledButton(
                        onPressed: otpState.isLoading || expired
                            ? null
                            : () => _handleVerify(_codeController.text),
                        child: otpState.isLoading
                            ? const SizedBox(
                                width: 20,
                                height: 20,
                                child: CircularProgressIndicator(strokeWidth: 2),
                              )
                            : Text('auth.verify_button'.tr()),
                      ),
                    ),
                  ),

                  const SizedBox(height: 24),

                  // Countdown timer and resend
                  Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      if (!expired) ...[
                        Icon(Icons.timer_outlined, size: 16, color: colorScheme.onSurfaceVariant),
                        const SizedBox(width: 4),
                        Text(
                          _formatTimerText(otpState.secondsLeft),
                          style: textTheme.bodyMedium?.copyWith(
                            color: colorScheme.onSurfaceVariant,
                            fontFeatures: const [FontFeature.tabularFigures()],
                          ),
                        ),
                        const SizedBox(width: 16),
                      ],
                      Semantics(
                        button: true,
                        label: 'auth.resend_code_semantic'.tr(),
                        child: TextButton(
                          onPressed: (expired || otpState.secondsLeft < 240) && !otpState.isResending
                              ? () {
                                  _codeController.clear();
                                  notifier.resend();
                                }
                              : null,
                          child: otpState.isResending
                              ? const SizedBox(
                                  width: 16,
                                  height: 16,
                                  child: CircularProgressIndicator(strokeWidth: 2),
                                )
                              : Text('auth.resend_code'.tr()),
                        ),
                      ),
                    ],
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
