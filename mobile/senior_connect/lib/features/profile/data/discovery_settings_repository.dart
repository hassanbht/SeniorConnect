// lib/features/profile/data/discovery_settings_repository.dart
//
// Phase 4 (docs/security/verification-and-discovery.md): the caller's own
// "people can find my profile" switch and verification facts.
//   GET/PUT /api/v1/me/discoverability
//   GET     /api/v1/me/verification
//
// Both are read from the server every time; nothing is cached locally. The
// app never sends a verification level: it only displays what the server
// computed from its own records.

import '../../../core/network/api_client.dart';

class Discoverability {
  const Discoverability({required this.isDiscoverable, this.updatedAtUtc});

  factory Discoverability.fromJson(Map<String, dynamic> json) =>
      Discoverability(
        isDiscoverable: json['isDiscoverable'] as bool,
        updatedAtUtc: json['updatedAtUtc'] == null
            ? null
            : DateTime.parse(json['updatedAtUtc'] as String),
      );

  final bool isDiscoverable;
  final DateTime? updatedAtUtc;
}

/// One verified fact, as the server states it (who and when), never "safe".
class VerificationBadge {
  const VerificationBadge({
    required this.fact,
    this.verifiedAtUtc,
    this.organizationName,
  });

  factory VerificationBadge.fromJson(Map<String, dynamic> json) =>
      VerificationBadge(
        fact: json['fact'] as String,
        verifiedAtUtc: json['verifiedAtUtc'] == null
            ? null
            : DateTime.parse(json['verifiedAtUtc'] as String),
        organizationName: json['organizationName'] as String?,
      );

  /// EMAIL_VERIFIED, PHONE_VERIFIED, ADDRESS_VERIFIED or ORGANIZATION_VERIFIED.
  final String fact;
  final DateTime? verifiedAtUtc;
  final String? organizationName;
}

class VerificationStatus {
  const VerificationStatus({
    required this.level,
    required this.badges,
    this.nextStep,
  });

  factory VerificationStatus.fromJson(Map<String, dynamic> json) =>
      VerificationStatus(
        level: json['level'] as String,
        badges: (json['badges'] as List<dynamic>)
            .map((b) => VerificationBadge.fromJson(b as Map<String, dynamic>))
            .toList(),
        nextStep: json['nextStep'] as String?,
      );

  final String level;
  final List<VerificationBadge> badges;

  /// VERIFY_EMAIL, VERIFY_PHONE, VERIFY_ADDRESS or null when complete.
  final String? nextStep;

  bool has(String fact) => badges.any((b) => b.fact == fact);
}

class DiscoverySettingsRepository {
  DiscoverySettingsRepository(this._apiClient);

  final ApiClient _apiClient;

  static const _discoverabilityPath = '/api/v1/me/discoverability';
  static const _verificationPath = '/api/v1/me/verification';

  Future<Discoverability> getDiscoverability() async =>
      Discoverability.fromJson(
        await _apiClient.get<Map<String, dynamic>>(_discoverabilityPath),
      );

  Future<Discoverability> setDiscoverable(bool value) async =>
      Discoverability.fromJson(
        await _apiClient.put<Map<String, dynamic>>(
          _discoverabilityPath,
          data: {'isDiscoverable': value},
        ),
      );

  Future<VerificationStatus> getVerification() async =>
      VerificationStatus.fromJson(
        await _apiClient.get<Map<String, dynamic>>(_verificationPath),
      );
}
