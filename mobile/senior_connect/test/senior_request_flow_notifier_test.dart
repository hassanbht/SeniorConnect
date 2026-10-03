// test/senior_request_flow_notifier_test.dart
//
// P3-21 / P3-05: the senior create-request body and server emergency handling.

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:senior_connect/core/network/api_client.dart';
import 'package:senior_connect/features/help_requests/application/senior_request_flow_notifier.dart';

class _FakeApiClient extends ApiClient {
  _FakeApiClient({this.errorBody}) : super(baseUrl: 'http://localhost');

  final Map<String, dynamic>? errorBody;
  Map<String, dynamic>? lastBody;

  @override
  Future<T> post<T>(
    String path, {
    dynamic data,
    Map<String, dynamic>? headers,
    T Function(dynamic)? fromJson,
  }) async {
    lastBody = Map<String, dynamic>.from(data as Map);
    if (errorBody != null) {
      final options = RequestOptions(path: path);
      throw DioException(
        requestOptions: options,
        response: Response<dynamic>(
          requestOptions: options,
          statusCode: 400,
          data: errorBody,
        ),
        type: DioExceptionType.badResponse,
      );
    }
    return <String, dynamic>{'id': 'req-1'} as T;
  }
}

const _categories = [
  {'code': 'doctor', 'id': 'cat-doctor', 'isBlocked': false},
];

SeniorRequestFlowNotifier _readyNotifier(ApiClient api) {
  final notifier = SeniorRequestFlowNotifier(
    apiClient: api,
    initialCategories: _categories,
  );
  notifier.onCategorySelected(
    SeniorRequestFlowNotifier.categories.firstWhere((c) => c.id == 'doctor'),
  );
  notifier.onTimingSelected('tomorrow');
  return notifier;
}

void main() {
  test('sends scheduledEndUtc after start and an explicit locationType',
      () async {
    final api = _FakeApiClient();
    final notifier = _readyNotifier(api);

    await notifier.submitRequest('Begleitung zum Hausarzt');

    final body = api.lastBody!;
    final start = DateTime.parse(body['scheduledStartUtc'] as String);
    final end = DateTime.parse(body['scheduledEndUtc'] as String);
    expect(end.difference(start),
        const Duration(minutes: kDefaultRequestDurationMinutes));
    expect(body['durationMinutes'], kDefaultRequestDurationMinutes);
    expect(body['locationType'], HelpLocationType.institution);
    expect(body['categoryId'], 'cat-doctor');
    expect(notifier.state.currentStep, SeniorRequestStep.submittedSuccess);
  });

  test('EMERGENCY_DETECTED from the server exposes the emergency state',
      () async {
    final api = _FakeApiClient(errorBody: {'code': kEmergencyDetectedCode});
    final notifier = _readyNotifier(api);

    await notifier.submitRequest('Mein Mann ist gestürzt');

    expect(notifier.state.emergencyDetected, isTrue);
    expect(notifier.state.errorMessage, isNull);
    expect(notifier.state.isSubmitting, isFalse);
  });

  test('CATEGORY_BLOCKED from the server shows the referral step', () async {
    final api = _FakeApiClient(errorBody: {'code': kCategoryBlockedCode});
    final notifier = _readyNotifier(api);

    await notifier.submitRequest('Verband wechseln');

    expect(notifier.state.currentStep, SeniorRequestStep.blockedReferral);
    expect(notifier.state.errorMessage, isNull);
  });

  test('other server errors show an error, not the emergency state', () async {
    final api = _FakeApiClient(errorBody: {'code': 'VALIDATION_FAILED'});
    final notifier = _readyNotifier(api);

    await notifier.submitRequest('Einkauf');

    expect(notifier.state.emergencyDetected, isFalse);
    expect(notifier.state.errorMessage, isNotNull);
  });
}
