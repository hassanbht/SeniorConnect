// lib/features/community/application/report_notifier.dart
//
// Backs the reusable ReportDialog (report a group or a post).

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/network/api_client.dart';
import '../data/community_repository.dart';
import '../domain/group_enums.dart';

class ReportState {
  const ReportState({
    this.category = CommunityReportCategory.spam,
    this.isSubmitting = false,
    this.submitted = false,
    this.error,
  });

  final CommunityReportCategory category;
  final bool isSubmitting;
  final bool submitted;
  final String? error;

  ReportState copyWith({
    CommunityReportCategory? category,
    bool? isSubmitting,
    bool? submitted,
    String? error,
  }) {
    return ReportState(
      category: category ?? this.category,
      isSubmitting: isSubmitting ?? this.isSubmitting,
      submitted: submitted ?? this.submitted,
      error: error,
    );
  }
}

class ReportNotifier extends StateNotifier<ReportState> {
  ReportNotifier({required this._repository}) : super(const ReportState());

  final CommunityRepository _repository;

  void setCategory(CommunityReportCategory category) =>
      state = state.copyWith(category: category);

  Future<void> submit({
    required CommunityReportTargetType targetType,
    required String targetId,
    required String description,
    String? groupId,
  }) async {
    state = state.copyWith(isSubmitting: true, error: null);
    try {
      await _repository.createReport(
        targetType: targetType,
        targetId: targetId,
        category: state.category,
        description: description.trim(),
        groupId: groupId,
      );
      state = state.copyWith(isSubmitting: false, submitted: true);
    } on DioException catch (e) {
      state = state.copyWith(
        isSubmitting: false,
        error: mapDioError(e).l10nKey,
      );
    } catch (_) {
      state = state.copyWith(isSubmitting: false, error: 'errors.generic');
    }
  }
}

final reportProvider = StateNotifierProvider.autoDispose
    .family<ReportNotifier, ReportState, ApiClient>(
      (ref, apiClient) =>
          ReportNotifier(repository: CommunityRepositoryImpl(apiClient)),
    );
