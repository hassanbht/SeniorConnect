// P1-26: silent refresh behaviour of ApiClient's auth interceptor.

import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:senior_connect/core/network/api_client.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Answers every request from [handler] instead of the network.
class _FakeAdapter implements HttpClientAdapter {
  _FakeAdapter(this.handler);

  final Future<ResponseBody> Function(RequestOptions options) handler;
  final requests = <RequestOptions>[];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) {
    requests.add(options);
    return handler(options);
  }

  @override
  void close({bool force = false}) {}
}

ResponseBody _json(int status, Object body) => ResponseBody.fromString(
      jsonEncode(body),
      status,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );

void main() {
  late _FakeAdapter adapter;
  late ApiClient client;

  Future<void> setUpClient(
      Future<ResponseBody> Function(RequestOptions) handler) async {
    FlutterSecureStorage.setMockInitialValues({
      'auth.access_token': 'old-access',
      'auth.refresh_token': 'old-refresh',
    });
    SharedPreferences.setMockInitialValues({});
    adapter = _FakeAdapter(handler);
    final dio = Dio(BaseOptions(baseUrl: 'https://api.test'))
      ..httpClientAdapter = adapter;
    client = ApiClient(baseUrl: 'https://api.test', dio: dio);
  }

  bool isRefresh(RequestOptions o) => o.path == '/api/v1/auth/refresh';

  test('concurrent 401s share one refresh and every request is retried',
      () async {
    await setUpClient((o) async {
      if (isRefresh(o)) {
        await Future<void>.delayed(const Duration(milliseconds: 20));
        return _json(200, {'accessToken': 'new-access', 'refreshToken': 'r2'});
      }
      final auth = o.headers['Authorization'];
      return auth == 'Bearer new-access'
          ? _json(200, {'ok': true})
          : _json(401, {'code': 'UNAUTHENTICATED'});
    });

    final results = await Future.wait([
      client.get<dynamic>('/api/v1/me'),
      client.get<dynamic>('/api/v1/me/trust'),
      client.get<dynamic>('/api/v1/me/capabilities'),
    ]);

    expect(results, everyElement({'ok': true}));
    expect(adapter.requests.where(isRefresh).length, 1);
    expect(await client.readAccessToken(), 'new-access');
  });

  test('protected /auth routes (devices) still send the bearer token',
      () async {
    await setUpClient(
        (o) async => _json(200, {'auth': o.headers['Authorization']}));

    final body = await client.get<dynamic>('/api/v1/auth/devices');

    expect(body, {'auth': 'Bearer old-access'});
  });

  test('a network failure during refresh keeps the tokens', () async {
    await setUpClient((o) async {
      if (isRefresh(o)) {
        throw DioException.connectionError(
            requestOptions: o, reason: 'offline');
      }
      return _json(401, {'code': 'UNAUTHENTICATED'});
    });

    await expectLater(
        client.get<dynamic>('/api/v1/me'), throwsA(isA<DioException>()));

    expect(await client.readRefreshToken(), 'old-refresh');
    expect(client.sessionEnded.value, 0);
  });

  test('a rejected refresh clears the tokens and ends the session', () async {
    await setUpClient((o) async => isRefresh(o)
        ? _json(401, {'code': 'REFRESH_TOKEN_INVALID'})
        : _json(401, {'code': 'UNAUTHENTICATED'}));

    await expectLater(
        client.get<dynamic>('/api/v1/me'), throwsA(isA<DioException>()));

    expect(await client.readRefreshToken(), isNull);
    expect(client.sessionEnded.value, 1);
  });
}
