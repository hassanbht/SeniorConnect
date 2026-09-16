// lib/features/organizations/data/annual_report_repository.dart

import '../../../core/network/api_client.dart';
import 'annual_report_model.dart';

abstract class AnnualReportRepository {
  Future<AnnualStatisticsReport> getAnnualReport(String organizationId, int year);
  Future<List<int>> downloadPdf(String organizationId, int year);
}

class AnnualReportRepositoryImpl implements AnnualReportRepository {
  final ApiClient _apiClient;

  AnnualReportRepositoryImpl(this._apiClient);

  @override
  Future<AnnualStatisticsReport> getAnnualReport(String organizationId, int year) async {
    final response = await _apiClient.get<Map<String, dynamic>>(
      '/api/v1/reporting/organizations/$organizationId/annual-report',
      queryParameters: {'year': year},
    );
    return AnnualStatisticsReport.fromJson(response);
  }

  @override
  Future<List<int>> downloadPdf(String organizationId, int year) async {
    final response = await _apiClient.get<List<dynamic>>(
      '/api/v1/reporting/organizations/$organizationId/annual-report/export.pdf',
      queryParameters: {'year': year},
    );
    return List<int>.from(response);
  }
}
