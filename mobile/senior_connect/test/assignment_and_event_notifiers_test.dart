// test/assignment_and_event_notifiers_test.dart
//
// P3-23: completion sends the volunteer-confirmed duration.
// Community: unregister uses POST register:cancel and never fakes success.

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:senior_connect/core/network/api_client.dart';
import 'package:senior_connect/features/community/application/event_detail_notifier.dart';
import 'package:senior_connect/features/help_requests/application/active_assignment_notifier.dart';

class _FakeApiClient extends ApiClient {
  _FakeApiClient({this.getResponse, this.failPosts = false})
    : super(baseUrl: 'http://localhost');

  final Map<String, dynamic>? getResponse;
  final bool failPosts;
  final posts = <String, dynamic>{};

  DioException _error(String path) {
    final options = RequestOptions(path: path);
    return DioException(
      requestOptions: options,
      response: Response<dynamic>(requestOptions: options, statusCode: 500),
      type: DioExceptionType.badResponse,
    );
  }

  @override
  Future<T> get<T>(
    String path, {
    Map<String, dynamic>? queryParameters,
    T Function(dynamic)? fromJson,
  }) async {
    if (getResponse == null) throw _error(path);
    return getResponse as T;
  }

  @override
  Future<T> post<T>(
    String path, {
    dynamic data,
    Map<String, dynamic>? headers,
    T Function(dynamic)? fromJson,
  }) async {
    posts[path] = data;
    if (failPosts) throw _error(path);
    return null as T;
  }
}

void main() {
  test('complete sends the confirmed actual duration', () async {
    final api = _FakeApiClient(getResponse: {'durationMinutes': 90});
    final notifier = ActiveAssignmentNotifier(apiClient: api, assignmentId: 'a1');

    final ok = await notifier.handleComplete(75);

    expect(ok, isTrue);
    expect(api.posts['/api/v1/help-requests/a1:complete'],
        {'actualDurationMinutes': 75});
  });

  test('event load failure shows an error, not demo data', () async {
    final notifier = EventDetailNotifier(
      eventId: 'e1',
      apiClient: _FakeApiClient(),
    );
    await Future<void>.delayed(Duration.zero);

    expect(notifier.state.event, isNull);
    expect(notifier.state.error, isNotNull);
  });

  test('failed unregister keeps isRegistered and reports an error', () async {
    final api = _FakeApiClient(
      getResponse: {'id': 'e1', 'myRegistrationStatus': 'Going'},
      failPosts: true,
    );
    final notifier = EventDetailNotifier(eventId: 'e1', apiClient: api);
    await Future<void>.delayed(Duration.zero);
    expect(notifier.state.isRegistered, isTrue);

    await notifier.toggleRegistration();

    expect(api.posts.keys, contains('/api/v1/community/events/e1/register:cancel'));
    expect(notifier.state.isRegistered, isTrue);
    expect(notifier.state.actionError, isNotNull);
  });
}
