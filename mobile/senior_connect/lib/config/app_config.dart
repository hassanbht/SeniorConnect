// lib/config/app_config.dart
//
// P1-19 & P8-08: Flutter Build Flavors & Environment Configuration.

enum AppEnvironment {
  dev,
  staging,
  prod,
}

class AppConfig {
  const AppConfig({
    required this.environment,
    required this.appName,
    required this.apiBaseUrl,
    this.enableLogging = false,
    this.enableMockData = false,
  });

  final AppEnvironment environment;
  final String appName;
  final String apiBaseUrl;
  final bool enableLogging;
  final bool enableMockData;

  static AppConfig? _instance;
  static AppConfig get instance {
    if (_instance == null) {
      throw StateError('AppConfig must be initialized before accessing instance.');
    }
    return _instance!;
  }

  static void initialize(AppConfig config) {
    _instance = config;
  }

  bool get isProduction => environment == AppEnvironment.prod;
  bool get isDevelopment => environment == AppEnvironment.dev;
}
