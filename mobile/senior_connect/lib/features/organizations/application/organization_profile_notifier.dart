import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../data/intake_form_repository.dart';

class OrganizationProfileState {
  const OrganizationProfileState({
    this.isLoading = true,
    this.organization,
    this.newsItems = const [],
    this.events = const [],
    this.canManage = false,
    this.isActivatingTemplate = false,
    this.error,
  });

  final bool isLoading;
  final Map<String, dynamic>? organization;
  final List<Map<String, dynamic>> newsItems;
  final List<Map<String, dynamic>> events;
  final bool canManage;
  final bool isActivatingTemplate;
  final String? error;

  OrganizationProfileState copyWith({
    bool? isLoading,
    Map<String, dynamic>? organization,
    List<Map<String, dynamic>>? newsItems,
    List<Map<String, dynamic>>? events,
    bool? canManage,
    bool? isActivatingTemplate,
    String? error,
  }) {
    return OrganizationProfileState(
      isLoading: isLoading ?? this.isLoading,
      organization: organization ?? this.organization,
      newsItems: newsItems ?? this.newsItems,
      events: events ?? this.events,
      canManage: canManage ?? this.canManage,
      isActivatingTemplate: isActivatingTemplate ?? this.isActivatingTemplate,
      error: error,
    );
  }
}

class OrganizationProfileNotifier extends StateNotifier<OrganizationProfileState> {
  OrganizationProfileNotifier({
    required this.organizationId,
    required this.apiClient,
    IntakeFormRepository? intakeFormRepository,
  })  : _intakeFormRepository =
            intakeFormRepository ?? IntakeFormRepositoryImpl(apiClient),
        super(const OrganizationProfileState()) {
    load();
  }

  final String organizationId;
  final ApiClient apiClient;
  final IntakeFormRepository _intakeFormRepository;

  Future<void> load() async {
    state = state.copyWith(isLoading: true, error: null);
    try {
      final org = await apiClient.get<Map<String, dynamic>>(
        '/api/v1/organizations/$organizationId',
      );

      final posts = await apiClient.get<List<dynamic>>(
        '/api/v1/community/events',
        queryParameters: {'organizationId': organizationId},
      );
      final postMaps =
          posts.map((e) => Map<String, dynamic>.from(e as Map)).toList();

      var canManage = false;
      try {
        final mine = await apiClient.get<List<dynamic>>(
          '/api/v1/me/organizations',
        );
        canManage = mine.any(
          (m) =>
              Map<String, dynamic>.from(m as Map)['organizationId'] ==
              organizationId,
        );
      } catch (_) {
        // Staff-entry-point visibility only
      }

      state = state.copyWith(
        organization: org,
        newsItems: postMaps.where((e) => e['category'] == 'news').toList(),
        events: postMaps.where((e) => e['category'] != 'news').toList(),
        canManage: canManage,
        isLoading: false,
      );
    } catch (e) {
      state = state.copyWith(isLoading: false, error: e.toString());
    }
  }

  Future<String?> activateFwzTemplate() async {
    state = state.copyWith(isActivatingTemplate: true);
    try {
      await _intakeFormRepository.activateFwzTemplate(
        organizationId,
        IntakeFormType.volunteer,
      );
      state = state.copyWith(isActivatingTemplate: false);
      return null;
    } on DioException catch (e) {
      state = state.copyWith(isActivatingTemplate: false);
      return mapDioError(e).l10nKey;
    } catch (_) {
      state = state.copyWith(isActivatingTemplate: false);
      return 'errors.generic';
    }
  }
}

class OrganizationProfileParams {
  const OrganizationProfileParams({
    required this.organizationId,
    required this.apiClient,
  });

  final String organizationId;
  final ApiClient apiClient;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is OrganizationProfileParams &&
          other.organizationId == organizationId &&
          identical(other.apiClient, apiClient));

  @override
  int get hashCode => Object.hash(organizationId, apiClient);
}

final organizationProfileProvider = StateNotifierProvider.autoDispose.family<
    OrganizationProfileNotifier,
    OrganizationProfileState,
    OrganizationProfileParams>(
  (ref, params) => OrganizationProfileNotifier(
    organizationId: params.organizationId,
    apiClient: params.apiClient,
  ),
);
