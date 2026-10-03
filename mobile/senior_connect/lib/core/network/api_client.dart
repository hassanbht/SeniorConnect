// lib/core/network/api_client.dart
//
// P1-26: Dio-based API client with silent JWT refresh.
//
// Design constraints:
//  - Maps ProblemDetails `code` field, never the message text.
//  - On 401 → tries one silent refresh (shared by concurrent 401s) →
//    retries the original request once.
//  - Refresh rejected by the server → clears tokens and bumps
//    [ApiClient.sessionEnded] so the router redirects to login.
//  - Refresh failed for a transient reason (offline) → tokens are kept.
//  - Never reads/writes userId, trustLevel, role from local storage.

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../cache/local_read_cache.dart';
import '../cache/reference_data_repository.dart';
import '../errors/app_error.dart';

const _kAccessToken = 'auth.access_token';
const _kRefreshToken = 'auth.refresh_token';

class ApiClient {
  ApiClient({required String baseUrl, FlutterSecureStorage? storage, Dio? dio})
    : _storage = storage ?? const FlutterSecureStorage(),
      _dio =
          dio ??
          Dio(
            BaseOptions(
              baseUrl: baseUrl,
              connectTimeout: const Duration(seconds: 10),
              receiveTimeout: const Duration(seconds: 15),
              headers: {'Accept': 'application/json'},
            ),
          ) {
    _dio.interceptors.add(_AuthInterceptor(this));
  }

  final Dio _dio;
  final FlutterSecureStorage _storage;

  /// Incremented whenever the server ends the session (refresh rejected).
  /// The router listens to it to redirect to the login screen.
  final ValueNotifier<int> sessionEnded = ValueNotifier<int>(0);

  /// The API's origin (e.g. `https://api.example.com`) — used to resolve
  /// server-relative URLs such as an uploaded profile photo's `photoUrl`.
  String get baseUrl => _dio.options.baseUrl;

  // --- Token persistence ---------------------------------------------------

  Future<String?> readAccessToken() => _storage.read(key: _kAccessToken);
  Future<String?> readRefreshToken() => _storage.read(key: _kRefreshToken);

  Future<void> persistTokens({
    required String accessToken,
    required String refreshToken,
  }) async {
    await _storage.write(key: _kAccessToken, value: accessToken);
    await _storage.write(key: _kRefreshToken, value: refreshToken);
  }

  Future<void> clearTokens() async {
    await _storage.delete(key: _kAccessToken);
    await _storage.delete(key: _kRefreshToken);
    // Cached reads (appointments etc.) belong to the session that just ended.
    await LocalReadCache.clearAll();
    await ReferenceDataRepository.clearSessionTraces();
  }

  // --- HTTP helpers --------------------------------------------------------

  Future<T> get<T>(
    String path, {
    Map<String, dynamic>? queryParameters,
    T Function(dynamic)? fromJson,
  }) async {
    final response = await _dio.get<dynamic>(
      path,
      queryParameters: queryParameters,
    );
    return _decode(response, fromJson);
  }

  Future<T> post<T>(
    String path, {
    dynamic data,
    Map<String, dynamic>? headers,
    T Function(dynamic)? fromJson,
  }) async {
    final response = await _dio.post<dynamic>(
      path,
      data: data,
      options: headers != null ? Options(headers: headers) : null,
    );
    return _decode(response, fromJson);
  }

  Future<T> put<T>(
    String path, {
    dynamic data,
    T Function(dynamic)? fromJson,
  }) async {
    final response = await _dio.put<dynamic>(path, data: data);
    return _decode(response, fromJson);
  }

  Future<T> patch<T>(
    String path, {
    dynamic data,
    T Function(dynamic)? fromJson,
  }) async {
    final response = await _dio.patch<dynamic>(path, data: data);
    return _decode(response, fromJson);
  }

  Future<T> delete<T>(
    String path, {
    dynamic data,
    T Function(dynamic)? fromJson,
  }) async {
    final response = await _dio.delete<dynamic>(path, data: data);
    return _decode(response, fromJson);
  }

  T _decode<T>(Response<dynamic> response, T Function(dynamic)? fromJson) {
    if (fromJson != null) return fromJson(response.data);
    return response.data as T;
  }
}

// ---------------------------------------------------------------------------
// Auth interceptor — attaches Bearer token, retries once on 401
// ---------------------------------------------------------------------------

/// Endpoints under /api/v1/auth that are anonymous. Every other /auth route
/// (devices, change-phone) requires the Bearer token like any other call.
const _anonymousAuthPaths = {
  '/api/v1/auth/request-phone-code',
  '/api/v1/auth/verify-phone',
  '/api/v1/auth/request-email-link',
  '/api/v1/auth/verify-email',
  '/api/v1/auth/register',
  '/api/v1/auth/login',
  '/api/v1/auth/google',
  '/api/v1/auth/id-austria',
  '/api/v1/auth/staff-login',
  '/api/v1/auth/refresh',
  '/api/v1/auth/logout',
};

const _kRetriedFlag = 'auth.retried';

bool _isAnonymous(RequestOptions options) =>
    _anonymousAuthPaths.contains(options.uri.path);

enum _RefreshOutcome { refreshed, rejected, transientFailure }

class _AuthInterceptor extends Interceptor {
  _AuthInterceptor(this._client);

  final ApiClient _client;

  /// The in-flight refresh, shared by every request that hit a 401 while it
  /// runs — so N concurrent 401s cause exactly one refresh call.
  Future<_RefreshOutcome>? _refreshing;

  @override
  Future<void> onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) async {
    if (_isAnonymous(options)) return handler.next(options);

    final token = await _client.readAccessToken();
    if (token != null) {
      options.headers['Authorization'] = 'Bearer $token';
    }
    handler.next(options);
  }

  @override
  Future<void> onError(
    DioException err,
    ErrorInterceptorHandler handler,
  ) async {
    final options = err.requestOptions;
    if (err.response?.statusCode != 401 ||
        _isAnonymous(options) ||
        options.extra[_kRetriedFlag] == true) {
      return handler.next(err);
    }

    final outcome = await (_refreshing ??= _tryRefresh().whenComplete(
      () => _refreshing = null,
    ));

    switch (outcome) {
      case _RefreshOutcome.refreshed:
        try {
          options.extra[_kRetriedFlag] = true;
          final response = await _client._dio.fetch<dynamic>(options);
          handler.resolve(response);
        } on DioException catch (retryError) {
          handler.next(retryError);
        }
      case _RefreshOutcome.rejected:
        // The server refused the refresh token: the session is over.
        await _client.clearTokens();
        _client.sessionEnded.value++;
        handler.next(err);
      case _RefreshOutcome.transientFailure:
        // Offline or server hiccup: keep the tokens, the next call retries.
        handler.next(err);
    }
  }

  Future<_RefreshOutcome> _tryRefresh() async {
    final refreshToken = await _client.readRefreshToken();
    if (refreshToken == null) return _RefreshOutcome.rejected;

    try {
      final response = await _client._dio.post<Map<String, dynamic>>(
        '/api/v1/auth/refresh',
        data: {'refreshToken': refreshToken},
      );
      final data = response.data;
      final access = data?['accessToken'];
      final refresh = data?['refreshToken'];
      if (access is! String || refresh is! String) {
        return _RefreshOutcome.rejected;
      }

      await _client.persistTokens(accessToken: access, refreshToken: refresh);
      return _RefreshOutcome.refreshed;
    } on DioException catch (e) {
      final status = e.response?.statusCode;
      // 400/401/403/409 (reuse detected) = token is dead; anything else
      // (no connection, timeout, 5xx) is transient.
      if (status != null && status >= 400 && status < 500) {
        return _RefreshOutcome.rejected;
      }
      return _RefreshOutcome.transientFailure;
    }
  }
}

// ---------------------------------------------------------------------------
// Error mapping — maps ProblemDetails `code` to AppError
// ---------------------------------------------------------------------------

AppError mapDioError(DioException e) {
  final data = e.response?.data;
  final code = (data is Map) ? (data['code'] as String?) : null;
  final statusCode = e.response?.statusCode;

  if (statusCode == 429) return const AppError.rateLimited();
  if (statusCode == 401) return const AppError.unauthenticated();
  if (statusCode == 403) {
    final missing = (data is Map)
        ? List<String>.from(data['missing'] as List? ?? [])
        : <String>[];
    return AppError.forbidden(missing: missing);
  }
  if (statusCode == 409 && code == 'TOKEN_COMPROMISED') {
    return const AppError.tokenCompromised();
  }
  if (e.type == DioExceptionType.connectionTimeout ||
      e.type == DioExceptionType.receiveTimeout ||
      e.type == DioExceptionType.sendTimeout ||
      e.type == DioExceptionType.connectionError) {
    return const AppError.noInternet();
  }

  return AppError.unknown(code: code ?? 'UNKNOWN');
}
