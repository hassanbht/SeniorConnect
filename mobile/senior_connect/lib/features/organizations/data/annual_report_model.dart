// lib/features/organizations/data/annual_report_model.dart

class MetricWithGrowth {
  final int currentValue;
  final int previousYearValue;
  final int growth;
  final String formattedGrowth;

  const MetricWithGrowth({
    required this.currentValue,
    required this.previousYearValue,
    required this.growth,
    required this.formattedGrowth,
  });

  factory MetricWithGrowth.fromJson(Map<String, dynamic> json) {
    return MetricWithGrowth(
      currentValue: (json['currentValue'] as num?)?.toInt() ?? 0,
      previousYearValue: (json['previousYearValue'] as num?)?.toInt() ?? 0,
      growth: (json['growth'] as num?)?.toInt() ?? 0,
      formattedGrowth: json['formattedGrowth'] as String? ?? '',
    );
  }
}

class AnnualStatisticsReport {
  final String organizationId;
  final String organizationName;
  final int year;
  final MetricWithGrowth totalVolunteers;
  final MetricWithGrowth volunteerPool;
  final MetricWithGrowth networkPartners;
  final MetricWithGrowth placements;
  final MetricWithGrowth insuredPersons;
  final MetricWithGrowth eventsAndProjects;
  final double totalHours;
  final int totalActivities;
  final Map<String, double> hoursByCategory;
  final DateTime generatedAtUtc;

  const AnnualStatisticsReport({
    required this.organizationId,
    required this.organizationName,
    required this.year,
    required this.totalVolunteers,
    required this.volunteerPool,
    required this.networkPartners,
    required this.placements,
    required this.insuredPersons,
    required this.eventsAndProjects,
    required this.totalHours,
    required this.totalActivities,
    required this.hoursByCategory,
    required this.generatedAtUtc,
  });

  factory AnnualStatisticsReport.fromJson(Map<String, dynamic> json) {
    final rawHours = json['hoursByCategory'] as Map<String, dynamic>? ?? {};
    final hoursByCategory = rawHours.map(
      (key, value) => MapEntry(key, (value as num?)?.toDouble() ?? 0.0),
    );

    return AnnualStatisticsReport(
      organizationId: json['organizationId'] as String? ?? '',
      organizationName: json['organizationName'] as String? ?? '',
      year: (json['year'] as num?)?.toInt() ?? DateTime.now().year,
      totalVolunteers: MetricWithGrowth.fromJson(
        json['totalVolunteers'] as Map<String, dynamic>? ?? {},
      ),
      volunteerPool: MetricWithGrowth.fromJson(
        json['volunteerPool'] as Map<String, dynamic>? ?? {},
      ),
      networkPartners: MetricWithGrowth.fromJson(
        json['networkPartners'] as Map<String, dynamic>? ?? {},
      ),
      placements: MetricWithGrowth.fromJson(
        json['placements'] as Map<String, dynamic>? ?? {},
      ),
      insuredPersons: MetricWithGrowth.fromJson(
        json['insuredPersons'] as Map<String, dynamic>? ?? {},
      ),
      eventsAndProjects: MetricWithGrowth.fromJson(
        json['eventsAndProjects'] as Map<String, dynamic>? ?? {},
      ),
      totalHours: (json['totalHours'] as num?)?.toDouble() ?? 0.0,
      totalActivities: (json['totalActivities'] as num?)?.toInt() ?? 0,
      hoursByCategory: hoursByCategory,
      generatedAtUtc: DateTime.tryParse(json['generatedAtUtc'] as String? ?? '') ??
          DateTime.now().toUtc(),
    );
  }
}
