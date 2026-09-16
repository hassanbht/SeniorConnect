import '../../../../core/network/api_client.dart';

/// An active refresh-token session, as returned by GET /auth/devices.
class DeviceSession {
  const DeviceSession({
    required this.id,
    required this.deviceLabel,
    required this.isPersonalDevice,
    required this.issuedAtUtc,
    required this.expiresAtUtc,
    required this.isActive,
    required this.isCurrent,
  });

  factory DeviceSession.fromJson(Map<String, dynamic> json) => DeviceSession(
    id: json['id'] as String,
    deviceLabel: json['deviceLabel'] as String?,
    isPersonalDevice: json['isPersonalDevice'] as bool,
    issuedAtUtc: DateTime.parse(json['issuedAtUtc'] as String),
    expiresAtUtc: DateTime.parse(json['expiresAtUtc'] as String),
    isActive: json['isActive'] as bool,
    isCurrent: json['isCurrent'] as bool,
  );

  final String id;
  final String? deviceLabel;
  final bool isPersonalDevice;
  final DateTime issuedAtUtc;
  final DateTime expiresAtUtc;
  final bool isActive;
  final bool isCurrent;
}

abstract class AuthRepository {
  Future<void> requestPhoneOtp(String phoneNumber);
  Future<void> verifyPhoneOtp(String phoneNumber, String code);
  Future<void> logout();
  Future<bool> isAuthenticated();

  // ADR-021 additions
  Future<void> registerEmailPassword({
    required String email,
    required String password,
    required String confirmPassword,
  });
  Future<void> verifyEmailRegistration(String token);
  Future<void> emailPasswordLogin({
    required String email,
    required String password,
  });
  Future<void> googleSignIn(String idToken);
  Future<void> idAustriaSignIn({required String code, String? state});
  Future<void> requestProfilePhoneVerification(String phone);
  Future<void> verifyProfilePhone(String phone, String code);

  // P1-11: email magic-link sign-in (passwordless, for users with no phone/password).
  Future<void> requestEmailMagicLink(String email);
  Future<void> verifyEmailMagicLink(String email, String code);

  // P1-12: organization staff / platform admin login (password + optional TOTP).
  Future<void> staffLogin({
    required String email,
    required String password,
    String? totpCode,
  });

  // P1-10: device session list + per-device revoke.
  Future<List<DeviceSession>> getDevices();
  Future<void> revokeDevice(String deviceId);

  // P1-12: staff TOTP authenticator enrollment.
  Future<TotpEnrollment> enrollTotp();
  Future<void> confirmTotpEnrollment(String code);

  // P1-13: phone-number change (SIM-swap mitigation, BR-AUTH-06). Verifying
  // invalidates every session (including the current one) — the caller must
  // clear local tokens and force a fresh sign-in afterwards.
  Future<void> initiatePhoneChange(String newPhone);
  Future<void> verifyPhoneChange(String newPhone, String code);
}

/// Returned by POST /me/totp/enroll — `secret` for manual entry, `provisioningUri`
/// (an otpauth:// URI) for apps that can import it directly.
class TotpEnrollment {
  const TotpEnrollment({required this.secret, required this.provisioningUri});

  factory TotpEnrollment.fromJson(Map<String, dynamic> json) => TotpEnrollment(
    secret: json['secret'] as String,
    provisioningUri: json['provisioningUri'] as String,
  );

  final String secret;
  final String provisioningUri;
}

class AuthRepositoryImpl implements AuthRepository {
  AuthRepositoryImpl(this._apiClient);

  final ApiClient _apiClient;

  @override
  Future<void> requestPhoneOtp(String phoneNumber) async {
    await _apiClient.post<void>(
      '/api/v1/auth/request-phone-code',
      data: {'phone': phoneNumber},
    );
  }

  @override
  Future<void> verifyPhoneOtp(String phoneNumber, String code) async {
    final response = await _apiClient.post<Map<String, dynamic>>(
      '/api/v1/auth/verify-phone',
      data: {'phone': phoneNumber, 'code': code},
    );

    final accessToken = response['accessToken'] as String?;
    final refreshToken = response['refreshToken'] as String?;

    if (accessToken != null && refreshToken != null) {
      await _apiClient.persistTokens(
        accessToken: accessToken,
        refreshToken: refreshToken,
      );
    }
  }

  @override
  Future<void> logout() async {
    final refreshToken = await _apiClient.readRefreshToken();
    try {
      if (refreshToken != null) {
        await _apiClient.post<void>(
          '/api/v1/auth/logout',
          data: {'refreshToken': refreshToken},
        );
      }
    } catch (_) {
      // Ignore network errors on logout
    } finally {
      await _apiClient.clearTokens();
    }
  }

  @override
  Future<bool> isAuthenticated() async {
    final refreshToken = await _apiClient.readRefreshToken();
    return refreshToken != null && refreshToken.isNotEmpty;
  }

  @override
  Future<void> registerEmailPassword({
    required String email,
    required String password,
    required String confirmPassword,
  }) async {
    await _apiClient.post<void>(
      '/api/v1/auth/register',
      data: {
        'email': email,
        'password': password,
        'confirmPassword': confirmPassword,
      },
    );
  }

  @override
  Future<void> verifyEmailRegistration(String token) async {
    // Backend registers GET /auth/verify-email for the registration-token flow —
    // POST on the same path hits the unrelated email-magic-link endpoint.
    await _apiClient.get<void>(
      '/api/v1/auth/verify-email',
      queryParameters: {'token': token},
    );
  }

  @override
  Future<void> emailPasswordLogin({
    required String email,
    required String password,
  }) async {
    final response = await _apiClient.post<Map<String, dynamic>>(
      '/api/v1/auth/login',
      data: {'email': email, 'password': password},
    );

    final accessToken = response['accessToken'] as String?;
    final refreshToken = response['refreshToken'] as String?;

    if (accessToken != null && refreshToken != null) {
      await _apiClient.persistTokens(
        accessToken: accessToken,
        refreshToken: refreshToken,
      );
    }
  }

  @override
  Future<void> googleSignIn(String idToken) async {
    final response = await _apiClient.post<Map<String, dynamic>>(
      '/api/v1/auth/google',
      data: {'idToken': idToken},
    );

    final accessToken = response['accessToken'] as String?;
    final refreshToken = response['refreshToken'] as String?;

    if (accessToken != null && refreshToken != null) {
      await _apiClient.persistTokens(
        accessToken: accessToken,
        refreshToken: refreshToken,
      );
    }
  }

  @override
  Future<void> idAustriaSignIn({required String code, String? state}) async {
    final response = await _apiClient.post<Map<String, dynamic>>(
      '/api/v1/auth/id-austria',
      data: {'code': code, if (state != null) 'state': state},
    );

    final accessToken = response['accessToken'] as String?;
    final refreshToken = response['refreshToken'] as String?;

    if (accessToken != null && refreshToken != null) {
      await _apiClient.persistTokens(
        accessToken: accessToken,
        refreshToken: refreshToken,
      );
    }
  }

  @override
  Future<void> requestProfilePhoneVerification(String phone) async {
    await _apiClient.post<void>(
      '/api/v1/me/phone/request-verification',
      data: {'phone': phone},
    );
  }

  @override
  Future<void> verifyProfilePhone(String phone, String code) async {
    await _apiClient.post<void>(
      '/api/v1/me/phone/verify',
      data: {'phone': phone, 'code': code},
    );
  }

  @override
  Future<void> requestEmailMagicLink(String email) async {
    await _apiClient.post<void>(
      '/api/v1/auth/request-email-link',
      data: {'email': email},
    );
  }

  @override
  Future<void> verifyEmailMagicLink(String email, String code) async {
    final response = await _apiClient.post<Map<String, dynamic>>(
      '/api/v1/auth/verify-email',
      data: {'email': email, 'code': code},
    );

    final accessToken = response['accessToken'] as String?;
    final refreshToken = response['refreshToken'] as String?;

    if (accessToken != null && refreshToken != null) {
      await _apiClient.persistTokens(
        accessToken: accessToken,
        refreshToken: refreshToken,
      );
    }
  }

  @override
  Future<void> staffLogin({
    required String email,
    required String password,
    String? totpCode,
  }) async {
    final response = await _apiClient.post<Map<String, dynamic>>(
      '/api/v1/auth/staff-login',
      data: {
        'email': email,
        'password': password,
        if (totpCode != null) 'totpCode': totpCode,
      },
    );

    final accessToken = response['accessToken'] as String?;
    final refreshToken = response['refreshToken'] as String?;

    if (accessToken != null && refreshToken != null) {
      await _apiClient.persistTokens(
        accessToken: accessToken,
        refreshToken: refreshToken,
      );
    }
  }

  @override
  Future<List<DeviceSession>> getDevices() async {
    final response = await _apiClient.get<List<dynamic>>(
      '/api/v1/auth/devices',
    );
    return response
        .map((e) => DeviceSession.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  @override
  Future<void> revokeDevice(String deviceId) async {
    await _apiClient.post<void>('/api/v1/auth/devices/$deviceId:revoke');
  }

  @override
  Future<TotpEnrollment> enrollTotp() async {
    final response = await _apiClient.post<Map<String, dynamic>>(
      '/api/v1/me/totp/enroll',
    );
    return TotpEnrollment.fromJson(response);
  }

  @override
  Future<void> confirmTotpEnrollment(String code) async {
    await _apiClient.post<void>(
      '/api/v1/me/totp/confirm',
      data: {'code': code},
    );
  }

  @override
  Future<void> initiatePhoneChange(String newPhone) async {
    await _apiClient.post<void>(
      '/api/v1/auth/change-phone:initiate',
      data: {'newPhone': newPhone},
    );
  }

  @override
  Future<void> verifyPhoneChange(String newPhone, String code) async {
    await _apiClient.post<void>(
      '/api/v1/auth/change-phone:verify',
      data: {'newPhone': newPhone, 'code': code},
    );
  }
}
