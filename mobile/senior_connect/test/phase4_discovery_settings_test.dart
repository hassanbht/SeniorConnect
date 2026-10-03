// Phase 4 (docs/security/verification-and-discovery.md): the discoverability
// switch, verification facts as the server states them, the visit-address
// explanation, and no exact coordinates on disk. Tnn refers to the Phase 4
// test list.

import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:senior_connect/core/cache/local_read_cache.dart';
import 'package:senior_connect/core/network/api_client.dart';
import 'package:senior_connect/features/profile/application/discovery_settings_notifier.dart';
import 'package:senior_connect/features/profile/data/discovery_settings_repository.dart';
import 'package:senior_connect/features/profile/presentation/discovery_settings_screen.dart';
import 'package:senior_connect/features/profile/presentation/location_sharing_screen.dart';
import 'package:senior_connect/shared/widgets/app_states.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'matrix.dart';

const _discoverability = '/api/v1/me/discoverability';
const _verification = '/api/v1/me/verification';

Map<String, dynamic> _verificationJson() => {
  'level': 'EMAIL_PHONE',
  'badges': [
    {'fact': 'EMAIL_VERIFIED', 'verifiedAtUtc': '2026-09-01T10:00:00Z'},
    {'fact': 'PHONE_VERIFIED', 'verifiedAtUtc': '2026-09-02T10:00:00Z'},
    {
      'fact': 'ORGANIZATION_VERIFIED',
      'verifiedAtUtc': '2026-09-03T10:00:00Z',
      'organizationName': 'Testverein Musterstadt',
    },
  ],
  'nextStep': 'VERIFY_ADDRESS',
};

class _FakeApi extends ApiClient {
  _FakeApi() : super(baseUrl: 'http://localhost');

  bool discoverable = false;
  final puts = <Map<String, dynamic>>[];
  DioException? failGet;
  DioException? failPut;
  bool loggedOut = false;

  @override
  Future<void> clearTokens() async => loggedOut = true;

  @override
  Future<T> get<T>(
    String path, {
    Map<String, dynamic>? queryParameters,
    T Function(dynamic)? fromJson,
  }) async {
    if (failGet != null) throw failGet!;
    if (path == _verification) return _verificationJson() as T;
    return {'isDiscoverable': discoverable, 'updatedAtUtc': null} as T;
  }

  @override
  Future<T> put<T>(
    String path, {
    dynamic data,
    T Function(dynamic)? fromJson,
  }) async {
    puts.add(Map<String, dynamic>.from(data as Map));
    if (failPut != null) throw failPut!;
    discoverable = data['isDiscoverable'] as bool;
    return {
          'isDiscoverable': discoverable,
          'updatedAtUtc': '2026-09-25T10:00:00Z',
        }
        as T;
  }
}

DioException _offline(String path) => DioException(
  requestOptions: RequestOptions(path: path),
  type: DioExceptionType.connectionError,
);

Future<DiscoverySettingsNotifier> _loaded(_FakeApi api) async {
  final notifier = DiscoverySettingsNotifier(DiscoverySettingsRepository(api));
  await Future<void>.delayed(Duration.zero);
  return notifier;
}

void main() {
  group('discoverability', () {
    test('T09 shows what the server has: off by default', () async {
      final notifier = await _loaded(_FakeApi());

      expect(
        notifier.state.dataOrNull?.discoverability.isDiscoverable,
        isFalse,
      );
      expect(notifier.state.dataOrNull?.verification.level, 'EMAIL_PHONE');
    });

    test('T06 turning it on sends only the switch, never a level', () async {
      final api = _FakeApi();
      final notifier = await _loaded(api);

      final ok = await notifier.setDiscoverable(true);

      expect(ok, isTrue);
      expect(api.puts.single, {'isDiscoverable': true});
      expect(notifier.state.dataOrNull?.discoverability.isDiscoverable, isTrue);
    });

    test('a failed change keeps the last confirmed value', () async {
      final api = _FakeApi()..failPut = _offline(_discoverability);
      final notifier = await _loaded(api);

      expect(await notifier.setDiscoverable(true), isFalse);
      expect(notifier.saveErrorKey, isNotNull);
      expect(
        notifier.state.dataOrNull?.discoverability.isDiscoverable,
        isFalse,
      );
    });

    test('a network failure is an error state, never a logout', () async {
      final api = _FakeApi()..failGet = _offline(_verification);
      final notifier = await _loaded(api);

      expect(notifier.state.isError, isTrue);
      expect(api.loggedOut, isFalse);
    });

    testAcrossMatrix(
      'T07 the screen shows the switch and each fact with who confirmed it',
      (tester, c) async {
        await tester.pumpWidget(
          wrapForTest(DiscoverySettingsScreen(apiClient: _FakeApi()), c),
        );
        await tester.pumpAndSettle();

        expect(
          find.byKey(const ValueKey('discoverable-switch')),
          findsOneWidget,
        );
        for (final fact in [
          'EMAIL_VERIFIED',
          'PHONE_VERIFIED',
          'ADDRESS_VERIFIED',
          'ORGANIZATION_VERIFIED',
        ]) {
          await _scrollTo(tester, find.text('discovery_settings.fact.$fact'));
        }
        expect(find.byType(AppErrorView), findsNothing);
      },
    );

    testAcrossMatrix('the screen shows a retryable error when offline', (
      tester,
      c,
    ) async {
      final api = _FakeApi()..failGet = _offline(_discoverability);
      await tester.pumpWidget(
        wrapForTest(DiscoverySettingsScreen(apiClient: api), c),
      );
      await tester.pumpAndSettle();

      expect(find.byType(AppErrorView), findsOneWidget);
      expect(find.byType(SwitchListTile), findsNothing);
    });
  });

  group('visit address and local storage', () {
    testAcrossMatrix(
      'the location screen explains the visit address as a separate rule',
      (tester, c) async {
        await tester.pumpWidget(
          wrapForTest(LocationSharingScreen(apiClient: _LocationApi()), c),
        );
        await tester.pumpAndSettle();

        await _scrollTo(
          tester,
          find.byKey(const ValueKey('visit-address-info')),
        );
      },
    );

    test(
      'T22 exact coordinates are never written to the offline cache',
      () async {
        SharedPreferences.setMockInitialValues({});

        await LocalReadCache.save('my_schedule', [
          {
            'eventId': 'e1',
            'locationAddress': 'Hauptplatz 1',
            'latitude': 47.26917,
            'longitude': 11.40433,
            'venue': {'lat': 47.1, 'lng': 11.2, 'name': 'Saal'},
          },
        ]);

        final prefs = await SharedPreferences.getInstance();
        final stored = prefs.getString('senior_connect_cache_my_schedule')!;
        expect(stored, isNot(contains('47.26917')));
        expect(stored, isNot(contains('11.40433')));
        expect(stored, isNot(contains('47.1')));
        final decoded = jsonDecode(stored) as List<dynamic>;
        expect((decoded.single as Map)['locationAddress'], 'Hauptplatz 1');
        expect(((decoded.single as Map)['venue'] as Map)['name'], 'Saal');
      },
    );
  });
}

/// Large text scales push rows below the fold of the lazy ListView.
Future<void> _scrollTo(WidgetTester tester, Finder finder) async {
  await tester.scrollUntilVisible(
    finder,
    200,
    scrollable: find.byType(Scrollable).first,
  );
  expect(finder, findsOneWidget);
}

/// Minimal server for the location-sharing screen.
class _LocationApi extends ApiClient {
  _LocationApi() : super(baseUrl: 'http://localhost');

  @override
  Future<T> get<T>(
    String path, {
    Map<String, dynamic>? queryParameters,
    T Function(dynamic)? fromJson,
  }) async =>
      {
            'level': 'NONE',
            'consentVersion': null,
            'grantedAtUtc': null,
            'currentConsentVersion': '2026-09-location-v1',
          }
          as T;
}
