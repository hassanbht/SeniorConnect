// lib/core/network/api_client.dart
//
// P1-26: Dio-based API client with silent JWT refresh.
//
// Design constraints:
//  - Maps ProblemDetails `code` field, never the message text.
//  - On 401 → tries one silent refresh → retries original request.
//  - On second 401 (refresh also failed) → emits unauthenticated event.
//  - Never reads/writes userId, trustLevel, role from local storage.

import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../errors/app_error.dart';

const _kAccessToken = 'auth.access_token';
const _kRefreshToken = 'auth.refresh_token';

class ApiClient {
  ApiClient({
    required String baseUrl,
    FlutterSecureStorage? storage,
    Dio? dio,
  })  : _storage = storage ?? const FlutterSecureStorage(),
        _dio = dio ??
            Dio(BaseOptions(
              baseUrl: baseUrl,
              connectTimeout: const Duration(seconds: 10),
              receiveTimeout: const Duration(seconds: 15),
              headers: {'Accept': 'application/json'},
            )) {
    _dio.interceptors.add(_AuthInterceptor(this));
  }

  final Dio _dio;
  final FlutterSecureStorage _storage;

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
  }

  // --- HTTP helpers --------------------------------------------------------

  Future<T> get<T>(
    String path, {
    Map<String, dynamic>? queryParameters,
    T Function(dynamic)? fromJson,
  }) async {
    final response = await _dio.get<dynamic>(path,
        queryParameters: queryParameters);
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

class _AuthInterceptor extends Interceptor {
  _AuthInterceptor(this._client);

  final ApiClient _client;
  bool _isRefreshing = false;

  @override
  Future<void> onRequest(
      RequestOptions options, RequestInterceptorHandler handler) async {
    // Skip auth header for auth endpoints themselves
    if (options.path.contains('/auth/')) {
      return handler.next(options);
    }
    final token = await _client.readAccessToken();
    if (token != null) {
      options.headers['Authorization'] = 'Bearer $token';
    }
    handler.next(options);
  }

  @override
  Future<void> onError(
      DioException err, ErrorInterceptorHandler handler) async {
    if (err.response?.statusCode != 401 || _isRefreshing) {
      return handler.next(err);
    }

    _isRefreshing = true;
    try {
      final refreshed = await _tryRefresh();
      if (!refreshed) {
        await _client.clearTokens();
        return handler.next(err);
      }

      // Retry original request with new token
      final token = await _client.readAccessToken();
      final opts = err.requestOptions;
      opts.headers['Authorization'] = 'Bearer $token';
      final response = await _client._dio.fetch<dynamic>(opts);
      handler.resolve(response);
    } catch (_) {
      await _client.clearTokens();
      handler.next(err);
    } finally {
      _isRefreshing = false;
    }
  }

  Future<bool> _tryRefresh() async {
    final refreshToken = await _client.readRefreshToken();
    if (refreshToken == null) return false;

    try {
      final response = await _client._dio.post<Map<String, dynamic>>(
        '/api/v1/auth/refresh',
        data: {'refreshToken': refreshToken},
        options: Options(headers: {}), // no auth header for refresh
      );
      final data = response.data;
      if (data == null) return false;

      await _client.persistTokens(
        accessToken: data['accessToken'] as String,
        refreshToken: data['refreshToken'] as String,
      );
      return true;
    } on DioException {
      return false;
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
