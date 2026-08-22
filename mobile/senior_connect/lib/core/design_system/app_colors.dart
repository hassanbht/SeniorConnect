// lib/core/design_system/app_colors.dart
//
// SeniorConnect colour tokens.
//
// RULES
//  - Nothing in lib/features may reference Colors.* or Color(0xFF...).
//  - Semantic colours (success / warning / emergency / trust) are NOT part of
//    Flutter's ColorScheme, so they live in AppSemanticColors, exposed as a
//    ThemeExtension and read via context.appColors.
//  - Every text pair here is verified against WCAG. Do not change a value
//    without re-checking contrast.

import 'package:flutter/material.dart';

abstract final class AppPalette {
  // ---- Brand -------------------------------------------------------------
  static const petrol900 = Color(0xFF00201C);
  static const petrol700 = Color(0xFF00504A);
  static const petrol600 = Color(0xFF14625A); // primary (light)
  static const petrol300 = Color(0xFF6FD9C8); // primary (dark)
  static const petrol100 = Color(0xFFA8F2E4);

  // ---- Accent (warm amber) -----------------------------------------------
  static const amber900 = Color(0xFF2C1600);
  static const amber800 = Color(0xFF4A2800);
  static const amber700 = Color(0xFF6B3F00);
  static const amber600 = Color(0xFF8A5100); // accent text on light
  static const amber500 = Color(0xFFB36A00);
  static const amber300 = Color(0xFFFFB870); // accent on dark
  static const amber100 = Color(0xFFFFDDB5);

  // ---- Warm neutrals ------------------------------------------------------
  static const neutral0 = Color(0xFFFFFFFF);
  static const neutral25 = Color(0xFFFAF8F5);
  static const neutral50 = Color(0xFFF0EDE8);
  static const neutral200 = Color(0xFFD5CFC6);
  static const neutral400 = Color(0xFF9A938A);
  static const neutral500 = Color(0xFF857F76);
  static const neutral600 = Color(0xFF4A453E);
  static const neutral700 = Color(0xFF3A3733);
  static const neutral800 = Color(0xFF2A2825);
  static const neutral850 = Color(0xFF252320);
  static const neutral900 = Color(0xFF1E1D1A);
  static const neutral950 = Color(0xFF161513);
  static const neutralInk = Color(0xFF1B1A18);
  static const neutralPaper = Color(0xFFF2EEE8);

  // ---- Semantic -----------------------------------------------------------
  static const successLight = Color(0xFF2E6B3A);
  static const successDark = Color(0xFF7FD68C);
  static const successContainerLight = Color(0xFFC6EFCB);
  static const successContainerDark = Color(0xFF1E4A28);

  static const errorLight = Color(0xFFB3261E);
  static const errorDark = Color(0xFFFFB4AB);
  static const errorContainerLight = Color(0xFFF9DEDC);
  static const errorContainerDark = Color(0xFF93000A);
  static const onErrorContainerLight = Color(0xFF410E0B);
  static const onErrorContainerDark = Color(0xFFFFDAD6);

  /// Reserved exclusively for Emergency / SOS. Never use for validation errors.
  static const emergencyLight = Color(0xFFC0261F);
  static const emergencyDark = Color(0xFFFF6B60);

  // ---- Trust badge accents ------------------------------------------------
  static const trustIdentity = Color(0xFF14625A);
  static const trustPhone = Color(0xFF1B5E8A);
  static const trustAddress = Color(0xFF6B3FA0);
  static const trustOrganization = Color(0xFF8A5100);
  static const trustTraining = Color(0xFF2E6B3A);
  static const trustBackground = Color(0xFF9A2C2C);
}

/// Semantic colours that Flutter's [ColorScheme] does not provide.
@immutable
class AppSemanticColors extends ThemeExtension<AppSemanticColors> {
  const AppSemanticColors({
    required this.success,
    required this.onSuccess,
    required this.successContainer,
    required this.onSuccessContainer,
    required this.warning,
    required this.onWarning,
    required this.warningContainer,
    required this.onWarningContainer,
    required this.emergency,
    required this.onEmergency,
    required this.surfaceElevated,
    required this.trustIdentity,
    required this.trustPhone,
    required this.trustAddress,
    required this.trustOrganization,
    required this.trustTraining,
    required this.trustBackgroundCheck,
  });

  final Color success;
  final Color onSuccess;
  final Color successContainer;
  final Color onSuccessContainer;
  final Color warning;
  final Color onWarning;
  final Color warningContainer;
  final Color onWarningContainer;

  /// SOS only. Using this anywhere else is a defect.
  final Color emergency;
  final Color onEmergency;

  /// Dark mode expresses elevation with colour, not shadow.
  final Color surfaceElevated;

  final Color trustIdentity;
  final Color trustPhone;
  final Color trustAddress;
  final Color trustOrganization;
  final Color trustTraining;
  final Color trustBackgroundCheck;

  static const light = AppSemanticColors(
    success: AppPalette.successLight,
    onSuccess: AppPalette.neutral0,
    successContainer: AppPalette.successContainerLight,
    onSuccessContainer: Color(0xFF0C2712),
    warning: AppPalette.amber600,
    onWarning: AppPalette.neutral0,
    warningContainer: AppPalette.amber100,
    onWarningContainer: AppPalette.amber900,
    emergency: AppPalette.emergencyLight,
    onEmergency: AppPalette.neutral0,
    surfaceElevated: AppPalette.neutral0,
    trustIdentity: AppPalette.trustIdentity,
    trustPhone: AppPalette.trustPhone,
    trustAddress: AppPalette.trustAddress,
    trustOrganization: AppPalette.trustOrganization,
    trustTraining: AppPalette.trustTraining,
    trustBackgroundCheck: AppPalette.trustBackground,
  );

  static const dark = AppSemanticColors(
    success: AppPalette.successDark,
    onSuccess: Color(0xFF0C2712),
    successContainer: AppPalette.successContainerDark,
    onSuccessContainer: AppPalette.successContainerLight,
    warning: AppPalette.amber300,
    onWarning: AppPalette.amber800,
    warningContainer: AppPalette.amber700,
    onWarningContainer: AppPalette.amber100,
    emergency: AppPalette.emergencyDark,
    onEmergency: Color(0xFF3A0603),
    surfaceElevated: AppPalette.neutral850,
    trustIdentity: AppPalette.petrol300,
    trustPhone: Color(0xFF8CC8F0),
    trustAddress: Color(0xFFC8AAF0),
    trustOrganization: AppPalette.amber300,
    trustTraining: AppPalette.successDark,
    trustBackgroundCheck: Color(0xFFF0A0A0),
  );

  @override
  AppSemanticColors copyWith({
    Color? success,
    Color? onSuccess,
    Color? successContainer,
    Color? onSuccessContainer,
    Color? warning,
    Color? onWarning,
    Color? warningContainer,
    Color? onWarningContainer,
    Color? emergency,
    Color? onEmergency,
    Color? surfaceElevated,
    Color? trustIdentity,
    Color? trustPhone,
    Color? trustAddress,
    Color? trustOrganization,
    Color? trustTraining,
    Color? trustBackgroundCheck,
  }) {
    return AppSemanticColors(
      success: success ?? this.success,
      onSuccess: onSuccess ?? this.onSuccess,
      successContainer: successContainer ?? this.successContainer,
      onSuccessContainer: onSuccessContainer ?? this.onSuccessContainer,
      warning: warning ?? this.warning,
      onWarning: onWarning ?? this.onWarning,
      warningContainer: warningContainer ?? this.warningContainer,
      onWarningContainer: onWarningContainer ?? this.onWarningContainer,
      emergency: emergency ?? this.emergency,
      onEmergency: onEmergency ?? this.onEmergency,
      surfaceElevated: surfaceElevated ?? this.surfaceElevated,
      trustIdentity: trustIdentity ?? this.trustIdentity,
      trustPhone: trustPhone ?? this.trustPhone,
      trustAddress: trustAddress ?? this.trustAddress,
      trustOrganization: trustOrganization ?? this.trustOrganization,
      trustTraining: trustTraining ?? this.trustTraining,
      trustBackgroundCheck: trustBackgroundCheck ?? this.trustBackgroundCheck,
    );
  }

  @override
  AppSemanticColors lerp(ThemeExtension<AppSemanticColors>? other, double t) {
    if (other is! AppSemanticColors) return this;
    return AppSemanticColors(
      success: Color.lerp(success, other.success, t)!,
      onSuccess: Color.lerp(onSuccess, other.onSuccess, t)!,
      successContainer: Color.lerp(
        successContainer,
        other.successContainer,
        t,
      )!,
      onSuccessContainer: Color.lerp(
        onSuccessContainer,
        other.onSuccessContainer,
        t,
      )!,
      warning: Color.lerp(warning, other.warning, t)!,
      onWarning: Color.lerp(onWarning, other.onWarning, t)!,
      warningContainer: Color.lerp(
        warningContainer,
        other.warningContainer,
        t,
      )!,
      onWarningContainer: Color.lerp(
        onWarningContainer,
        other.onWarningContainer,
        t,
      )!,
      emergency: Color.lerp(emergency, other.emergency, t)!,
      onEmergency: Color.lerp(onEmergency, other.onEmergency, t)!,
      surfaceElevated: Color.lerp(surfaceElevated, other.surfaceElevated, t)!,
      trustIdentity: Color.lerp(trustIdentity, other.trustIdentity, t)!,
      trustPhone: Color.lerp(trustPhone, other.trustPhone, t)!,
      trustAddress: Color.lerp(trustAddress, other.trustAddress, t)!,
      trustOrganization: Color.lerp(
        trustOrganization,
        other.trustOrganization,
        t,
      )!,
      trustTraining: Color.lerp(trustTraining, other.trustTraining, t)!,
      trustBackgroundCheck: Color.lerp(
        trustBackgroundCheck,
        other.trustBackgroundCheck,
        t,
      )!,
    );
  }
}

extension AppColorsX on BuildContext {
  AppSemanticColors get appColors =>
      Theme.of(this).extension<AppSemanticColors>()!;

  ColorScheme get colors => Theme.of(this).colorScheme;
}

// ---------------------------------------------------------------------------
// ColorScheme definitions
// ---------------------------------------------------------------------------

abstract final class AppColorSchemes {
  static const light = ColorScheme(
    brightness: Brightness.light,
    primary: AppPalette.petrol600,
    onPrimary: AppPalette.neutral0,
    primaryContainer: AppPalette.petrol100,
    onPrimaryContainer: AppPalette.petrol900,
    secondary: AppPalette.amber500,
    onSecondary: AppPalette.neutral0,
    secondaryContainer: AppPalette.amber100,
    onSecondaryContainer: AppPalette.amber900,
    tertiary: AppPalette.amber600,
    onTertiary: AppPalette.neutral0,
    tertiaryContainer: AppPalette.amber100,
    onTertiaryContainer: AppPalette.amber900,
    error: AppPalette.errorLight,
    onError: AppPalette.neutral0,
    errorContainer: AppPalette.errorContainerLight,
    onErrorContainer: AppPalette.onErrorContainerLight,
    surface: AppPalette.neutral25,
    onSurface: AppPalette.neutralInk,
    surfaceContainerLowest: AppPalette.neutral0,
    surfaceContainerLow: AppPalette.neutral25,
    surfaceContainer: AppPalette.neutral50,
    surfaceContainerHigh: AppPalette.neutral50,
    surfaceContainerHighest: AppPalette.neutral200,
    onSurfaceVariant: AppPalette.neutral600,
    outline: AppPalette.neutral500,
    outlineVariant: AppPalette.neutral200,
    scrim: Color(0x66000000),
    shadow: Color(0x1A000000),
    inverseSurface: AppPalette.neutral900,
    onInverseSurface: AppPalette.neutralPaper,
    inversePrimary: AppPalette.petrol300,
  );

  static const dark = ColorScheme(
    brightness: Brightness.dark,
    primary: AppPalette.petrol300,
    onPrimary: Color(0xFF00382F),
    primaryContainer: AppPalette.petrol700,
    onPrimaryContainer: AppPalette.petrol100,
    secondary: AppPalette.amber300,
    onSecondary: AppPalette.amber800,
    secondaryContainer: AppPalette.amber700,
    onSecondaryContainer: AppPalette.amber100,
    tertiary: AppPalette.amber300,
    onTertiary: AppPalette.amber800,
    tertiaryContainer: AppPalette.amber700,
    onTertiaryContainer: AppPalette.amber100,
    error: AppPalette.errorDark,
    onError: Color(0xFF690005),
    errorContainer: AppPalette.errorContainerDark,
    onErrorContainer: AppPalette.onErrorContainerDark,
    surface: AppPalette.neutral950,
    onSurface: AppPalette.neutralPaper,
    surfaceContainerLowest: AppPalette.neutral950,
    surfaceContainerLow: AppPalette.neutral900,
    surfaceContainer: AppPalette.neutral850,
    surfaceContainerHigh: AppPalette.neutral800,
    surfaceContainerHighest: AppPalette.neutral700,
    onSurfaceVariant: Color(0xFFCFC8BF),
    outline: AppPalette.neutral400,
    outlineVariant: AppPalette.neutral700,
    scrim: Color(0xA6000000),
    shadow: Color(0x66000000),
    inverseSurface: AppPalette.neutralPaper,
    onInverseSurface: AppPalette.neutralInk,
    inversePrimary: AppPalette.petrol600,
  );

  /// Phase 5. Pure black-on-white, 2px outlines, no fills.
  static const highContrastLight = ColorScheme(
    brightness: Brightness.light,
    primary: Color(0xFF003D36),
    onPrimary: Color(0xFFFFFFFF),
    primaryContainer: Color(0xFFFFFFFF),
    onPrimaryContainer: Color(0xFF000000),
    secondary: Color(0xFF4A2800),
    onSecondary: Color(0xFFFFFFFF),
    secondaryContainer: Color(0xFFFFFFFF),
    onSecondaryContainer: Color(0xFF000000),
    error: Color(0xFF8C0009),
    onError: Color(0xFFFFFFFF),
    errorContainer: Color(0xFFFFFFFF),
    onErrorContainer: Color(0xFF000000),
    surface: Color(0xFFFFFFFF),
    onSurface: Color(0xFF000000),
    onSurfaceVariant: Color(0xFF000000),
    outline: Color(0xFF000000),
    outlineVariant: Color(0xFF000000),
    scrim: Color(0xCC000000),
    inverseSurface: Color(0xFF000000),
    onInverseSurface: Color(0xFFFFFFFF),
  );

  /// Phase 5.
  static const highContrastDark = ColorScheme(
    brightness: Brightness.dark,
    primary: Color(0xFF9FF5E5),
    onPrimary: Color(0xFF000000),
    primaryContainer: Color(0xFF000000),
    onPrimaryContainer: Color(0xFFFFFFFF),
    secondary: Color(0xFFFFD9A8),
    onSecondary: Color(0xFF000000),
    secondaryContainer: Color(0xFF000000),
    onSecondaryContainer: Color(0xFFFFFFFF),
    error: Color(0xFFFFD2CC),
    onError: Color(0xFF000000),
    errorContainer: Color(0xFF000000),
    onErrorContainer: Color(0xFFFFFFFF),
    surface: Color(0xFF000000),
    onSurface: Color(0xFFFFFFFF),
    onSurfaceVariant: Color(0xFFFFFFFF),
    outline: Color(0xFFFFFFFF),
    outlineVariant: Color(0xFFFFFFFF),
    scrim: Color(0xE6000000),
    inverseSurface: Color(0xFFFFFFFF),
    onInverseSurface: Color(0xFF000000),
  );
}
