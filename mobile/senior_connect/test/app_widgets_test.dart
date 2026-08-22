// test/app_widgets_test.dart
//
// Widget matrix tests for lib/shared/widgets/app_widgets.dart.

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:senior_connect/shared/widgets/app_widgets.dart';

import 'matrix.dart';

List<MatrixCase> get _matrix => smokeMatrix();

void main() {
  group('AppButton (app_widgets)', () {
    testAcrossMatrix(
      'renders primary button correctly across matrix',
      (tester, c) async {
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
      },
      matrix: _matrix,
    );

    testAcrossMatrix(
      'renders emergency button correctly across matrix',
      (tester, c) async {
        await tester.pumpWidget(
          wrapForTest(
            Padding(
              padding: const EdgeInsets.all(16),
              child: AppButton(
                label: 'Notruf 144',
                variant: AppButtonVariant.emergency,
                onPressed: () {},
              ),
            ),
            c,
          ),
        );
        await tester.pumpAndSettle();

        await expectNoOverflow(tester);
        await expectAccessibility(tester);
      },
      matrix: _matrix,
    );
  });

  group('AppTextField (app_widgets)', () {
    testAcrossMatrix(
      'renders label above field and error text below with icon',
      (tester, c) async {
        await tester.pumpWidget(
          wrapForTest(
            const Padding(
              padding: EdgeInsets.all(16),
              child: AppTextField(
                label: 'Name',
                hint: 'Max Mustermann',
                errorText: 'Bitte geben Sie einen Namen ein.',
              ),
            ),
            c,
          ),
        );
        await tester.pumpAndSettle();

        await expectNoOverflow(tester);
        expect(find.text('Name'), findsOneWidget);
        expect(find.text('Bitte geben Sie einen Namen ein.'), findsOneWidget);
        expect(find.byIcon(Icons.error_outline), findsOneWidget);
      },
      matrix: _matrix,
    );
  });

  group('AppStatusChip (app_widgets)', () {
    testAcrossMatrix(
      'renders status chips with icon + label across matrix',
      (tester, c) async {
        await tester.pumpWidget(
          wrapForTest(
            const Padding(
              padding: EdgeInsets.all(16),
              child: Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  AppStatusChip(label: 'Offen', tone: AppStatusTone.pending),
                  AppStatusChip(label: 'Bestätigt', tone: AppStatusTone.success),
                  AppStatusChip(label: 'Abgelehnt', tone: AppStatusTone.error),
                ],
              ),
            ),
            c,
          ),
        );
        await tester.pumpAndSettle();

        await expectNoOverflow(tester);
        expect(find.byType(Icon), findsNWidgets(3));
        expect(find.text('Bestätigt'), findsOneWidget);
      },
      matrix: _matrix,
    );
  });

  group('TrustBadge (app_widgets)', () {
    testAcrossMatrix(
      'renders trust badge factually without green tick assessment',
      (tester, c) async {
        await tester.pumpWidget(
          wrapForTest(
            Padding(
              padding: const EdgeInsets.all(16),
              child: TrustBadge(
                kind: TrustBadgeKind.identity,
                label: 'Identität bestätigt',
                verifiedOn: 'Bestätigt am 12.05.2026',
                onTap: () {},
              ),
            ),
            c,
          ),
        );
        await tester.pumpAndSettle();

        await expectNoOverflow(tester);
        expect(find.text('Identität bestätigt'), findsOneWidget);
        expect(find.text('Bestätigt am 12.05.2026'), findsOneWidget);
      },
      matrix: _matrix,
    );
  });

  group('State Views (app_widgets)', () {
    testAcrossMatrix(
      'AppLoading renders spinner and message',
      (tester, c) async {
        await tester.pumpWidget(
          wrapForTest(
            const AppLoading(message: 'Daten werden geladen...'),
            c,
          ),
        );
        await tester.pump();

        await expectNoOverflow(tester);
        expect(find.text('Daten werden geladen...'), findsOneWidget);
      },
      matrix: _matrix,
    );

    testAcrossMatrix(
      'AppEmptyState renders icon, message and action button',
      (tester, c) async {
        await tester.pumpWidget(
          wrapForTest(
            AppEmptyState(
              icon: Icons.event_outlined,
              message: 'Keine anstehenden Termine.',
              actionLabel: 'Suchen',
              onAction: () {},
            ),
            c,
          ),
        );
        await tester.pumpAndSettle();

        await expectNoOverflow(tester);
        expect(find.text('Keine anstehenden Termine.'), findsOneWidget);
        expect(find.text('Suchen'), findsOneWidget);
      },
      matrix: _matrix,
    );

    testAcrossMatrix(
      'AppErrorView renders error icon, message and retry button',
      (tester, c) async {
        await tester.pumpWidget(
          wrapForTest(
            AppErrorView(
              message: 'Verbindung fehlgeschlagen.',
              retryLabel: 'Erneut versuchen',
              onRetry: () {},
            ),
            c,
          ),
        );
        await tester.pumpAndSettle();

        await expectNoOverflow(tester);
        expect(find.text('Verbindung fehlgeschlagen.'), findsOneWidget);
        expect(find.text('Erneut versuchen'), findsOneWidget);
      },
      matrix: _matrix,
    );
  });
}
