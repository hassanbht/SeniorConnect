// Security hotfix (docs/security/hotfix-matching-location.md): people and
// requests arrive with a distance band and no coordinates/km.

import 'package:flutter_test/flutter_test.dart';
import 'package:senior_connect/core/format/distance_band.dart';
import 'package:senior_connect/features/discovery/data/discovery_repository.dart';

void main() {
  test('a volunteer result without coordinates or km parses', () {
    final result = ProximityResult.fromJson({
      'userId': '00000000-0000-0000-0000-000000000000',
      'displayName': '',
      'distanceKm': null,
      'distanceBand': 'KM_1_3',
      'postalCode': '6020',
      'gemeindeName': 'Innsbruck',
      'latitude': null,
      'longitude': null,
      'isFuzzed': true,
    });

    expect(result.latitude, isNull);
    expect(result.longitude, isNull);
    expect(result.distanceKm, isNull);
    expect(result.distanceBand, 'KM_1_3');
  });

  test('band codes map to their keys; unknown or missing reads "unknown"', () {
    // Without a loaded locale easy_localization returns the key itself.
    expect(distanceBandLabel('KM_UNDER_1'), 'common.distance_band.KM_UNDER_1');
    expect(distanceBandLabel('KM_OVER_20'), 'common.distance_band.KM_OVER_20');
    expect(distanceBandLabel(null), 'common.distance_unknown');
    expect(distanceBandLabel('4.3'), 'common.distance_unknown');
  });
}
