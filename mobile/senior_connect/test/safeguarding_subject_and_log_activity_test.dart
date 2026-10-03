// test/safeguarding_subject_and_log_activity_test.dart
//
// Safeguarding concerns name the counterpart's user id (never a request id,
// never the masked Guid.Empty). Log activity sends a server category id.

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:senior_connect/core/network/api_client.dart';
import 'package:senior_connect/features/help_requests/application/active_assignment_notifier.dart';
import 'package:senior_connect/features/help_requests/application/my_request_status_notifier.dart';
import 'package:senior_connect/features/organizations/application/log_activity_notifier.dart';
import 'package:shared_preferences/shared_preferences.dart';

const _emptyGuid = '00000000-0000-0000-0000-000000000000';

class _FakeApiClient extends ApiClient {
  _FakeApiClient(this.responses) : super(baseUrl: 'http://localhost');

  /// path -> response; a missing path throws like an unreachable server.
  final Map<String, dynamic> responses;
  final posts = <String, dynamic>{};

  @override
  Future<T> get<T>(
    String path, {
    Map<String, dynamic>? queryParameters,
    T Function(dynamic)? fromJson,
  }) async {
    if (!responses.containsKey(path)) {
      throw DioException(
        requestOptions: RequestOptions(path: path),
        type: DioExceptionType.connectionError,
      );
    }
    return responses[path] as T;
  }

  @override
  Future<T> post<T>(
    String path, {
    dynamic data,
    Map<String, dynamic>? headers,
    T Function(dynamic)? fromJson,
  }) async {
    posts[path] = data;
    return null as T;
  }
}

Future<void> _settle() => Future<void>.delayed(Duration.zero);

void main() {
  setUp(() => SharedPreferences.setMockInitialValues({}));

  group('safeguarding subject', () {
    test('partyUserId rejects missing and masked ids', () {
      expect(partyUserId(null), isNull);
      expect(partyUserId(''), isNull);
      expect(partyUserId(_emptyGuid), isNull);
      expect(partyUserId('senior-1'), 'senior-1');
    });

    test('volunteer view uses the senior id, not the assignment id', () async {
      final notifier = ActiveAssignmentNotifier(
        apiClient: _FakeApiClient({
          '/api/v1/help-requests/a1': {'id': 'a1', 'seniorUserId': 'senior-1'},
          '/api/v1/activities/categories': <dynamic>[],
        }),
        assignmentId: 'a1',
      );
      await _settle();

      expect(notifier.state.details?.seniorUserId, 'senior-1');
    });

    test(
      'senior view uses the assigned volunteer id; none -> hidden',
      () async {
        MyRequestStatusNotifier build(Object? volunteerId) =>
            MyRequestStatusNotifier(
              apiClient: _FakeApiClient({
                '/api/v1/help-requests/r1': {
                  'id': 'r1',
                  'assignedVolunteerUserId': volunteerId,
                },
                '/api/v1/activities/categories': <dynamic>[],
              }),
              requestId: 'r1',
            );

        final assigned = build('vol-1');
        final open = build(null);
        await _settle();

        expect(assigned.state.details?.assignedVolunteerUserId, 'vol-1');
        expect(open.state.details, isNotNull);
        expect(open.state.details?.assignedVolunteerUserId, isNull);
      },
    );
  });

  group('log activity category', () {
    const categories = [
      {'id': 'cat-shop', 'nameKey': 'help.category.shopping'},
      {'id': 'cat-nursing', 'nameKey': 'x', 'isBlocked': true},
    ];

    test('loads server categories, skips blocked, requires a pick', () async {
      final api = _FakeApiClient({'/api/v1/activities/categories': categories});
      final notifier = LogActivityNotifier(apiClient: api);
      await _settle();
      await _settle();

      expect(notifier.state.categories.map((c) => c.id), ['cat-shop']);
      expect(await notifier.submit(notes: ''), isFalse);
      expect(
        notifier.state.errorMessage,
        'help.log_activity.category_required',
      );
      expect(api.posts, isEmpty);

      notifier.setCategory('cat-shop');
      expect(await notifier.submit(notes: ''), isTrue);
      expect(
        (api.posts['/api/v1/activities'] as Map)['categoryId'],
        'cat-shop',
      );
    });

    test('category load failure exposes an error state', () async {
      final notifier = LogActivityNotifier(apiClient: _FakeApiClient({}));
      await _settle();

      expect(notifier.state.isLoadingCategories, isFalse);
      expect(notifier.state.categoriesError, isNotNull);
    });
  });
}
