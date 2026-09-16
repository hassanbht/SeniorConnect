// P4-17: the checklist requires both high-contrast themes to pass 7:1
// (WCAG AAA for normal text) on every text/background pair actually used.
// This was previously "visually plausible" but never actually verified —
// this test computes the real WCAG contrast ratio from the ColorScheme
// constants and fails the build if either theme regresses below 7:1.

import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:senior_connect/core/design_system/app_colors.dart';

/// WCAG 2.x relative luminance of an sRGB color (channels as 0.0-1.0).
double _relativeLuminance(Color color) {
  double linearize(double c) =>
      c <= 0.03928 ? c / 12.92 : math.pow((c + 0.055) / 1.055, 2.4).toDouble();

  return 0.2126 * linearize(color.r) +
      0.7152 * linearize(color.g) +
      0.0722 * linearize(color.b);
}

double _contrastRatio(Color a, Color b) {
  final la = _relativeLuminance(a);
  final lb = _relativeLuminance(b);
  final lighter = la > lb ? la : lb;
  final darker = la > lb ? lb : la;
  return (lighter + 0.05) / (darker + 0.05);
}

void _expectAaa(String pairName, Color foreground, Color background) {
  final ratio = _contrastRatio(foreground, background);
  expect(
    ratio,
    greaterThanOrEqualTo(7.0),
    reason: '$pairName contrast ratio is ${ratio.toStringAsFixed(2)}:1, '
        'below the 7:1 WCAG AAA requirement for high-contrast mode',
  );
}

void _checkScheme(String label, ColorScheme scheme) {
  group(label, () {
    test('text-on-surface pairs meet 7:1', () {
      _expectAaa('onSurface/surface', scheme.onSurface, scheme.surface);
      _expectAaa('onSurfaceVariant/surface', scheme.onSurfaceVariant, scheme.surface);
    });

    test('primary pairs meet 7:1', () {
      _expectAaa('onPrimary/primary', scheme.onPrimary, scheme.primary);
      _expectAaa('onPrimaryContainer/primaryContainer', scheme.onPrimaryContainer, scheme.primaryContainer);
    });

    test('secondary pairs meet 7:1', () {
      _expectAaa('onSecondary/secondary', scheme.onSecondary, scheme.secondary);
      _expectAaa('onSecondaryContainer/secondaryContainer', scheme.onSecondaryContainer, scheme.secondaryContainer);
    });

    test('error pairs meet 7:1', () {
      _expectAaa('onError/error', scheme.onError, scheme.error);
      _expectAaa('onErrorContainer/errorContainer', scheme.onErrorContainer, scheme.errorContainer);
    });
  });
}

void main() {
  _checkScheme('AppColorSchemes.highContrastLight', AppColorSchemes.highContrastLight);
  _checkScheme('AppColorSchemes.highContrastDark', AppColorSchemes.highContrastDark);
}
