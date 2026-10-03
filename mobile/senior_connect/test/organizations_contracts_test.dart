// test/organizations_contracts_test.dart
//
// Bulk entry reports rejected rows; the annual report keeps unrecorded
// metrics null instead of showing a fabricated 0.

import 'package:flutter_test/flutter_test.dart';
import 'package:senior_connect/features/organizations/application/coordinator_bulk_entry_notifier.dart';
import 'package:senior_connect/features/organizations/data/annual_report_model.dart';

void main() {
  test('bulk entry: no failures means success', () {
    expect(bulkEntryFailureKey({'logged': [{}], 'failures': []}), isNull);
  });

  test('bulk entry: a rejected row maps to a localized reason', () {
    expect(
      bulkEntryFailureKey({
        'logged': [],
        'failures': [
          {'index': 0, 'code': 'VOLUNTEER_NOT_IN_ORGANIZATION'},
        ],
      }),
      'coordinator.bulk_failure.not_in_organization',
    );
    expect(
      bulkEntryFailureKey({
        'logged': [],
        'failures': [
          {'index': 0, 'code': 'SOMETHING_ELSE'},
        ],
      }),
      'coordinator.bulk_failure.generic',
    );
  });

  test('annual report: null metrics and name stay null', () {
    const metric = {
      'currentValue': 3,
      'previousYearValue': 1,
      'growth': 2,
      'formattedGrowth': '+2',
    };
    final report = AnnualStatisticsReport.fromJson({
      'organizationId': 'o1',
      'organizationName': null,
      'year': 2025,
      'totalVolunteers': metric,
      'volunteerPool': null,
      'networkPartners': null,
      'placements': metric,
      'insuredPersons': null,
      'eventsAndProjects': null,
    });

    expect(report.organizationName, isNull);
    expect(report.volunteerPool, isNull);
    expect(report.networkPartners, isNull);
    expect(report.insuredPersons, isNull);
    expect(report.eventsAndProjects, isNull);
    expect(report.totalVolunteers.currentValue, 3);
  });
}
