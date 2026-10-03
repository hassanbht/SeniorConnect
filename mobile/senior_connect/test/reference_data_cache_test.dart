// Phase 2 (docs/architecture/reference-data-cache.md): reference-data cache
// policy and the municipality (Gemeinde) API contract.

import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:senior_connect/core/cache/local_read_cache.dart';
import 'package:senior_connect/core/cache/reference_data_repository.dart';
import 'package:senior_connect/core/network/api_client.dart';
import 'package:senior_connect/features/profile/data/geography_repository.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Serves [responses] by path and counts calls; [failWith] simulates outages.
class _FakeApi extends ApiClient {
  _FakeApi(this.responses) : super(baseUrl: 'http://localhost');

  final Map<String, dynamic> responses;
  final calls = <String>[];
  DioException? failWith;

  @override
  Future<T> get<T>(
    String path, {
    Map<String, dynamic>? queryParameters,
    T Function(dynamic)? fromJson,
  }) async {
    calls.add(path);
    if (failWith != null) throw failWith!;
    return responses[path] as T;
  }

  @override
  Future<T> post<T>(
    String path, {
    dynamic data,
    Map<String, dynamic>? headers,
    T Function(dynamic)? fromJson,
  }) async {
    calls.add(path);
    return responses[path] as T;
  }
}

DioException _offline() => DioException(
  requestOptions: RequestOptions(path: '/'),
  type: DioExceptionType.connectionError,
);

const _categoriesPath = '/api/v1/activities/categories';
const _gemeindenPath = '/api/v1/reference/austria/gemeinden';
const _lookupPath = '/api/v1/reference/austria/lookup';

// Exactly what the backend's GemeindeDto serializes to (ASP.NET web defaults).
final _innsbruck = <String, dynamic>{
  'code': '70101',
  'name': 'Innsbruck',
  'postalCode': '6020',
  'latitude': 47.2692,
  'longitude': 11.4041,
};

List<dynamic> _categories(String nameKey) => [
  {
    'id': '11111111-1111-1111-1111-111111111111',
    'code': 'shopping',
    'nameKey': nameKey,
    'defaultSafetyLevel': 1,
    'isBlocked': false,
    'referralGroup': null,
    'isActive': true,
  },
];

Future<Map<String, Object>> _stored() async {
  final prefs = await SharedPreferences.getInstance();
  return {for (final k in prefs.getKeys()) k: prefs.get(k)!};
}

void main() {
  late DateTime now;
  late _FakeApi api;
  late ReferenceDataRepository repo;

  setUp(() {
    SharedPreferences.setMockInitialValues({});
    now = DateTime(2026, 9, 25, 12);
    api = _FakeApi({
      _categoriesPath: _categories('help.category.shopping'),
      _gemeindenPath: [_innsbruck],
      _lookupPath: [_innsbruck],
    });
    repo = ReferenceDataRepository(api, now: () => now);
  });

  group('cache policy', () {
    test('1. first load calls the API and stores the result', () async {
      final result = await repo.activityCategories();

      expect(api.calls, [_categoriesPath]);
      expect(result.single['nameKey'], 'help.category.shopping');
      expect(
        (await _stored()).keys,
        contains(
          ReferenceDataRepository.storageKey(
            ReferenceDataset.activityCategories,
          ),
        ),
      );
    });

    test('2. repeated loads within the TTL make no request', () async {
      await repo.activityCategories();
      for (var i = 0; i < 5; i++) {
        now = now.add(const Duration(hours: 1)); // screen re-opened
        await repo.activityCategories();
      }

      expect(api.calls, hasLength(1));
    });

    test('3. a stale entry is refreshed from the API', () async {
      await repo.activityCategories();
      api.responses[_categoriesPath] = _categories('help.category.new');
      now = now.add(const Duration(hours: 25));

      final result = await repo.activityCategories();

      expect(api.calls, hasLength(2));
      expect(result.single['nameKey'], 'help.category.new');
    });

    test(
      'explicit refresh bypasses a fresh entry; invalidate drops it',
      () async {
        await repo.activityCategories();
        await repo.activityCategories(refresh: true);
        expect(api.calls, hasLength(2));

        await ReferenceDataRepository.invalidate(
          ReferenceDataset.activityCategories,
        );
        expect(await _stored(), isEmpty);
      },
    );

    test(
      '4. network failure with a stale entry returns the cached value',
      () async {
        await repo.activityCategories();
        now = now.add(const Duration(days: 3));
        api.failWith = _offline();

        final result = await repo.activityCategories();

        expect(result.single['nameKey'], 'help.category.shopping');
      },
    );

    test('a non-temporary error (4xx) is never masked by stale data', () async {
      await repo.activityCategories();
      now = now.add(const Duration(days: 3));
      api.failWith = DioException(
        requestOptions: RequestOptions(path: _categoriesPath),
        type: DioExceptionType.badResponse,
        response: Response(
          requestOptions: RequestOptions(path: _categoriesPath),
          statusCode: 401,
        ),
      );

      await expectLater(
        repo.activityCategories(),
        throwsA(isA<DioException>()),
      );
    });

    test('5. network failure without a cache is a truthful error', () async {
      api.failWith = _offline();

      await expectLater(
        repo.activityCategories(),
        throwsA(isA<DioException>()),
      );
      expect(await _stored(), isEmpty); // nothing invented, nothing stored
    });

    test('6. a corrupt entry is discarded and reloaded', () async {
      final key = ReferenceDataRepository.storageKey(
        ReferenceDataset.activityCategories,
      );
      SharedPreferences.setMockInitialValues({key: '{not json'});

      final result = await repo.activityCategories();

      expect(api.calls, hasLength(1));
      expect(result, hasLength(1));
      final entry = jsonDecode((await _stored())[key]! as String) as Map;
      expect(entry['v'], kReferenceCacheVersion);
    });

    test('6b. an entry that no longer parses is deleted, not served', () async {
      final key = ReferenceDataRepository.storageKey(
        ReferenceDataset.gemeinden,
        '701',
      );
      SharedPreferences.setMockInitialValues({
        key: jsonEncode({
          'v': kReferenceCacheVersion,
          'storedAt': now.millisecondsSinceEpoch,
          'payload': [
            {'gemeindeCode': '70101', 'gemeindeName': 'Innsbruck'},
          ],
        }),
      });
      api.failWith = _offline();

      await expectLater(
        GeographyRepositoryImpl(api, reference: repo).getGemeinden('701'),
        throwsA(isA<DioException>()),
      );
      expect(await _stored(), isEmpty);
    });

    test('7. a cache version change invalidates old entries', () async {
      final oldEntry = jsonEncode({
        'v': 0,
        'storedAt': now.millisecondsSinceEpoch,
        'payload': _categories('help.category.old'),
      });
      SharedPreferences.setMockInitialValues({
        'reference.v0.activity.categories': oldEntry,
        ReferenceDataRepository.storageKey(ReferenceDataset.activityCategories):
            oldEntry,
      });

      final result = await repo.activityCategories();

      expect(api.calls, hasLength(1));
      expect(result.single['nameKey'], 'help.category.shopping');
      expect(
        (await _stored()).keys.where((k) => k.startsWith('reference.v0.')),
        isEmpty,
      );
    });
  });

  group('security boundaries', () {
    test('only the allowlisted public datasets exist', () {
      expect(ReferenceDataset.values.map((d) => d.key).toSet(), {
        'geo.bundeslaender',
        'geo.bezirke',
        'geo.gemeinden',
        'activity.categories',
      });
    });

    test('8. logout wipes session data; reference data holds nothing '
        'user-specific', () async {
      await repo.activityCategories();
      await GeographyRepositoryImpl(api, reference: repo).getGemeinden('701');
      await LocalReadCache.save('my_schedule', [
        {'userId': 'u1', 'title': 'Arzttermin'},
      ]);

      // What ApiClient.clearTokens does on logout.
      await LocalReadCache.clearAll();
      await ReferenceDataRepository.clearSessionTraces();

      final left = await _stored();
      // Only the unparameterised public list survives; the Bezirk the user
      // browsed does not.
      expect(left.keys, [
        ReferenceDataRepository.storageKey(ReferenceDataset.activityCategories),
      ]);
      expect(left.keys, everyElement(startsWith(kReferenceKeyPrefix)));
      for (final value in left.values) {
        for (final field in kForbiddenReferenceFields) {
          expect(value as String, isNot(contains('"$field"')));
        }
      }
    });

    test('9. precise coordinates are never stored', () async {
      api.responses['/api/v1/reference/geocode-address'] = {
        'addressLine': 'Maria-Theresien-Straße 1',
        'postalCode': '6020',
        'city': 'Innsbruck',
        'latitude': 47.268412,
        'longitude': 11.393125,
        'gemeindeCode': '70101',
        'gemeindeName': 'Innsbruck',
        'bezirkCode': '701',
        'bezirkName': 'Innsbruck-Stadt',
        'bundeslandCode': '7',
        'bundeslandName': 'Tirol',
        'confidence': 0.9,
      };

      await GeographyRepositoryImpl(
        api,
        reference: repo,
      ).geocodeAddress('Maria-Theresien-Straße 1, 6020 Innsbruck');
      expect(await _stored(), isEmpty); // geocoding bypasses the cache

      // Even an allowlisted endpoint returning an address is refused.
      api.responses[_categoriesPath] = [
        {'id': 'x', 'addressLine': 'Maria-Theresien-Straße 1'},
      ];
      await repo.activityCategories();
      expect(await _stored(), isEmpty);
    });

    test('10. help requests are never stored', () async {
      api.responses[_categoriesPath] = [
        {'id': 'x', 'helpRequestId': 'r1', 'assignedVolunteerId': 'v1'},
      ];

      await repo.activityCategories();

      expect(await _stored(), isEmpty);
    });

    test('11. safeguarding data is never stored', () async {
      api.responses[_categoriesPath] = [
        {'id': 'x', 'safeguardingCaseId': 's1'},
      ];

      await repo.activityCategories();

      expect(await _stored(), isEmpty);
    });

    test('unexpected parameters are not turned into cache keys', () async {
      await GeographyRepositoryImpl(
        api,
        reference: repo,
      ).getGemeinden('../../auth');

      expect(await _stored(), isEmpty);
    });

    test('a typed PLZ (usually the user\'s own) is never cached', () async {
      await GeographyRepositoryImpl(api, reference: repo).lookupByPlz('6020');

      expect(await _stored(), isEmpty);
    });
  });

  group('municipality contract (backend GemeindeDto)', () {
    test(
      '12. getGemeinden parses the real response and keeps the code',
      () async {
        final geo = GeographyRepositoryImpl(api, reference: repo);

        final result = await geo.getGemeinden('701');

        expect(result.single.code, '70101');
        expect(result.single.name, 'Innsbruck');
        expect(result.single.postalCode, '6020');
        expect(result.single.latitude, 47.2692);

        // Served again from the cache with the same code.
        final again = await geo.getGemeinden('701');
        expect(again.single.code, '70101');
        expect(api.calls, hasLength(1));
      },
    );

    test(
      '13. lookupByPlz parses the real response and keeps the code',
      () async {
        final result = await GeographyRepositoryImpl(
          api,
          reference: repo,
        ).lookupByPlz('6020');

        expect(result.single.code, '70101');
        expect(result.single.name, 'Innsbruck');
      },
    );

    test('a missing code fails loudly instead of defaulting', () {
      expect(
        () => Gemeinde.fromJson({..._innsbruck}..remove('code')),
        throwsA(isA<TypeError>()),
      );
      // The old, wrong field names are not silently accepted either.
      expect(
        () => Gemeinde.fromJson({
          'gemeindeCode': '70101',
          'gemeindeName': 'Innsbruck',
          'postalCode': '6020',
          'latitude': 47.2692,
          'longitude': 11.4041,
        }),
        throwsA(isA<TypeError>()),
      );
    });
  });
}
