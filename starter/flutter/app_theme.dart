// lib/core/design_system/app_theme.dart

import 'package:flutter/material.dart';

import 'app_colors.dart';
import 'app_tokens.dart';

enum AppThemeMode {
  system,
  light,
  dark,
  highContrastLight,
  highContrastDark;

  /// Which Flutter ThemeMode this maps to for MaterialApp.
  ThemeMode get materialMode => switch (this) {
        AppThemeMode.system => ThemeMode.system,
        AppThemeMode.light || AppThemeMode.highContrastLight => ThemeMode.light,
        AppThemeMode.dark || AppThemeMode.highContrastDark => ThemeMode.dark,
      };

  bool get isHighContrast =>
      this == AppThemeMode.highContrastLight ||
      this == AppThemeMode.highContrastDark;
}

abstract final class AppTheme {
  static ThemeData light({
    required bool seniorMode,
    required Locale locale,
    bool highContrast = false,
  }) =>
      _build(
        scheme: highContrast
            ? AppColorSchemes.highContrastLight
            : AppColorSchemes.light,
        semantic: AppSemanticColors.light,
        seniorMode: seniorMode,
        locale: locale,
        highContrast: highContrast,
      );

  static ThemeData dark({
    required bool seniorMode,
    required Locale locale,
    bool highContrast = false,
  }) =>
      _build(
        scheme: highContrast
            ? AppColorSchemes.highContrastDark
            : AppColorSchemes.dark,
        semantic: AppSemanticColors.dark,
        seniorMode: seniorMode,
        locale: locale,
        highContrast: highContrast,
      );

  static ThemeData _build({
    required ColorScheme scheme,
    required AppSemanticColors semantic,
    required bool seniorMode,
    required Locale locale,
    required bool highContrast,
  }) {
    final text = AppTypography.textTheme(
      seniorMode: seniorMode,
      locale: locale,
      onSurface: scheme.onSurface,
    );

    final buttonHeight = AppTouch.buttonHeight(seniorMode);
    final minTarget = AppTouch.min(seniorMode);
    final borderWidth = highContrast ? 2.0 : 1.0;

    return ThemeData(
      useMaterial3: true,
      colorScheme: scheme,
      brightness: scheme.brightness,
      scaffoldBackgroundColor: scheme.surface,
      textTheme: text,
      fontFamily: AppTypography.familyFor(locale),
      splashFactory: InkRipple.splashFactory,
      visualDensity: seniorMode
          ? const VisualDensity(horizontal: 1, vertical: 1)
          : VisualDensity.standard,
      extensions: <ThemeExtension<dynamic>>[semantic],

      appBarTheme: AppBarTheme(
        backgroundColor: scheme.surface,
        foregroundColor: scheme.onSurface,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        scrolledUnderElevation: highContrast ? 0 : 1,
        centerTitle: false,
        titleTextStyle: text.titleLarge,
        toolbarHeight: seniorMode ? 72 : 56,
      ),

      cardTheme: CardThemeData(
        color: semantic.surfaceElevated,
        surfaceTintColor: Colors.transparent,
        elevation: highContrast ? 0 : 1,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(
          borderRadius: AppRadius.card,
          side: highContrast
              ? BorderSide(color: scheme.outline, width: borderWidth)
              : BorderSide(color: scheme.outlineVariant, width: 1),
        ),
      ),

      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: Size(minTarget, buttonHeight),
          padding: const EdgeInsets.symmetric(
            horizontal: AppSpacing.xl,
            vertical: AppSpacing.md,
          ),
          shape: const RoundedRectangleBorder(borderRadius: AppRadius.field),
          textStyle: text.labelLarge,
        ),
      ),

      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: Size(minTarget, buttonHeight),
          side: BorderSide(color: scheme.outline, width: borderWidth),
          shape: const RoundedRectangleBorder(borderRadius: AppRadius.field),
          textStyle: text.labelLarge,
        ),
      ),

      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          minimumSize: Size(minTarget, minTarget),
          textStyle: text.labelLarge,
        ),
      ),

      iconButtonTheme: IconButtonThemeData(
        style: IconButton.styleFrom(
          minimumSize: Size(minTarget, minTarget),
          iconSize: seniorMode ? 32 : 24,
        ),
      ),

      inputDecorationTheme: InputDecorationTheme(
        filled: !highContrast,
        fillColor: scheme.surfaceContainer,
        contentPadding: EdgeInsets.symmetric(
          horizontal: AppSpacing.lg,
          vertical: seniorMode ? AppSpacing.xl : AppSpacing.lg,
        ),
        border: OutlineInputBorder(
          borderRadius: AppRadius.field,
          borderSide: BorderSide(color: scheme.outline, width: borderWidth),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: AppRadius.field,
          borderSide: BorderSide(color: scheme.outline, width: borderWidth),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: AppRadius.field,
          borderSide: BorderSide(color: scheme.primary, width: borderWidth + 1),
        ),
        errorBorder: OutlineInputBorder(
          borderRadius: AppRadius.field,
          borderSide: BorderSide(color: scheme.error, width: borderWidth + 1),
        ),
        // Labels always sit above the field. Placeholder-only labels are a
        // documented accessibility defect in this product.
        floatingLabelBehavior: FloatingLabelBehavior.always,
        labelStyle: text.titleSmall,
        errorStyle: text.bodySmall?.copyWith(color: scheme.error),
        errorMaxLines: 3,
      ),

      chipTheme: ChipThemeData(
        padding: const EdgeInsets.symmetric(
          horizontal: AppSpacing.md,
          vertical: AppSpacing.sm,
        ),
        shape: const RoundedRectangleBorder(borderRadius: AppRadius.chip),
        labelStyle: text.labelMedium,
        side: BorderSide(color: scheme.outlineVariant, width: borderWidth),
      ),

      listTileTheme: ListTileThemeData(
        minVerticalPadding: seniorMode ? AppSpacing.lg : AppSpacing.md,
        contentPadding: EdgeInsets.symmetric(
          horizontal: AppSpacing.lg,
          vertical: seniorMode ? AppSpacing.sm : 0,
        ),
        shape: const RoundedRectangleBorder(borderRadius: AppRadius.card),
        titleTextStyle: text.titleMedium,
        subtitleTextStyle: text.bodyMedium?.copyWith(
          color: scheme.onSurfaceVariant,
        ),
      ),

      bottomSheetTheme: BottomSheetThemeData(
        backgroundColor: semantic.surfaceElevated,
        surfaceTintColor: Colors.transparent,
        shape: const RoundedRectangleBorder(borderRadius: AppRadius.sheet),
        showDragHandle: true,
      ),

      dialogTheme: DialogThemeData(
        backgroundColor: semantic.surfaceElevated,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(
          borderRadius: const BorderRadius.all(Radius.circular(AppRadius.xl)),
          side: highContrast
              ? BorderSide(color: scheme.outline, width: borderWidth)
              : BorderSide.none,
        ),
        titleTextStyle: text.headlineSmall,
        contentTextStyle: text.bodyLarge,
      ),

      snackBarTheme: SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
        backgroundColor: scheme.inverseSurface,
        contentTextStyle: text.bodyLarge?.copyWith(
          color: scheme.onInverseSurface,
        ),
        shape: const RoundedRectangleBorder(borderRadius: AppRadius.field),
      ),

      navigationBarTheme: NavigationBarThemeData(
        height: seniorMode ? 88 : 72,
        backgroundColor: semantic.surfaceElevated,
        surfaceTintColor: Colors.transparent,
        indicatorColor: scheme.primaryContainer,
        labelTextStyle: WidgetStatePropertyAll(text.labelMedium),
        // Labels are always visible. Icon-only navigation is a defect here.
        labelBehavior: NavigationDestinationLabelBehavior.alwaysShow,
      ),

      dividerTheme: DividerThemeData(
        color: scheme.outlineVariant,
        thickness: borderWidth,
        space: AppSpacing.lg,
      ),

      // Explicitly disable Material's default page transitions on desktop and
      // keep them short elsewhere. Reduce Motion is handled per-transition via
      // AppMotion.duration().
      pageTransitionsTheme: const PageTransitionsTheme(
        builders: <TargetPlatform, PageTransitionsBuilder>{
          TargetPlatform.android: FadeForwardsPageTransitionsBuilder(),
          TargetPlatform.iOS: CupertinoPageTransitionsBuilder(),
          TargetPlatform.macOS: CupertinoPageTransitionsBuilder(),
          TargetPlatform.windows: FadeForwardsPageTransitionsBuilder(),
          TargetPlatform.linux: FadeForwardsPageTransitionsBuilder(),
        },
      ),
    );
  }
}
