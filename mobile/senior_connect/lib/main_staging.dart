// lib/main_staging.dart
// Entry point for Staging flavor (P1-19, P8-08)

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'config/app_config.dart';
import 'core/network/api_client.dart';
import 'core/settings/app_settings.dart';
import 'main.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await EasyLocalization.ensureInitialized();

  AppConfig.initialize(
    const AppConfig(
      environment: AppEnvironment.staging,
      appName: 'SeniorConnect (Staging)',
      apiBaseUrl: String.fromEnvironment('API_BASE_URL', defaultValue: 'https://staging-api.seniorconnect.at'),
      enableLogging: true,
      enableMockData: false,
    ),
  );

  final settings = await AppSettings.load();
  final apiClient = ApiClient(baseUrl: AppConfig.instance.apiBaseUrl);

  runApp(
    EasyLocalization(
      supportedLocales: const [Locale('de'), Locale('en'), Locale('fa')],
      path: 'assets/translations',
      fallbackLocale: const Locale('de'),
      useOnlyLangCode: true,
      child: SeniorConnectApp(settings: settings, apiClient: apiClient),
    ),
  );
}
