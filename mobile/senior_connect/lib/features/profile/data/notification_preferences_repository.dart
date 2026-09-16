// lib/features/profile/data/notification_preferences_repository.dart
//
// P7-03: Notification preferences repository for quiet hours and category opt-outs.

import '../../../core/network/api_client.dart';

class NotificationPreferencesModel {
  const NotificationPreferencesModel({
    required this.pushEnabled,
    required this.smsEnabled,
    required this.inAppEnabled,
    required this.quietHoursEnabled,
    required this.quietHoursStart,
    required this.quietHoursEnd,
    required this.helpRequestsCategoryEnabled,
    required this.communityCategoryEnabled,
    required this.familyWelfareCategoryEnabled,
    required this.systemAccountCategoryEnabled,
  });

  factory NotificationPreferencesModel.fromJson(Map<String, dynamic> json) {
    return NotificationPreferencesModel(
      pushEnabled: json['pushEnabled'] as bool? ?? true,
      smsEnabled: json['smsEnabled'] as bool? ?? true,
      inAppEnabled: json['inAppEnabled'] as bool? ?? true,
      quietHoursEnabled: json['quietHoursEnabled'] as bool? ?? true,
      quietHoursStart: json['quietHoursStart'] as String? ?? '20:00:00',
      quietHoursEnd: json['quietHoursEnd'] as String? ?? '08:00:00',
      helpRequestsCategoryEnabled:
          json['helpRequestsCategoryEnabled'] as bool? ?? true,
      communityCategoryEnabled:
          json['communityCategoryEnabled'] as bool? ?? true,
      familyWelfareCategoryEnabled:
          json['familyWelfareCategoryEnabled'] as bool? ?? true,
      systemAccountCategoryEnabled:
          json['systemAccountCategoryEnabled'] as bool? ?? true,
    );
  }

  final bool pushEnabled;
  final bool smsEnabled;
  final bool inAppEnabled;
  final bool quietHoursEnabled;
  final String quietHoursStart;
  final String quietHoursEnd;
  final bool helpRequestsCategoryEnabled;
  final bool communityCategoryEnabled;
  final bool familyWelfareCategoryEnabled;
  final bool systemAccountCategoryEnabled;

  Map<String, dynamic> toJson() => {
        'pushEnabled': pushEnabled,
        'smsEnabled': smsEnabled,
        'inAppEnabled': inAppEnabled,
        'quietHoursEnabled': quietHoursEnabled,
        'quietHoursStart': quietHoursStart,
        'quietHoursEnd': quietHoursEnd,
        'helpRequestsCategoryEnabled': helpRequestsCategoryEnabled,
        'communityCategoryEnabled': communityCategoryEnabled,
        'familyWelfareCategoryEnabled': familyWelfareCategoryEnabled,
        'systemAccountCategoryEnabled': systemAccountCategoryEnabled,
      };

  NotificationPreferencesModel copyWith({
    bool? pushEnabled,
    bool? smsEnabled,
    bool? inAppEnabled,
    bool? quietHoursEnabled,
    String? quietHoursStart,
    String? quietHoursEnd,
    bool? helpRequestsCategoryEnabled,
    bool? communityCategoryEnabled,
    bool? familyWelfareCategoryEnabled,
    bool? systemAccountCategoryEnabled,
  }) {
    return NotificationPreferencesModel(
      pushEnabled: pushEnabled ?? this.pushEnabled,
      smsEnabled: smsEnabled ?? this.smsEnabled,
      inAppEnabled: inAppEnabled ?? this.inAppEnabled,
      quietHoursEnabled: quietHoursEnabled ?? this.quietHoursEnabled,
      quietHoursStart: quietHoursStart ?? this.quietHoursStart,
      quietHoursEnd: quietHoursEnd ?? this.quietHoursEnd,
      helpRequestsCategoryEnabled:
          helpRequestsCategoryEnabled ?? this.helpRequestsCategoryEnabled,
      communityCategoryEnabled:
          communityCategoryEnabled ?? this.communityCategoryEnabled,
      familyWelfareCategoryEnabled:
          familyWelfareCategoryEnabled ?? this.familyWelfareCategoryEnabled,
      systemAccountCategoryEnabled:
          systemAccountCategoryEnabled ?? this.systemAccountCategoryEnabled,
    );
  }
}

abstract class NotificationPreferencesRepository {
  Future<NotificationPreferencesModel> getPreferences();
  Future<void> updatePreferences(NotificationPreferencesModel model);
}

class NotificationPreferencesRepositoryImpl
    implements NotificationPreferencesRepository {
  const NotificationPreferencesRepositoryImpl(this._client);

  final ApiClient _client;

  @override
  Future<NotificationPreferencesModel> getPreferences() async {
    final res = await _client.get<Map<String, dynamic>>(
      '/api/v1/notifications/preferences',
      fromJson: (data) => data as Map<String, dynamic>,
    );
    return NotificationPreferencesModel.fromJson(res);
  }

  @override
  Future<void> updatePreferences(NotificationPreferencesModel model) async {
    await _client.put<dynamic>(
      '/api/v1/notifications/preferences',
      data: model.toJson(),
    );
  }
}
