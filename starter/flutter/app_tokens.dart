// lib/core/design_system/app_tokens.dart
//
// Spacing, radius, breakpoints, durations and typography.
//
// NOTE ON flutter_screenutil
//   Spacing may use .w where it genuinely helps on very small or very large
//   screens. TYPOGRAPHY MUST NOT use .sp — type size must follow the operating
//   system text-scale setting, not the design width. Scaling type by screen
//   width fights the accessibility setting and is a defect in this product.

import 'package:flutter/material.dart';

// ---------------------------------------------------------------------------
// Spacing — 4pt grid
// ---------------------------------------------------------------------------

abstract final class AppSpacing {
  static const double xxs = 2;
  static const double xs = 4;
  static const double sm = 8;
  static const double md = 12;
  static const double lg = 16;
  static const double xl = 24;
  static const double xxl = 32;
  static const double xxxl = 48;

  /// Horizontal page padding.
  static const double pageH = lg;
  static const double pageHSenior = xl;

  /// Minimum gap between two tappable elements.
  static const double tapGap = md;
}

// ---------------------------------------------------------------------------
// Radius
// ---------------------------------------------------------------------------

abstract final class AppRadius {
  static const double sm = 8;
  static const double md = 12;
  static const double lg = 16;
  static const double xl = 24;
  static const double full = 999;

  static const chip = BorderRadius.all(Radius.circular(sm));
  static const field = BorderRadius.all(Radius.circular(md));
  static const card = BorderRadius.all(Radius.circular(lg));
  static const sheet =
      BorderRadius.vertical(top: Radius.circular(xl));
}

// ---------------------------------------------------------------------------
// Touch targets
// ---------------------------------------------------------------------------

abstract final class AppTouch {
  static const double minStandard = 48;
  static const double minSenior = 64;
  static const double buttonHeightStandard = 52;
  static const double buttonHeightSenior = 72;

  static double min(bool seniorMode) => seniorMode ? minSenior : minStandard;
  static double buttonHeight(bool seniorMode) =>
      seniorMode ? buttonHeightSenior : buttonHeightStandard;
}

// ---------------------------------------------------------------------------
// Breakpoints
// ---------------------------------------------------------------------------

enum AppWindowClass { compact, medium, expanded, large }

abstract final class AppBreakpoints {
  static const double compact = 600;
  static const double medium = 905;
  static const double expanded = 1280;

  static AppWindowClass of(BuildContext context) {
    final w = MediaQuery.sizeOf(context).width;
    if (w < compact) return AppWindowClass.compact;
    if (w < medium) return AppWindowClass.medium;
    if (w < expanded) return AppWindowClass.expanded;
    return AppWindowClass.large;
  }

  /// Maximum readable text column width (~60–70 characters).
  static const double maxTextWidth = 680;
}

// ---------------------------------------------------------------------------
// Motion
// ---------------------------------------------------------------------------

abstract final class AppMotion {
  static const fast = Duration(milliseconds: 150);
  static const normal = Duration(milliseconds: 220);
  static const slow = Duration(milliseconds: 320);

  /// Respect the OS "Reduce Motion" setting.
  static Duration duration(BuildContext context, Duration d) =>
      MediaQuery.disableAnimationsOf(context) ? Duration.zero : d;
}

// ---------------------------------------------------------------------------
// Typography
// ---------------------------------------------------------------------------

abstract final class AppTypography {
  static const String latinFamily = 'AtkinsonHyperlegibleNext';
  static const String arabicFamily = 'Vazirmatn';

  static const List<String> latinFallback = <String>['Inter', 'Roboto'];

  /// Chooses the family for the active locale. Persian/Arabic need Vazirmatn.
  static String familyFor(Locale locale) =>
      (locale.languageCode == 'fa' || locale.languageCode == 'ar')
          ? arabicFamily
          : latinFamily;

  static TextTheme textTheme({
    required bool seniorMode,
    required Locale locale,
    required Color onSurface,
  }) {
    final family = familyFor(locale);
    // Senior scale is ~1.25x the standard scale. The OS text-scale factor is
    // applied on top of this by Flutter — do not pre-multiply it here.
    final s = seniorMode ? 1.25 : 1.0;

    TextStyle t(double size, FontWeight weight, double height) => TextStyle(
          fontFamily: family,
          fontFamilyFallback: latinFallback,
          fontSize: size * s,
          fontWeight: weight,
          height: height,
          color: onSurface,
          letterSpacing: 0,
        );

    return TextTheme(
      displayLarge: t(32, FontWeight.w700, 1.20),
      displayMedium: t(28, FontWeight.w700, 1.22),
      headlineLarge: t(26, FontWeight.w700, 1.25),
      headlineMedium: t(22, FontWeight.w600, 1.30),
      headlineSmall: t(20, FontWeight.w600, 1.32),
      titleLarge: t(20, FontWeight.w600, 1.35),
      titleMedium: t(17, FontWeight.w600, 1.40),
      titleSmall: t(15, FontWeight.w600, 1.40),
      // Body line height 1.55 — required by WCAG 1.4.12 and materially helps
      // older readers.
      bodyLarge: t(17, FontWeight.w400, 1.55),
      bodyMedium: t(15, FontWeight.w400, 1.55),
      bodySmall: t(13, FontWeight.w400, 1.50),
      labelLarge: t(17, FontWeight.w600, 1.20),
      labelMedium: t(15, FontWeight.w500, 1.25),
      // 13 is the absolute minimum size anywhere in this app.
      labelSmall: t(13, FontWeight.w500, 1.30),
    );
  }
}

// ---------------------------------------------------------------------------
// Senior Mode ("Große Ansicht") — a device preference, never a role.
// ---------------------------------------------------------------------------

@immutable
class SeniorModeScope extends InheritedWidget {
  const SeniorModeScope({
    super.key,
    required this.enabled,
    required super.child,
  });

  final bool enabled;

  static bool of(BuildContext context) =>
      context
          .dependOnInheritedWidgetOfExactType<SeniorModeScope>()
          ?.enabled ??
      false;

  @override
  bool updateShouldNotify(SeniorModeScope oldWidget) =>
      enabled != oldWidget.enabled;
}

extension SeniorModeX on BuildContext {
  bool get isSeniorMode => SeniorModeScope.of(this);
  double get minTouchTarget => AppTouch.min(isSeniorMode);
  double get pagePadding =>
      isSeniorMode ? AppSpacing.pageHSenior : AppSpacing.pageH;
}
