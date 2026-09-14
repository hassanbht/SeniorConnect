// lib/features/profile/data/profile_repository.dart
//
// P1-28: GET/PATCH /api/v1/me — current user's profile (name, locale, etc).

import 'package:dio/dio.dart';

import '../../../core/network/api_client.dart';

class UserSummary {
  const UserSummary({
    required this.id,
    required this.phone,
    required this.email,
    required this.displayName,
    required this.preferredLocale,
    required this.seniorModeDefault,
    required this.primaryAuthMethod,
    required this.status,
    this.photoUrl,
  });

  factory UserSummary.fromJson(Map<String, dynamic> json) => UserSummary(
        id: json['id'] as String,
        phone: json['phone'] as String?,
        email: json['email'] as String?,
        displayName: json['displayName'] as String,
        preferredLocale: json['preferredLocale'] as String,
        seniorModeDefault: json['seniorModeDefault'] as bool,
        primaryAuthMethod: json['primaryAuthMethod'] as String,
        status: json['status'] as String,
        photoUrl: json['photoUrl'] as String?,
      );

  final String id;
  final String? phone;
  final String? email;
  final String displayName;
  final String preferredLocale;
  final bool seniorModeDefault;
  final String primaryAuthMethod;
  final String status;
  final String? photoUrl;
}

abstract class ProfileRepository {
  Future<UserSummary> getCurrentUser();

  Future<UserSummary> updateProfile({
    required String displayName,
    required String preferredLocale,
    required bool seniorModeDefault,
    DateTime? dateOfBirth,
  });

  /// P1-28: uploads a new profile photo (JPEG/PNG/WebP, local-disk storage
  /// on the backend pilot). Returns the updated user with the new photoUrl.
  Future<UserSummary> uploadPhoto(String filePath);
}

class ProfileRepositoryImpl implements ProfileRepository {
  ProfileRepositoryImpl(this._apiClient);

  final ApiClient _apiClient;

  @override
  Future<UserSummary> getCurrentUser() async {
    final response = await _apiClient.get<Map<String, dynamic>>('/api/v1/me/');
    return UserSummary.fromJson(response);
  }

  @override
  Future<UserSummary> updateProfile({
    required String displayName,
    required String preferredLocale,
    required bool seniorModeDefault,
    DateTime? dateOfBirth,
  }) async {
    final response = await _apiClient.patch<Map<String, dynamic>>(
      '/api/v1/me/',
      data: {
        'displayName': displayName,
        'dateOfBirth': dateOfBirth == null
            ? null
            : '${dateOfBirth.year.toString().padLeft(4, '0')}-'
                '${dateOfBirth.month.toString().padLeft(2, '0')}-'
                '${dateOfBirth.day.toString().padLeft(2, '0')}',
        'preferredLocale': preferredLocale,
        'seniorModeDefault': seniorModeDefault,
      },
    );
    return UserSummary.fromJson(response);
  }

  @override
  Future<UserSummary> uploadPhoto(String filePath) async {
    final formData = FormData.fromMap({
      'file': await MultipartFile.fromFile(filePath),
    });
    final response = await _apiClient.post<Map<String, dynamic>>(
      '/api/v1/me/photo',
      data: formData,
    );
    return UserSummary.fromJson(response);
  }
}
