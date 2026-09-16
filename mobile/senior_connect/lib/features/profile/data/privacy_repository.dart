// lib/features/profile/data/privacy_repository.dart
//
// P7-05, P7-06, P7-07: Privacy, GDPR data export and account deletion repository.

import '../../../core/network/api_client.dart';

class ConsentItem {
  const ConsentItem({
    required this.id,
    required this.consentType,
    required this.documentVersion,
    required this.granted,
    required this.grantedAtUtc,
    this.withdrawnAtUtc,
  });

  factory ConsentItem.fromJson(Map<String, dynamic> json) => ConsentItem(
        id: json['id'] as String,
        consentType: json['consentType'] as String? ?? json['type'] as String? ?? 'Terms',
        documentVersion: json['documentVersion'] as String? ?? json['version'] as String? ?? '1.0',
        granted: json['granted'] as bool? ?? json['isActive'] as bool? ?? false,
        grantedAtUtc: DateTime.parse(json['grantedAtUtc'] as String? ?? json['agreedAtUtc'] as String),
        withdrawnAtUtc: json['withdrawnAtUtc'] != null
            ? DateTime.parse(json['withdrawnAtUtc'] as String)
            : null,
      );

  final String id;
  final String consentType;
  final String documentVersion;
  final bool granted;
  final DateTime grantedAtUtc;
  final DateTime? withdrawnAtUtc;
}

class UserDataExportSummary {
  const UserDataExportSummary({
    required this.userId,
    required this.exportedAtUtc,
    required this.exportFormat,
    required this.rawJson,
  });

  final String userId;
  final DateTime exportedAtUtc;
  final String exportFormat;
  final String rawJson;
}

class DeletionRequestSummary {
  const DeletionRequestSummary({
    required this.id,
    required this.userId,
    required this.status,
    required this.requestedAtUtc,
    required this.scheduledTier2PurgeUtc,
  });

  factory DeletionRequestSummary.fromJson(Map<String, dynamic> json) =>
      DeletionRequestSummary(
        id: json['id'] as String,
        userId: json['userId'] as String,
        status: json['status'] as String? ?? 'PendingConfirmation',
        requestedAtUtc: DateTime.parse(json['requestedAtUtc'] as String),
        scheduledTier2PurgeUtc:
            DateTime.parse(json['scheduledTier2PurgeUtc'] as String),
      );

  final String id;
  final String userId;
  final String status;
  final DateTime requestedAtUtc;
  final DateTime scheduledTier2PurgeUtc;
}

abstract class PrivacyRepository {
  Future<List<ConsentItem>> getConsents();
  Future<void> withdrawConsent(String consentType);
  Future<UserDataExportSummary> exportUserData();
  Future<DeletionRequestSummary> requestAccountDeletion(String? reason);
  Future<void> confirmAccountDeletion(String confirmationToken);
}

class PrivacyRepositoryImpl implements PrivacyRepository {
  const PrivacyRepositoryImpl(this._client);

  final ApiClient _client;

  @override
  Future<List<ConsentItem>> getConsents() async {
    final list = await _client.get<List<dynamic>>(
      '/api/v1/privacy/consents',
      fromJson: (data) => data as List<dynamic>,
    );
    return list
        .map((e) => ConsentItem.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  @override
  Future<void> withdrawConsent(String consentType) async {
    await _client.post<dynamic>(
      '/api/v1/privacy/consents/$consentType/withdraw',
    );
  }

  @override
  Future<UserDataExportSummary> exportUserData() async {
    final res = await _client.get<Map<String, dynamic>>(
      '/api/v1/privacy/me/export',
      fromJson: (data) => data as Map<String, dynamic>,
    );
    return UserDataExportSummary(
      userId: res['userId'] as String? ?? '',
      exportedAtUtc: res['exportedAtUtc'] != null
          ? DateTime.parse(res['exportedAtUtc'] as String)
          : DateTime.now().toUtc(),
      exportFormat: res['exportFormat'] as String? ?? 'JSON',
      rawJson: res.toString(),
    );
  }

  @override
  Future<DeletionRequestSummary> requestAccountDeletion(String? reason) async {
    final res = await _client.post<Map<String, dynamic>>(
      '/api/v1/privacy/account-deletion:request',
      data: {'reason': reason ?? 'User requested deletion'},
      fromJson: (data) => data as Map<String, dynamic>,
    );
    return DeletionRequestSummary.fromJson(res);
  }

  @override
  Future<void> confirmAccountDeletion(String confirmationToken) async {
    await _client.post<dynamic>(
      '/api/v1/privacy/account-deletion:confirm',
      data: {'confirmationToken': confirmationToken},
    );
  }
}
