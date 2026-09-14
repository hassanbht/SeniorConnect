// lib/features/profile/data/geography_repository.dart
//
// P1-28: Austrian administrative geography reference + address geocoding.
// Wraps GET /reference/austria/* and POST /reference/geocode-address.

import '../../../core/network/api_client.dart';

class Bundesland {
  const Bundesland({required this.code, required this.name});

  factory Bundesland.fromJson(Map<String, dynamic> json) => Bundesland(
        code: json['code'] as String,
        name: json['name'] as String,
      );

  final String code;
  final String name;
}

class Bezirk {
  const Bezirk({required this.code, required this.name});

  factory Bezirk.fromJson(Map<String, dynamic> json) => Bezirk(
        code: json['code'] as String,
        name: json['name'] as String,
      );

  final String code;
  final String name;
}

/// A municipality (Gemeinde), as returned by GET /reference/austria/gemeinden
/// and GET /reference/austria/lookup (backend `GemeindeDto`).
class Gemeinde {
  const Gemeinde({
    required this.code,
    required this.name,
    required this.postalCode,
    required this.latitude,
    required this.longitude,
  });

  factory Gemeinde.fromJson(Map<String, dynamic> json) => Gemeinde(
        code: json['gemeindeCode'] as String,
        name: json['gemeindeName'] as String,
        postalCode: json['postalCode'] as String,
        latitude: (json['latitude'] as num).toDouble(),
        longitude: (json['longitude'] as num).toDouble(),
      );

  final String code;
  final String name;
  final String postalCode;
  final double latitude;
  final double longitude;
}

class GeocodeResult {
  const GeocodeResult({
    required this.addressLine,
    required this.postalCode,
    required this.city,
    required this.latitude,
    required this.longitude,
    required this.gemeindeCode,
    required this.gemeindeName,
    required this.bezirkCode,
    required this.bezirkName,
    required this.bundeslandCode,
    required this.bundeslandName,
    required this.confidence,
  });

  factory GeocodeResult.fromJson(Map<String, dynamic> json) => GeocodeResult(
        addressLine: json['addressLine'] as String,
        postalCode: json['postalCode'] as String,
        city: json['city'] as String,
        latitude: (json['latitude'] as num).toDouble(),
        longitude: (json['longitude'] as num).toDouble(),
        gemeindeCode: json['gemeindeCode'] as String,
        gemeindeName: json['gemeindeName'] as String,
        bezirkCode: json['bezirkCode'] as String,
        bezirkName: json['bezirkName'] as String,
        bundeslandCode: json['bundeslandCode'] as String,
        bundeslandName: json['bundeslandName'] as String,
        confidence: (json['confidence'] as num).toDouble(),
      );

  final String addressLine;
  final String postalCode;
  final String city;
  final double latitude;
  final double longitude;
  final String gemeindeCode;
  final String gemeindeName;
  final String bezirkCode;
  final String bezirkName;
  final String bundeslandCode;
  final String bundeslandName;
  final double confidence;
}

abstract class GeographyRepository {
  Future<List<Bundesland>> getBundeslaender();
  Future<List<Bezirk>> getBezirke(String bundeslandCode);
  Future<List<Gemeinde>> getGemeinden(String bezirkCode);
  Future<List<Gemeinde>> lookupByPlz(String plz);
  Future<GeocodeResult> geocodeAddress(String address, {bool persistToProfile = false});
}

class GeographyRepositoryImpl implements GeographyRepository {
  GeographyRepositoryImpl(this._apiClient);

  final ApiClient _apiClient;

  @override
  Future<List<Bundesland>> getBundeslaender() async {
    final response = await _apiClient.get<List<dynamic>>(
      '/api/v1/reference/austria/bundeslaender',
    );
    return response
        .map((e) => Bundesland.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  @override
  Future<List<Bezirk>> getBezirke(String bundeslandCode) async {
    final response = await _apiClient.get<List<dynamic>>(
      '/api/v1/reference/austria/bezirke',
      queryParameters: {'bundeslandCode': bundeslandCode},
    );
    return response.map((e) => Bezirk.fromJson(e as Map<String, dynamic>)).toList();
  }

  @override
  Future<List<Gemeinde>> getGemeinden(String bezirkCode) async {
    final response = await _apiClient.get<List<dynamic>>(
      '/api/v1/reference/austria/gemeinden',
      queryParameters: {'bezirkCode': bezirkCode},
    );
    return response.map((e) => Gemeinde.fromJson(e as Map<String, dynamic>)).toList();
  }

  @override
  Future<List<Gemeinde>> lookupByPlz(String plz) async {
    final response = await _apiClient.get<List<dynamic>>(
      '/api/v1/reference/austria/lookup',
      queryParameters: {'plz': plz},
    );
    return response.map((e) => Gemeinde.fromJson(e as Map<String, dynamic>)).toList();
  }

  @override
  Future<GeocodeResult> geocodeAddress(String address, {bool persistToProfile = false}) async {
    final response = await _apiClient.post<Map<String, dynamic>>(
      '/api/v1/reference/geocode-address',
      data: {
        'address': address,
        'persistToProfile': persistToProfile,
      },
    );
    return GeocodeResult.fromJson(response);
  }
}
