// test/pilot_hardening_screens_test.dart
//
// Widget test suite for Phase 7 Pilot Hardening:
// - PrivacySettingsScreen (P7-05, P7-06, P7-07)
// - NotificationPreferencesScreen (P7-03)
// - LocalReadCache (P7-04 / Gate 7.1)

import 'package:flutter_test/flutter_test.dart';
import 'package:senior_connect/core/cache/local_read_cache.dart';
import 'package:senior_connect/core/network/api_client.dart';
import 'package:senior_connect/features/profile/presentation/notification_preferences_screen.dart';
import 'package:senior_connect/features/profile/presentation/privacy_settings_screen.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'matrix.dart';

void main() {
  final testApiClient = ApiClient(baseUrl: 'http://localhost:5000');

  group('PrivacySettingsScreen (P7-05, P7-06, P7-07)', () {
    testAcrossMatrix(
      'renders privacy settings screen without overflow',
      (tester, c) async {
        await tester.pumpWidget(
          wrapForTest(
            PrivacySettingsScreen(apiClient: testApiClient),
            c,
          ),
        );
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 300));

        await expectNoOverflow(tester);
        expect(find.byType(PrivacySettingsScreen), findsOneWidget);
      },
      matrix: smokeMatrix(),
    );
  });

  group('NotificationPreferencesScreen (P7-03)', () {
    testAcrossMatrix(
      'renders notification preferences screen without overflow',
      (tester, c) async {
        await tester.pumpWidget(
          wrapForTest(
            NotificationPreferencesScreen(apiClient: testApiClient),
            c,
          ),
        );
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 300));

        await expectNoOverflow(tester);
        expect(find.byType(NotificationPreferencesScreen), findsOneWidget);
      },
      matrix: smokeMatrix(),
    );
  });

  group('LocalReadCache (P7-04 / Gate 7.1)', () {
    setUp(() {
      SharedPreferences.setMockInitialValues({});
    });

    test('saves and reads cached offline data successfully', () async {
      const cacheKey = 'test_schedule';
      final scheduleData = [
        {'id': '1', 'title': 'Gartenhilfe mit Anna', 'time': '10:00'},
        {'id': '2', 'title': 'Spaziergang im Park', 'time': '14:30'},
      ];

      final saved = await LocalReadCache.save(cacheKey, scheduleData);
      expect(saved, isTrue);

      final readData = await LocalReadCache.read(cacheKey);
      expect(readData, isNotNull);
      expect(readData, isA<List<dynamic>>());

      final list = readData as List<dynamic>;
      expect(list.length, 2);
      expect((list[0] as Map<String, dynamic>)['title'], 'Gartenhilfe mit Anna');
      expect((list[1] as Map<String, dynamic>)['title'], 'Spaziergang im Park');
    });

    test('returns null for nonexistent cache key', () async {
      final data = await LocalReadCache.read('nonexistent_key');
      expect(data, isNull);
    });

    test('clears cache key cleanly', () async {
      await LocalReadCache.save('to_clear', {'status': 'ok'});
      expect(await LocalReadCache.read('to_clear'), isNotNull);

      final cleared = await LocalReadCache.clear('to_clear');
      expect(cleared, isTrue);
      expect(await LocalReadCache.read('to_clear'), isNull);
    });
  });
}
