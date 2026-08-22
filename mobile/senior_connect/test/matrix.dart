// test/support/matrix.dart
//
// The test harness that makes the Definition of Done mechanical.
//
// Every widget test written against `testAcrossMatrix` runs the same widget in
// every combination that mobile/AGENTS.md requires:
//
//     light / dark  ×  standard / senior  ×  scale 1.0 / 1.5 / 2.0  ×  de / en / fa
//
// and asserts the accessibility guidelines Flutter can check automatically.
//
// This is how "did you check dark mode at 200% in Persian?" stops being a
// question anyone has to ask in code review.

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:senior_connect/core/design_system/app_theme.dart';
import 'package:senior_connect/core/design_system/app_tokens.dart';

class MatrixCase {
  const MatrixCase({
    required this.brightness,
    required this.seniorMode,
    required this.textScale,
    required this.locale,
  });

  final Brightness brightness;
  final bool seniorMode;
  final double textScale;
  final Locale locale;

  bool get isRtl => locale.languageCode == 'fa' || locale.languageCode == 'ar';

  @override
  String toString() =>
      '${brightness.name}/'
      '${seniorMode ? 'senior' : 'standard'}/'
      'x$textScale/'
      '${locale.languageCode}';
}

/// The full matrix: 2 × 2 × 3 × 3 = 36 cases.
const fullMatrix = <MatrixCase>[
  // Generated below rather than listed; see [buildMatrix].
];

List<MatrixCase> buildMatrix({
  List<Brightness> brightnesses = const [Brightness.light, Brightness.dark],
  List<bool> seniorModes = const [false, true],
  List<double> textScales = const [1.0, 1.5, 2.0],
  List<Locale> locales = const [Locale('de'), Locale('en'), Locale('fa')],
}) {
  return [
    for (final b in brightnesses)
      for (final s in seniorModes)
        for (final t in textScales)
          for (final l in locales)
            MatrixCase(
              brightness: b,
              seniorMode: s,
              textScale: t,
              locale: l,
            ),
  ];
}

/// A reduced matrix for fast inner-loop tests. Use [buildMatrix] in CI.
List<MatrixCase> smokeMatrix() => buildMatrix(
      textScales: const [1.0, 2.0],
      locales: const [Locale('de'), Locale('fa')],
    );

/// Wraps a widget in the app's real theme for a given matrix case.
Widget wrapForTest(Widget child, MatrixCase c) {
  final theme = c.brightness == Brightness.light
      ? AppTheme.light(seniorMode: c.seniorMode, locale: c.locale)
      : AppTheme.dark(seniorMode: c.seniorMode, locale: c.locale);

  return MaterialApp(
    theme: theme,
    locale: c.locale,
    localizationsDelegates: const [
      GlobalMaterialLocalizations.delegate,
      GlobalWidgetsLocalizations.delegate,
      GlobalCupertinoLocalizations.delegate,
      DefaultMaterialLocalizations.delegate,
      DefaultWidgetsLocalizations.delegate,
    ],
    supportedLocales: const [Locale('de'), Locale('en'), Locale('fa')],
    home: Builder(
      builder: (context) => MediaQuery(
        data: MediaQuery.of(context).copyWith(
          textScaler: TextScaler.linear(c.textScale),
        ),
        child: Directionality(
          textDirection: c.isRtl ? TextDirection.rtl : TextDirection.ltr,
          child: SeniorModeScope(
            enabled: c.seniorMode,
            child: Scaffold(body: child),
          ),
        ),
      ),
    ),
  );
}

/// Runs [body] once per matrix case, with a descriptive test name.
///
/// Usage:
/// ```dart
/// testAcrossMatrix('AppButton', (tester, c) async {
///   await tester.pumpWidget(wrapForTest(
///     AppButton(label: 'Hilfe anfragen', onPressed: () {}),
///     c,
///   ));
///   await expectNoOverflow(tester);
///   await expectAccessibility(tester);
/// });
/// ```
void testAcrossMatrix(
  String description,
  Future<void> Function(WidgetTester tester, MatrixCase c) body, {
  List<MatrixCase>? matrix,
}) {
  for (final c in matrix ?? buildMatrix()) {
    testWidgets('$description [$c]', (tester) => body(tester, c));
  }
}

// ---------------------------------------------------------------------------
// Assertions
// ---------------------------------------------------------------------------

/// Fails if any RenderFlex overflowed. This is the assertion that catches
/// "it looked fine at 100%" before a user at 200% finds it.
Future<void> expectNoOverflow(WidgetTester tester) async {
  final exception = tester.takeException();
  expect(
    exception,
    isNull,
    reason: 'Layout overflowed. A screen must survive text scale 2.0 — see '
        'docs/design/accessibility.md §2.',
  );
}

/// The four automated guidelines. Contrast is checked against the real theme,
/// so a hardcoded colour that happens to be unreadable in dark mode fails here.
Future<void> expectAccessibility(WidgetTester tester) async {
  final handle = tester.ensureSemantics();
  await expectLater(tester, meetsGuideline(androidTapTargetGuideline));
  await expectLater(tester, meetsGuideline(iOSTapTargetGuideline));
  await expectLater(tester, meetsGuideline(labeledTapTargetGuideline));
  await expectLater(tester, meetsGuideline(textContrastGuideline));
  handle.dispose();
}

/// Senior Mode raises the floor above what the platform guidelines require.
Future<void> expectSeniorTouchTargets(WidgetTester tester, MatrixCase c) async {
  if (!c.seniorMode) return;
  final min = AppTouch.minSenior;

  for (final element in find.byType(InkWell).evaluate()) {
    final size = element.size;
    if (size == null) continue;
    expect(
      size.height >= min || size.width >= min,
      isTrue,
      reason: 'Senior Mode requires ${min}dp touch targets, found $size. '
          'See docs/design/accessibility.md §4.',
    );
  }
}

/// Asserts a widget subtree contains no hardcoded pure black or pure white,
/// which are almost always a sign of `Colors.white` / `Colors.black` sneaking in.
/// Not exhaustive — the lint rule is the primary guard — but it catches the
/// common case in a rendered tree.
void expectNoHardcodedMonochrome(WidgetTester tester) {
  final containers = tester.widgetList<Container>(find.byType(Container));
  for (final container in containers) {
    final decoration = container.decoration;
    if (decoration is BoxDecoration) {
      final color = decoration.color;
      if (color == const Color(0xFFFFFFFF) || color == const Color(0xFF000000)) {
        fail(
          'Hardcoded pure black or white found in a Container decoration. '
          'Use Theme.of(context).colorScheme or context.appColors — '
          'see mobile/AGENTS.md.',
        );
      }
    }
  }
}
