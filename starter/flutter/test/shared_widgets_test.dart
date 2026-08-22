// test/widgets/shared_widgets_test.dart
//
// Example tests. Copy this pattern for every shared widget and every screen.
//
// Run the fast loop:   flutter test
// Run the full matrix: FULL_MATRIX=1 flutter test

import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:SeniorConnect/shared/widgets/app_button.dart';
import 'package:SeniorConnect/shared/widgets/app_states.dart';
import 'package:SeniorConnect/shared/widgets/app_status.dart';

import '../support/matrix.dart';

List<MatrixCase> get _matrix =>
    Platform.environment['FULL_MATRIX'] == '1' ? buildMatrix() : smokeMatrix();

void main() {
  group('AppButton', () {
    testAcrossMatrix('renders and passes accessibility guidelines', (
      tester,
      c,
    ) async {
      await tester.pumpWidget(
        wrapForTest(
          Padding(
            padding: const EdgeInsets.all(16),
            child: AppButton(
              label: 'Hilfe anfragen',
              semanticLabel: 'Hilfe anfragen. Öffnet das Formular.',
              onPressed: () {},
            ),
          ),
          c,
        ),
      );
      await tester.pumpAndSettle();

      await expectNoOverflow(tester);
      await expectAccessibility(tester);
      await expectSeniorTouchTargets(tester, c);
    }, matrix: _matrix);

    testWidgets('does not fire twice on a rapid double tap', (tester) async {
      var count = 0;
      await tester.pumpWidget(
        wrapForTest(
          AppButton(label: 'Anfragen', onPressed: () => count++),
          const MatrixCase(
            brightness: Brightness.light,
            seniorMode: true,
            textScale: 1.0,
            locale: Locale('de'),
          ),
        ),
      );

      await tester.tap(find.byType(AppButton));
      await tester.pump(const Duration(milliseconds: 50));
      await tester.tap(find.byType(AppButton));
      await tester.pump(const Duration(milliseconds: 50));

      expect(count, 1, reason: 'A senior tapping twice must not submit twice.');
      await tester.pumpAndSettle();
    });

    testWidgets('destructive variant requires confirmation', (tester) async {
      var fired = false;
      await tester.pumpWidget(
        wrapForTest(
          AppButton(
            label: 'Termin absagen',
            variant: AppButtonVariant.destructive,
            confirmationText: 'Möchten Sie den Termin wirklich absagen?',
            onPressed: () => fired = true,
          ),
          const MatrixCase(
            brightness: Brightness.light,
            seniorMode: false,
            textScale: 1.0,
            locale: Locale('de'),
          ),
        ),
      );

      await tester.tap(find.byType(AppButton));
      await tester.pumpAndSettle();

      expect(fired, isFalse, reason: 'Must not fire before confirmation.');
      expect(
        find.text('Möchten Sie den Termin wirklich absagen?'),
        findsOneWidget,
      );
    });
  });

  group('AppStatusChip', () {
    testAcrossMatrix('status is carried by icon and word, not only colour', (
      tester,
      c,
    ) async {
      await tester.pumpWidget(
        wrapForTest(
          const Padding(
            padding: EdgeInsets.all(16),
            child: Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                AppStatusChip(
                  label: 'Wird gesucht',
                  tone: AppStatusTone.pending,
                ),
                AppStatusChip(label: 'Bestätigt', tone: AppStatusTone.success),
                AppStatusChip(label: 'Abgesagt', tone: AppStatusTone.error),
              ],
            ),
          ),
          c,
        ),
      );
      await tester.pumpAndSettle();

      await expectNoOverflow(tester);
      await expectAccessibility(tester);

      // Every chip carries an icon AND a word — grayscale-safe.
      expect(find.byType(Icon), findsNWidgets(3));
      expect(find.text('Bestätigt'), findsOneWidget);
    }, matrix: _matrix);

    test('status code maps to a tone without reading translated text', () {
      // Branching on a translated string is a defect (mobile/AGENTS.md).
      final chip = AppStatusChip.forStatusCode(
        code: 'cancelled',
        label: 'Irgendein übersetzter Text',
      );
      expect(chip.tone, AppStatusTone.error);
    });
  });

  group('AppEmptyState', () {
    testAcrossMatrix('renders a message and an action, never "Keine Daten"', (
      tester,
      c,
    ) async {
      await tester.pumpWidget(
        wrapForTest(
          AppEmptyState(
            icon: Icons.event_outlined,
            message: 'Sie haben noch keine Termine.',
            actionLabel: 'Gruppen in Ihrer Nähe ansehen',
            onAction: () {},
          ),
          c,
        ),
      );
      await tester.pumpAndSettle();

      await expectNoOverflow(tester);
      await expectAccessibility(tester);
      expect(find.text('Keine Daten'), findsNothing);
    }, matrix: _matrix);
  });

  group('AppErrorView', () {
    testAcrossMatrix('a 403 renders what is missing as a checklist', (
      tester,
      c,
    ) async {
      await tester.pumpWidget(
        wrapForTest(
          AppErrorView(
            message: 'Für Hausbesuche fehlt noch eine Bestätigung.',
            retryLabel: 'Nochmal versuchen',
            nextSteps: const [
              'Strafregisterbescheinigung einreichen',
              'Freigabe durch die Organisation abwarten',
            ],
            onRetry: () {},
          ),
          c,
        ),
      );
      await tester.pumpAndSettle();

      await expectNoOverflow(tester);
      await expectAccessibility(tester);

      // authorization.md §5: a refusal must always say what to do next.
      expect(find.textContaining('Strafregisterbescheinigung'), findsOneWidget);
    }, matrix: _matrix);
  });
}
