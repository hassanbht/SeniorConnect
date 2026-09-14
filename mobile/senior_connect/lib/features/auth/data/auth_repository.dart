import '../../../../core/network/api_client.dart';

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
  Future<void> idAustriaSignIn({
    required String code,
    String? state,
  });
  Future<void> requestProfilePhoneVerification(String phone);
  Future<void> verifyProfilePhone(String phone, String code);
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
      data: {
        'phone': phoneNumber,
        'code': code,
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
      data: {
        'email': email,
        'password': password,
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
  Future<void> idAustriaSignIn({
    required String code,
    String? state,
  }) async {
    final response = await _apiClient.post<Map<String, dynamic>>(
      '/api/v1/auth/id-austria',
      data: {
        'code': code,
        if (state != null) 'state': state,
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
      data: {
        'phone': phone,
        'code': code,
      },
    );
  }
}
