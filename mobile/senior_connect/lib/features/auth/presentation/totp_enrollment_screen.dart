// lib/features/auth/presentation/totp_enrollment_screen.dart
//
// P1-12: Staff TOTP authenticator enrollment. No QR library is bundled —
// the secret and otpauth:// URI are shown as selectable text for manual
// entry into an authenticator app (Google Authenticator, Authy, etc).

import 'package:dio/dio.dart';
import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../../core/network/api_client.dart';
import '../data/auth_repository.dart';

class TotpEnrollmentScreen extends StatefulWidget {
  const TotpEnrollmentScreen({super.key, required this.apiClient});

  final ApiClient apiClient;

  @override
  State<TotpEnrollmentScreen> createState() => _TotpEnrollmentScreenState();
}

class _TotpEnrollmentScreenState extends State<TotpEnrollmentScreen> {
  late final AuthRepository _authRepository = AuthRepositoryImpl(widget.apiClient);

  final _codeController = TextEditingController();
  TotpEnrollment? _enrollment;
  bool _isLoading = true;
  bool _isConfirming = false;
  bool _confirmed = false;
  String? _errorKey;

  @override
  void initState() {
    super.initState();
    _startEnrollment();
  }

  @override
  void dispose() {
    _codeController.dispose();
    super.dispose();
  }

  Future<void> _startEnrollment() async {
    setState(() {
      _isLoading = true;
      _errorKey = null;
    });
    try {
      final enrollment = await _authRepository.enrollTotp();
      if (mounted) setState(() => _enrollment = enrollment);
    } catch (_) {
      if (mounted) setState(() => _errorKey = 'errors.generic');
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  Future<void> _confirm() async {
    if (_codeController.text.trim().length != 6) return;
    setState(() {
      _isConfirming = true;
      _errorKey = null;
    });
    try {
      await _authRepository.confirmTotpEnrollment(_codeController.text.trim());
      if (mounted) setState(() => _confirmed = true);
    } on DioException catch (e) {
      setState(() => _errorKey = mapDioError(e).l10nKey);
    } catch (_) {
      setState(() => _errorKey = 'errors.generic');
    } finally {
      if (mounted) setState(() => _isConfirming = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final textTheme = theme.textTheme;
    final colorScheme = theme.colorScheme;

    return Scaffold(
      appBar: AppBar(title: Text('auth.staff.title'.tr())),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 480),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 32),
              child: _isLoading
                  ? const Center(child: CircularProgressIndicator())
                  : _confirmed
                      ? Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            Icon(Icons.check_circle, color: colorScheme.primary, size: 64),
                            const SizedBox(height: 16),
                            Text('auth.phone_verification.success'.tr(), textAlign: TextAlign.center),
                          ],
                        )
                      : _enrollment == null
                          ? Text(
                              (_errorKey ?? 'errors.generic').tr(),
                              style: TextStyle(color: colorScheme.error),
                              textAlign: TextAlign.center,
                            )
                          : Column(
                              crossAxisAlignment: CrossAxisAlignment.stretch,
                              children: [
                                Text('auth.staff.totp_hint'.tr(), style: textTheme.bodyLarge),
                                const SizedBox(height: 24),
                                Card(
                                  child: Padding(
                                    padding: const EdgeInsets.all(16),
                                    child: Column(
                                      crossAxisAlignment: CrossAxisAlignment.start,
                                      children: [
                                        Text('auth.staff.totp_label'.tr(), style: textTheme.labelMedium),
                                        const SizedBox(height: 8),
                                        SelectableText(
                                          _enrollment!.secret,
                                          style: textTheme.titleMedium?.copyWith(fontFamily: 'monospace'),
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
                                  inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                                  decoration: InputDecoration(
                                    counterText: '',
                                    labelText: 'auth.staff.totp_label'.tr(),
                                    border: const OutlineInputBorder(),
                                  ),
                                  onFieldSubmitted: (_) => _confirm(),
                                ),
                                if (_errorKey != null) ...[
                                  const SizedBox(height: 12),
                                  Text(
                                    _errorKey!.tr(),
                                    style: TextStyle(color: colorScheme.error),
                                    textAlign: TextAlign.center,
                                  ),
                                ],
                                const SizedBox(height: 16),
                                SizedBox(
                                  height: 64,
                                  child: FilledButton(
                                    onPressed: _isConfirming ? null : _confirm,
                                    child: _isConfirming
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
