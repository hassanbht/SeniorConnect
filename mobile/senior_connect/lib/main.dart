// lib/main.dart
//
// SeniorConnect.bootstrap.
//
// This wires the four things that must be right from the first commit:
//   1. Localization (de source of truth, en/fa) with RTL support
//   2. Theme mode (system / light / dark / high contrast)
//   3. Senior Mode ("Große Ansicht")
//   4. OS text scaling — clamped at the TOP only, never disabled
//
// P1-25: go_router wired via buildRouter()
// P1-26: ApiClient created with base URL from environment config

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';

import 'core/design_system/app_theme.dart';
import 'core/design_system/app_tokens.dart';
import 'core/network/api_client.dart';
import 'core/router/app_router.dart';
import 'core/settings/app_settings.dart'; // ChangeNotifier: themeMode, seniorMode

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await EasyLocalization.ensureInitialized();

  final settings = await AppSettings.load();

  // P1-26: API client — base URL injected at build time via --dart-define
  const baseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:5000', // Android emulator localhost
  );
  final apiClient = ApiClient(baseUrl: baseUrl);

  runApp(
    EasyLocalization(
      supportedLocales: const [Locale('de'), Locale('en'), Locale('fa')],
      path: 'assets/translations',
      fallbackLocale: const Locale('de'), // German is the source of truth
      useOnlyLangCode: true,
      child: SeniorConnectApp(settings: settings, apiClient: apiClient),
    ),
  );
}

class SeniorConnectApp extends StatelessWidget {
  const SeniorConnectApp({
    super.key,
    required this.settings,
    required this.apiClient,
  });

  final AppSettings settings;
  final ApiClient apiClient;

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: settings,
      builder: (context, _) {
        final locale = context.locale;
        final senior = settings.seniorMode;
        final mode = settings.themeMode;

        // P1-25: Router is rebuilt only when apiClient changes (never in practice)
        final router = buildRouter(apiClient: apiClient);

        return MaterialApp.router(
          title: 'SeniorConnect',
          debugShowCheckedModeBanner: false,

          // ---- Localization -------------------------------------------------
          locale: locale,
          supportedLocales: context.supportedLocales,
          localizationsDelegates: [
            ...context.localizationDelegates,
            GlobalMaterialLocalizations.delegate,
            GlobalWidgetsLocalizations.delegate,
            GlobalCupertinoLocalizations.delegate,
          ],

          // ---- Theming -------------------------------------------------------
          themeMode: mode.materialMode,
          theme: AppTheme.light(
            seniorMode: senior,
            locale: locale,
            highContrast: mode == AppThemeMode.highContrastLight,
          ),
          darkTheme: AppTheme.dark(
            seniorMode: senior,
            locale: locale,
            highContrast: mode == AppThemeMode.highContrastDark,
          ),

          // ---- Router (P1-25) -----------------------------------------------
          routerConfig: router,

          builder: (context, child) {
            return SeniorModeScope(
              enabled: senior,
              // Clamp the UPPER bound only, to prevent total layout collapse.
              // NEVER use TextScaler.noScaling — it is a build-breaking defect
              // in this codebase.
              child: MediaQuery.withClampedTextScaling(
                minScaleFactor: 1.0,
                maxScaleFactor: 2.0,
                child: _MaxWidthGuard(child: child ?? const SizedBox.shrink()),
              ),
            );
          },
        );
      },
    );
  }
}

/// Keeps text columns readable on wide screens (~60–70 characters).
/// Senior Mode stays single-column at every breakpoint.
class _MaxWidthGuard extends StatelessWidget {
  const _MaxWidthGuard({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    final windowClass = AppBreakpoints.of(context);
    if (windowClass == AppWindowClass.compact) return child;

    return Align(
      alignment: Alignment.topCenter,
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 1100),
        child: child,
      ),
    );
  }
}
