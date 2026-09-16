// lib/features/organizations/application/annual_report_notifier.dart

import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/network/api_client.dart';
import '../data/annual_report_model.dart';
import '../data/annual_report_repository.dart';

class AnnualReportState {
  final bool isLoading;
  final String? errorKey;
  final int selectedYear;
  final AnnualStatisticsReport? report;
  final bool isExportingPdf;
  final String? exportSuccessMessage;

  const AnnualReportState({
    this.isLoading = true,
    this.errorKey,
    this.selectedYear = 2025,
    this.report,
    this.isExportingPdf = false,
    this.exportSuccessMessage,
  });

  AnnualReportState copyWith({
    bool? isLoading,
    String? errorKey,
    int? selectedYear,
    AnnualStatisticsReport? report,
    bool? isExportingPdf,
    String? exportSuccessMessage,
  }) {
    return AnnualReportState(
      isLoading: isLoading ?? this.isLoading,
      errorKey: errorKey,
      selectedYear: selectedYear ?? this.selectedYear,
      report: report ?? this.report,
      isExportingPdf: isExportingPdf ?? this.isExportingPdf,
      exportSuccessMessage: exportSuccessMessage,
    );
  }
}

class AnnualReportNotifier extends StateNotifier<AnnualReportState> {
  final String organizationId;
  final AnnualReportRepository _repository;

  AnnualReportNotifier({
    required this.organizationId,
    required AnnualReportRepository repository,
    int initialYear = 2025,
  })  : _repository = repository,
        super(AnnualReportState(selectedYear: initialYear)) {
    load(initialYear);
  }

  Future<void> load(int year) async {
    state = state.copyWith(isLoading: true, errorKey: null, selectedYear: year);
    try {
      final report = await _repository.getAnnualReport(organizationId, year);
      state = state.copyWith(isLoading: false, report: report);
    } catch (_) {
      state = state.copyWith(isLoading: false, errorKey: 'errors.generic');
    }
  }

  Future<void> setYear(int year) async {
    if (year != state.selectedYear) {
      await load(year);
    }
  }
}

final annualReportProvider = StateNotifierProvider.autoDispose.family<AnnualReportNotifier, AnnualReportState, AnnualReportParams>(
  (ref, params) {
    final repo = AnnualReportRepositoryImpl(params.apiClient);
    return AnnualReportNotifier(
      organizationId: params.organizationId,
      repository: repo,
      initialYear: params.year ?? 2025,
    );
  },
);

class AnnualReportParams {
  final String organizationId;
  final ApiClient apiClient;
  final int? year;

  const AnnualReportParams({
    required this.organizationId,
    required this.apiClient,
    this.year,
  });

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is AnnualReportParams &&
          runtimeType == other.runtimeType &&
          organizationId == other.organizationId &&
          year == other.year;

  @override
  int get hashCode => Object.hash(organizationId, year);
}
