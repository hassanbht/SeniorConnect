// lib/core/format/distance_band.dart
//
// Security hotfix (docs/security/hotfix-matching-location.md): the server
// reports the distance to another person or request only as a band code,
// never in km. This maps the stable code to its translation; the code itself
// is never shown or branched on beyond this lookup.

import 'package:easy_localization/easy_localization.dart';

const _bandKeys = {
  'KM_UNDER_1': 'common.distance_band.KM_UNDER_1',
  'KM_1_3': 'common.distance_band.KM_1_3',
  'KM_3_5': 'common.distance_band.KM_3_5',
  'KM_5_10': 'common.distance_band.KM_5_10',
  'KM_10_20': 'common.distance_band.KM_10_20',
  'KM_OVER_20': 'common.distance_band.KM_OVER_20',
};

/// Localized label for a server distance band; null or an unknown code reads
/// "distance unknown" rather than inventing a number.
String distanceBandLabel(String? band) =>
    (_bandKeys[band] ?? 'common.distance_unknown').tr();
