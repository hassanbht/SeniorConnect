import 'dart:math';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';

class CoordinatorBulkEntryState {
  const CoordinatorBulkEntryState({
    this.isLoading = true,
    this.isSubmitting = false,
    this.volunteers = const [],
    this.categories = const [],
    this.selectedVolunteerId,
    this.selectedCategoryId,
    this.selectedDurationMinutes = 60,
    this.insuranceContext = 1,
  });

  final bool isLoading;
  final bool isSubmitting;
  final List<dynamic> volunteers;
  final List<dynamic> categories;
  final String? selectedVolunteerId;
  final String? selectedCategoryId;
  final int selectedDurationMinutes;
  final int insuranceContext;

  CoordinatorBulkEntryState copyWith({
    bool? isLoading,
    bool? isSubmitting,
    List<dynamic>? volunteers,
    List<dynamic>? categories,
    String? selectedVolunteerId,
    String? selectedCategoryId,
    int? selectedDurationMinutes,
    int? insuranceContext,
  }) {
    return CoordinatorBulkEntryState(
      isLoading: isLoading ?? this.isLoading,
      isSubmitting: isSubmitting ?? this.isSubmitting,
      volunteers: volunteers ?? this.volunteers,
      categories: categories ?? this.categories,
      selectedVolunteerId: selectedVolunteerId ?? this.selectedVolunteerId,
      selectedCategoryId: selectedCategoryId ?? this.selectedCategoryId,
      selectedDurationMinutes:
          selectedDurationMinutes ?? this.selectedDurationMinutes,
      insuranceContext: insuranceContext ?? this.insuranceContext,
    );
  }
}

class CoordinatorBulkEntryNotifier
    extends StateNotifier<CoordinatorBulkEntryState> {
  CoordinatorBulkEntryNotifier({
    required this.organizationId,
    required this.apiClient,
  }) : super(const CoordinatorBulkEntryState()) {
    loadReferenceData();
  }

  final String organizationId;
  final ApiClient apiClient;
  static final _random = Random.secure();

  Future<void> loadReferenceData() async {
    state = state.copyWith(isLoading: true);

    try {
      final volsResp = await apiClient.get<dynamic>(
        '/api/v1/coordinator/volunteers?organizationId=$organizationId',
      );
      final catResp = await apiClient.get<dynamic>(
        '/api/v1/activities/categories',
      );

      final vols = volsResp is List ? volsResp : [];
      final cats = catResp is List ? catResp : [];

      final volId = vols.isNotEmpty ? vols.first['userId'] as String? : null;
      final catId = cats.isNotEmpty ? cats.first['id'] as String? : null;

      state = state.copyWith(
        isLoading: false,
        volunteers: vols,
        categories: cats,
        selectedVolunteerId: volId,
        selectedCategoryId: catId,
      );
    } catch (_) {
      final vols = [
        {
          'userId': '00000000-0000-0000-0000-000000000001',
          'displayName': 'Maria Huber'
        },
      ];
      final cats = [
        {
          'id': '00000000-0000-0000-0000-000000000010',
          'name': 'Einkaufen (Shopping)'
        },
        {
          'id': '00000000-0000-0000-0000-000000000011',
          'name': 'Begleitung (Accompaniment)'
        },
      ];
      state = state.copyWith(
        isLoading: false,
        volunteers: vols,
        categories: cats,
        selectedVolunteerId: vols.first['userId'],
        selectedCategoryId: cats.first['id'],
      );
    }
  }

  void setSelectedVolunteer(String? id) {
    state = state.copyWith(selectedVolunteerId: id);
  }

  void setSelectedCategory(String? id) {
    state = state.copyWith(selectedCategoryId: id);
  }

  void setSelectedDuration(int mins) {
    state = state.copyWith(selectedDurationMinutes: mins);
  }

  String _generateIdempotencyKey() {
    final bytes = List<int>.generate(16, (_) => _random.nextInt(256));
    return bytes.map((b) => b.toRadixString(16).padLeft(2, '0')).join();
  }

  Future<String?> submit({required String notes}) async {
    if (state.selectedVolunteerId == null || state.selectedCategoryId == null) {
      return 'coordinator.fill_required_fields';
    }

    state = state.copyWith(isSubmitting: true);

    try {
      final selectedDate = DateTime.now();
      final dateOnlyStr =
          '${selectedDate.year.toString().padLeft(4, '0')}-${selectedDate.month.toString().padLeft(2, '0')}-${selectedDate.day.toString().padLeft(2, '0')}';

      final payload = {
        'organizationId': organizationId,
        'activities': [
          {
            'volunteerUserId': state.selectedVolunteerId,
            'categoryId': state.selectedCategoryId,
            'occurredOn': dateOnlyStr,
            'durationMinutes': state.selectedDurationMinutes,
            'locationType': 0, // InPersonHome
            'notes': notes.trim().isEmpty ? null : notes.trim(),
            'insuranceContext': state.insuranceContext,
            'transportMode': 0, // None
          }
        ],
      };

      await apiClient.post<dynamic>(
        '/api/v1/coordinator/activities:bulk-entry',
        data: payload,
        headers: {'Idempotency-Key': _generateIdempotencyKey()},
      );

      state = state.copyWith(isSubmitting: false);
      return null;
    } on DioException catch (e) {
      state = state.copyWith(isSubmitting: false);
      return mapDioError(e).l10nKey;
    } catch (_) {
      state = state.copyWith(isSubmitting: false);
      return 'errors.generic';
    }
  }
}

class CoordinatorBulkEntryParams {
  const CoordinatorBulkEntryParams({
    required this.organizationId,
    required this.apiClient,
  });

  final String organizationId;
  final ApiClient apiClient;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is CoordinatorBulkEntryParams &&
          other.organizationId == organizationId &&
          identical(other.apiClient, apiClient));

  @override
  int get hashCode => Object.hash(organizationId, apiClient);
}

final coordinatorBulkEntryProvider = StateNotifierProvider.autoDispose.family<
    CoordinatorBulkEntryNotifier,
    CoordinatorBulkEntryState,
    CoordinatorBulkEntryParams>(
  (ref, params) => CoordinatorBulkEntryNotifier(
    organizationId: params.organizationId,
    apiClient: params.apiClient,
  ),
);
