import 'package:dio/dio.dart';
import '../../../../core/network/api_client.dart';

abstract class AuthRepository {
  Future<void> requestPhoneOtp(String phoneNumber);
  Future<void> verifyPhoneOtp(String phoneNumber, String code);
  Future<void> logout();
  Future<bool> isAuthenticated();
}

class AuthRepositoryImpl implements AuthRepository {
  AuthRepositoryImpl({required ApiClient apiClient}) : _apiClient = apiClient;

  final ApiClient _apiClient;

  @override
  Future<void> requestPhoneOtp(String phoneNumber) async {
    await _apiClient.post<void>(
      '/api/v1/auth/request-phone-code',
      data: {'phoneNumber': phoneNumber},
    );
  }

  @override
  Future<void> verifyPhoneOtp(String phoneNumber, String code) async {
    final response = await _apiClient.post<Map<String, dynamic>>(
      '/api/v1/auth/verify-phone-code',
      data: {
        'phoneNumber': phoneNumber,
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
}
