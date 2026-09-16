// lib/features/auth/presentation/totp_enrollment_screen.dart
//
// P1-12: Staff TOTP authenticator enrollment. No QR library is bundled —
// the secret and otpauth:// URI are shown as selectable text for manual
// entry into an authenticator app (Google Authenticator, Authy, etc).

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../application/totp_enrollment_notifier.dart';

class TotpEnrollmentScreen extends ConsumerStatefulWidget {
  const TotpEnrollmentScreen({super.key, required this.apiClient});

  final ApiClient apiClient;

  @override
  ConsumerState<TotpEnrollmentScreen> createState() =>
      _TotpEnrollmentScreenState();
}

class _TotpEnrollmentScreenState extends ConsumerState<TotpEnrollmentScreen> {
  final _codeController = TextEditingController();

  @override
  void dispose() {
    _codeController.dispose();
    super.dispose();
  }

  Future<void> _confirm() async {
    final code = _codeController.text.trim();
    if (code.length != 6) return;
    await ref
        .read(totpEnrollmentProvider(widget.apiClient).notifier)
        .confirm(code);
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final textTheme = theme.textTheme;
    final colorScheme = theme.colorScheme;
    final totpState = ref.watch(totpEnrollmentProvider(widget.apiClient));

    return Scaffold(
      appBar: AppBar(title: Text('auth.staff.title'.tr())),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 480),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 32),
              child: totpState.isLoading
                  ? const Center(child: CircularProgressIndicator())
                  : totpState.confirmed
                      ? Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            Icon(
                              Icons.check_circle,
                              color: colorScheme.primary,
                              size: 64,
                            ),
                            const SizedBox(height: 16),
                            Text(
                              'auth.phone_verification.success'.tr(),
                              textAlign: TextAlign.center,
                            ),
                          ],
                        )
                      : totpState.enrollment == null
                          ? Text(
                              (totpState.errorKey ?? 'errors.generic').tr(),
                              style: TextStyle(color: colorScheme.error),
                              textAlign: TextAlign.center,
                            )
                          : Column(
                              crossAxisAlignment: CrossAxisAlignment.stretch,
                              children: [
                                Text(
                                  'auth.staff.totp_hint'.tr(),
                                  style: textTheme.bodyLarge,
                                ),
                                const SizedBox(height: 24),
                                Card(
                                  child: Padding(
                                    padding: const EdgeInsets.all(16),
                                    child: Column(
                                      crossAxisAlignment:
                                          CrossAxisAlignment.start,
                                      children: [
                                        Text(
                                          'auth.staff.totp_label'.tr(),
                                          style: textTheme.labelMedium,
                                        ),
                                        const SizedBox(height: 8),
                                        SelectableText(
                                          totpState.enrollment!.secret,
                                          style: textTheme.titleMedium
                                              ?.copyWith(
                                            fontFamily: 'monospace',
                                          ),
                                        ),
                                      ],
                                    ),
                                  ),
                                ),
                                const SizedBox(height: 24),
                                TextFormField(
                                  controller: _codeController,
                                  keyboardType: TextInputType.number,
                                  textAlign: TextAlign.center,
                                  maxLength: 6,
                                  inputFormatters: [
                                    FilteringTextInputFormatter.digitsOnly,
                                  ],
                                  decoration: InputDecoration(
                                    counterText: '',
                                    labelText: 'auth.staff.totp_label'.tr(),
                                    border: const OutlineInputBorder(),
                                  ),
                                  onFieldSubmitted: (_) => _confirm(),
                                ),
                                if (totpState.errorKey != null) ...[
                                  const SizedBox(height: 12),
                                  Text(
                                    totpState.errorKey!.tr(),
                                    style: TextStyle(color: colorScheme.error),
                                    textAlign: TextAlign.center,
                                  ),
                                ],
                                const SizedBox(height: 16),
                                SizedBox(
                                  height: 64,
                                  child: FilledButton(
                                    onPressed: totpState.isConfirming
                                        ? null
                                        : _confirm,
                                    child: totpState.isConfirming
                                        ? const CircularProgressIndicator()
                                        : Text('auth.verify_phone.submit'.tr()),
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
