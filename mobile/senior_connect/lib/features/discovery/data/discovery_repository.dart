// lib/features/discovery/data/discovery_repository.dart
//
// P2-41: Local proximity discovery (BR-GEO-05/06) — nearby organizations,
// independent volunteers, and open help requests. Wraps GET /api/v1/discovery/*.
//
// Exact coordinates are always server-fuzzed (see `isFuzzed`); name/locality
// fields are frequently blank by design — the UI must degrade gracefully.

import 'package:dio/dio.dart';

import '../../../core/network/api_client.dart';

/// A privacy-fuzzed nearby person or organization (`ProximityResult` on the
/// backend). `userId` is `Guid.Empty` for help-request results, since a
/// request isn't a person.
class ProximityResult {
  const ProximityResult({
    required this.userId,
    required this.displayName,
    required this.distanceKm,
    required this.localityName,
    required this.postalCode,
    required this.gemeindeName,
    required this.bezirkName,
    required this.latitude,
    required this.longitude,
    required this.isFuzzed,
  });

  factory ProximityResult.fromJson(Map<String, dynamic> json) =>
      ProximityResult(
        userId: json['userId'] as String? ?? '',
        displayName: json['displayName'] as String? ?? '',
        distanceKm: (json['distanceKm'] as num).toDouble(),
        localityName: json['localityName'] as String? ?? '',
        postalCode: json['postalCode'] as String? ?? '',
        gemeindeName: json['gemeindeName'] as String? ?? '',
        bezirkName: json['bezirkName'] as String? ?? '',
        latitude: (json['latitude'] as num).toDouble(),
        longitude: (json['longitude'] as num).toDouble(),
        isFuzzed: json['isFuzzed'] as bool? ?? true,
      );

  final String userId;
  final String displayName;
  final double distanceKm;
  final String localityName;
  final String postalCode;
  final String gemeindeName;
  final String bezirkName;
  final double latitude;
  final double longitude;
  final bool isFuzzed;

  /// Best-available label for this result — locality, else Gemeinde, else
  /// Bezirk, else nothing. Never assume any single field is populated.
  String get bestAvailableLocation {
    for (final candidate in [localityName, gemeindeName, bezirkName]) {
      if (candidate.isNotEmpty) return candidate;
    }
    return '';
  }
}

/// A nearby community event (`CommunityEventDiscoveryResult` on the
/// backend) — P5-06. Distance-only projection, no address.
class CommunityEventDiscoveryResult {
  const CommunityEventDiscoveryResult({
    required this.eventId,
    required this.title,
    required this.category,
    required this.startsAtUtc,
    required this.distanceKm,
  });

  factory CommunityEventDiscoveryResult.fromJson(Map<String, dynamic> json) =>
      CommunityEventDiscoveryResult(
        eventId: json['eventId'] as String,
        title: json['title'] as String? ?? '',
        category: json['category'] as String? ?? 'general',
        startsAtUtc: DateTime.parse(json['startsAtUtc'] as String),
        distanceKm: (json['distanceKm'] as num).toDouble(),
      );

  final String eventId;
  final String title;
  final String category;
  final DateTime startsAtUtc;
  final double distanceKm;
}

/// A municipality near a point (`NearbyTownDto` on the backend).
class NearbyTown {
  const NearbyTown({
    required this.gemeindeCode,
    required this.gemeindeName,
    required this.bezirkName,
    required this.bundeslandName,
    required this.distanceKm,
    required this.latitude,
    required this.longitude,
  });

  factory NearbyTown.fromJson(Map<String, dynamic> json) => NearbyTown(
        gemeindeCode: json['gemeindeCode'] as String,
        gemeindeName: json['gemeindeName'] as String,
        bezirkName: json['bezirkName'] as String,
        bundeslandName: json['bundeslandName'] as String,
        distanceKm: (json['distanceKm'] as num).toDouble(),
        latitude: (json['latitude'] as num).toDouble(),
        longitude: (json['longitude'] as num).toDouble(),
      );

  final String gemeindeCode;
  final String gemeindeName;
  final String bezirkName;
  final String bundeslandName;
  final double distanceKm;
  final double latitude;
  final double longitude;
}

/// The current user's own persisted coordinates, read back from whichever
/// profile (support or volunteer) already has them geocoded (P1-28's
/// "Lookup Address on Map"). Neither profile existing, or neither having a
/// location yet, is a normal state — not an error.
class MyLocation {
  const MyLocation({required this.latitude, required this.longitude});

  final double latitude;
  final double longitude;
}

abstract class DiscoveryRepository {
  /// Null means the caller has no persisted location yet — the UI must
  /// prompt them to set one in Profile, never query with (0, 0).
  Future<MyLocation?> getMyLocation();

  Future<List<ProximityResult>> getNearbyOrganizations(
      double latitude, double longitude, double radiusKm);
  Future<List<ProximityResult>> getNearbyVolunteers(
      double latitude, double longitude, double radiusKm);
  Future<List<ProximityResult>> getNearbyRequests(
      double latitude, double longitude, double radiusKm);
  Future<List<NearbyTown>> getNearestTowns(
    double latitude,
    double longitude, {
    double? maxDistanceKm,
    int? maxResults,
  });

  Future<List<CommunityEventDiscoveryResult>> getNearbyCommunityEvents(
    double latitude,
    double longitude,
    double radiusKm, {
    bool matchMyInterests,
  });
}

class DiscoveryRepositoryImpl implements DiscoveryRepository {
  DiscoveryRepositoryImpl(this._apiClient);

  final ApiClient _apiClient;

  @override
  Future<MyLocation?> getMyLocation() async {
    // Support profile first (seniors/citizens), then volunteer profile —
    // whichever the caller has, both already carry geocoded coordinates.
    for (final path in [
      '/api/v1/me/support-profile',
      '/api/v1/me/volunteer-profile',
    ]) {
      try {
        final response = await _apiClient.get<Map<String, dynamic>>(path);
        final lat = response['latitude'] as num?;
        final lng = response['longitude'] as num?;
        if (lat != null && lng != null) {
          return MyLocation(latitude: lat.toDouble(), longitude: lng.toDouble());
        }
      } on DioException catch (e) {
        if (e.response?.statusCode != 404) rethrow;
        // No profile of this kind for this user — try the next one.
      }
    }
    return null;
  }

  @override
  Future<List<ProximityResult>> getNearbyOrganizations(
    double latitude,
    double longitude,
    double radiusKm,
  ) =>
      _getNearby(
          '/api/v1/discovery/nearby-organizations', latitude, longitude, radiusKm);

  @override
  Future<List<ProximityResult>> getNearbyVolunteers(
    double latitude,
    double longitude,
    double radiusKm,
  ) =>
      _getNearby(
          '/api/v1/discovery/nearby-volunteers', latitude, longitude, radiusKm);

  @override
  Future<List<ProximityResult>> getNearbyRequests(
    double latitude,
    double longitude,
    double radiusKm,
  ) =>
      _getNearby(
          '/api/v1/discovery/nearby-requests', latitude, longitude, radiusKm);

  Future<List<ProximityResult>> _getNearby(
    String path,
    double latitude,
    double longitude,
    double radiusKm,
  ) async {
    final response = await _apiClient.get<List<dynamic>>(
      path,
      queryParameters: {
        'latitude': latitude,
        'longitude': longitude,
        'radiusKm': radiusKm,
      },
    );
    return response
        .map((e) => ProximityResult.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  @override
  Future<List<NearbyTown>> getNearestTowns(
    double latitude,
    double longitude, {
    double? maxDistanceKm,
    int? maxResults,
  }) async {
    final response = await _apiClient.get<List<dynamic>>(
      '/api/v1/discovery/nearest-towns',
      queryParameters: {
        'latitude': latitude,
        'longitude': longitude,
        'maxDistanceKm': ?maxDistanceKm,
        'maxResults': ?maxResults,
      },
    );
    return response
        .map((e) => NearbyTown.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  @override
  Future<List<CommunityEventDiscoveryResult>> getNearbyCommunityEvents(
    double latitude,
    double longitude,
    double radiusKm, {
    bool matchMyInterests = false,
  }) async {
    final response = await _apiClient.get<List<dynamic>>(
      '/api/v1/discovery/nearby-community-events',
      queryParameters: {
        'latitude': latitude,
        'longitude': longitude,
        'radiusKm': radiusKm,
        'matchMyInterests': matchMyInterests,
      },
    );
    return response
        .map((e) =>
            CommunityEventDiscoveryResult.fromJson(e as Map<String, dynamic>))
        .toList();
  }
}
