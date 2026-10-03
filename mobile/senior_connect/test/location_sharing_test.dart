// Phase 3 (docs/security/location-consent-and-visibility.md): the person's
// own location-sharing setting, truthful failure states, and where exact
// coordinates may be read in the app. Tnn refers to the Phase 3 test list.

import 'dart:io';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:senior_connect/core/format/distance_band.dart';
import 'package:senior_connect/core/network/api_client.dart';
import 'package:senior_connect/features/discovery/data/discovery_repository.dart';
import 'package:senior_connect/features/profile/application/location_sharing_notifier.dart';
import 'package:senior_connect/features/profile/data/location_sharing_repository.dart';
import 'package:senior_connect/features/profile/presentation/location_sharing_screen.dart';
import 'package:senior_connect/shared/widgets/app_states.dart';

import 'matrix.dart';

const _path = '/api/v1/me/location-sharing';
const _version = '2026-09-location-v1';

Map<String, dynamic> _server(String level) => {
  'level': level,
  'consentVersion': level == 'NONE' ? null : _version,
  'grantedAtUtc': level == 'NONE' ? null : '2026-09-25T10:00:00Z',
  'currentConsentVersion': _version,
};

DioException _offline() => DioException(
  requestOptions: RequestOptions(path: _path),
  type: DioExceptionType.connectionError,
);

DioException _status(int code, [Map<String, dynamic>? body]) => DioException(
  requestOptions: RequestOptions(path: _path),
  type: DioExceptionType.badResponse,
  response: Response(
    requestOptions: RequestOptions(path: _path),
    statusCode: code,
    data: body,
  ),
);

/// The server: remembers the level, records every PUT, can fail on demand.
class _FakeApi extends ApiClient {
  _FakeApi(this.level) : super(baseUrl: 'http://localhost');

  String level;
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
    return _server(level) as T;
  }

  @override
  Future<T> put<T>(
    String path, {
    dynamic data,
    T Function(dynamic)? fromJson,
  }) async {
    puts.add(Map<String, dynamic>.from(data as Map));
    if (failPut != null) throw failPut!;
    level = data['level'] as String;
    return _server(level) as T;
  }
}

Future<LocationSharingNotifier> _loaded(_FakeApi api) async {
  final notifier = LocationSharingNotifier(LocationSharingRepositoryImpl(api));
  await Future<void>.delayed(Duration.zero);
  return notifier;
}

void main() {
  group('location sharing setting', () {
    test('T31 loads the level the server has', () async {
      final notifier = await _loaded(_FakeApi('APPROXIMATE'));

      expect(
        notifier.state.dataOrNull?.level,
        LocationSharingLevel.approximate,
      );
    });

    test('T31 the default (never chosen) is NONE with no grant date', () async {
      final notifier = await _loaded(_FakeApi('NONE'));

      expect(notifier.state.dataOrNull?.level, LocationSharingLevel.none);
      expect(notifier.state.dataOrNull?.grantedAtUtc, isNull);
    });

    test('T32 a change is sent to the server with the explanation version, '
        'and the state shows what the server confirmed', () async {
      final api = _FakeApi('NONE');
      final notifier = await _loaded(api);

      final ok = await notifier.change(LocationSharingLevel.precise);

      expect(ok, isTrue);
      expect(api.puts.single, {'level': 'PRECISE', 'consentVersion': _version});
      expect(notifier.state.dataOrNull?.level, LocationSharingLevel.precise);
    });

    test(
      'T33 revoking precise sharing reaches the server immediately',
      () async {
        final api = _FakeApi('PRECISE');
        final notifier = await _loaded(api);

        await notifier.change(LocationSharingLevel.none);

        expect(api.puts.single['level'], 'NONE');
        expect(api.level, 'NONE');
        expect(notifier.state.dataOrNull?.level, LocationSharingLevel.none);
      },
    );

    test(
      'a failed change keeps the last confirmed level, not the wished one',
      () async {
        final api = _FakeApi('NONE')..failPut = _status(500);
        final notifier = await _loaded(api);

        final ok = await notifier.change(LocationSharingLevel.precise);

        expect(ok, isFalse);
        expect(notifier.saveErrorKey, isNotNull);
        expect(notifier.state.dataOrNull?.level, LocationSharingLevel.none);
      },
    );

    test('an outdated explanation reloads instead of saving', () async {
      final api = _FakeApi('NONE')
        ..failPut = _status(409, {'code': 'CONSENT_VERSION_OUTDATED'});
      final notifier = await _loaded(api);

      await notifier.change(LocationSharingLevel.approximate);

      expect(notifier.saveErrorKey, 'location_sharing.version_outdated');
      expect(notifier.state.dataOrNull?.level, LocationSharingLevel.none);
    });

    test(
      'T34 T35 a network failure is an error state: no logout, no made-up level',
      () async {
        final api = _FakeApi('PRECISE')..failGet = _offline();
        final notifier = await _loaded(api);

        expect(notifier.state.isError, isTrue);
        expect(notifier.state.dataOrNull, isNull);
        expect(api.loggedOut, isFalse);
      },
    );

    test('an unknown level from the server is not guessed', () {
      expect(
        () => LocationSharing.fromJson({..._server('NONE'), 'level': 'ALL'}),
        throwsFormatException,
      );
    });

    testAcrossMatrix(
      'T35 the screen shows a retryable error, never a selected option',
      (tester, c) async {
        final api = _FakeApi('PRECISE')..failGet = _offline();

        await tester.pumpWidget(
          wrapForTest(LocationSharingScreen(apiClient: api), c),
        );
        await tester.pumpAndSettle();

        expect(find.byType(RadioListTile<LocationSharingLevel>), findsNothing);
        expect(find.byType(AppErrorView), findsOneWidget);
        expect(api.loggedOut, isFalse);
      },
    );

    testAcrossMatrix(
      'T31 T33 the screen shows the server level and all three choices',
      (tester, c) async {
        final api = _FakeApi('NONE');

        await tester.pumpWidget(
          wrapForTest(LocationSharingScreen(apiClient: api), c),
        );
        await tester.pumpAndSettle();

        expect(
          find.byType(RadioListTile<LocationSharingLevel>),
          findsNWidgets(3),
        );
        final group = tester.widget<RadioGroup<LocationSharingLevel>>(
          find.byType(RadioGroup<LocationSharingLevel>),
        );
        expect(group.groupValue, LocationSharingLevel.none);
      },
    );
  });

  group('discovery and generic DTOs', () {
    test('T36 a person without area consent shows only the distance band', () {
      final result = ProximityResult.fromJson({
        'userId': '00000000-0000-0000-0000-000000000000',
        'displayName': '',
        'distanceBand': 'KM_1_3',
        'postalCode': '',
        'gemeindeName': '',
        'latitude': null,
        'longitude': null,
      });

      expect(result.bestAvailableLocation, isEmpty);
      expect(
        distanceBandLabel(result.distanceBand),
        'common.distance_band.KM_1_3',
      );
      expect(result.latitude, isNull);
    });

    test('T37 exact coordinates are parsed only in reviewed places', () {
      // Every Dart file that reads "latitude" from JSON must be listed here
      // with the reason it is allowed. Generic user/request models never are.
      const reviewed = {
        'lib/features/profile/data/geography_repository.dart':
            'own geocoded address / public Gemeinde centroid',
        'lib/features/discovery/data/discovery_repository.dart':
            'organizations (public venues); null for people and requests',
        'lib/features/community/domain/community_group.dart':
            'server-side ~1 km grid cell',
      };
      final reads = RegExp(r'''\[\s*['"]latitude['"]\s*\]''');

      final offenders = Directory('lib')
          .listSync(recursive: true)
          .whereType<File>()
          .where((f) => f.path.endsWith('.dart'))
          .where((f) => reads.hasMatch(f.readAsStringSync()))
          .map((f) => f.path.replaceAll(r'\', '/'))
          .where((p) => !reviewed.containsKey(p))
          .toList();

      expect(offenders, isEmpty);
    });
  });
}
