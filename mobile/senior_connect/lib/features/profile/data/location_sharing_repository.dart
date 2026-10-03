// lib/features/profile/data/location_sharing_repository.dart
//
// Phase 3 (docs/security/location-consent-and-visibility.md): the caller's
// own location-sharing consent. GET/PUT /api/v1/me/location-sharing.
//
// The server decides what anyone else sees; this only reads and changes the
// person's own choice. Nothing here is cached locally.

import '../../../core/network/api_client.dart';

enum LocationSharingLevel {
  none('NONE'),
  approximate('APPROXIMATE'),
  precise('PRECISE');

  const LocationSharingLevel(this.code);

  final String code;

  /// Unknown codes throw: a level is never guessed.
  static LocationSharingLevel fromCode(String code) => values.firstWhere(
    (l) => l.code == code,
    orElse: () => throw const FormatException('Unknown location sharing level'),
  );
}

class LocationSharing {
  const LocationSharing({
    required this.level,
    required this.currentConsentVersion,
    this.grantedAtUtc,
  });

  factory LocationSharing.fromJson(Map<String, dynamic> json) =>
      LocationSharing(
        level: LocationSharingLevel.fromCode(json['level'] as String),
        currentConsentVersion: json['currentConsentVersion'] as String,
        grantedAtUtc: json['grantedAtUtc'] == null
            ? null
            : DateTime.parse(json['grantedAtUtc'] as String),
      );

  final LocationSharingLevel level;

  /// Version of the explanation the app shows; echoed back on a change.
  final String currentConsentVersion;

  /// Null while the person never made a choice (the default, NONE).
  final DateTime? grantedAtUtc;
}

abstract class LocationSharingRepository {
  Future<LocationSharing> get();

  Future<LocationSharing> update(
    LocationSharingLevel level,
    String consentVersion,
  );
}

class LocationSharingRepositoryImpl implements LocationSharingRepository {
  LocationSharingRepositoryImpl(this._apiClient);

  final ApiClient _apiClient;

  static const _path = '/api/v1/me/location-sharing';

  @override
  Future<LocationSharing> get() async => LocationSharing.fromJson(
    await _apiClient.get<Map<String, dynamic>>(_path),
  );

  @override
  Future<LocationSharing> update(
    LocationSharingLevel level,
    String consentVersion,
  ) async => LocationSharing.fromJson(
    await _apiClient.put<Map<String, dynamic>>(
      _path,
      data: {'level': level.code, 'consentVersion': consentVersion},
    ),
  );
}
