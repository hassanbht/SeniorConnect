// lib/main.dart
//
// SeniorConnect bootstrap.
//
// This wires the four things that must be right from the first commit:
//   1. Localization (de source of truth, en/it/fa) with RTL support
//   2. Theme mode (system / light / dark / high contrast)
//   3. Senior Mode ("Große Ansicht")
//   4. OS text scaling — clamped at the TOP only, never disabled
//
// Verify the exact package versions against pub.dev when you scaffold the
// project. This file is a skeleton, not a pinned dependency set.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';

import 'core/design_system/app_theme.dart';
import 'core/design_system/app_tokens.dart';
import 'core/settings/app_settings.dart'; // ChangeNotifier: themeMode, seniorMode

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await EasyLocalization.ensureInitialized();

  // await configureDependencies();
  final settings = await AppSettings.load();

  runApp(
    EasyLocalization(
      supportedLocales: const [Locale('de'), Locale('en'), Locale('fa')],
      path: 'assets/translations',
      fallbackLocale: const Locale('de'), // German is the source of truth
      useOnlyLangCode: true,
      child: SeniorConnectApp(settings: settings),
    ),
  );
}

class SeniorConnectApp extends StatelessWidget {
  const SeniorConnectApp({super.key, required this.settings});

  final AppSettings settings;

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: settings,
      builder: (context, _) {
        final locale = context.locale;
        final senior = settings.seniorMode;
        final mode = settings.themeMode;

        return MaterialApp(
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

          // home: const AppBootstrap(),
          // routerConfig: appRouter,   // use MaterialApp.router with go_router
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
