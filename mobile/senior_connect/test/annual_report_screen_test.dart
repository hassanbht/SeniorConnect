// test/annual_report_screen_test.dart

import 'package:flutter_test/flutter_test.dart';
import 'package:senior_connect/core/network/api_client.dart';
import 'package:senior_connect/features/organizations/presentation/annual_report_screen.dart';

import 'matrix.dart';

void main() {
  final testApiClient = ApiClient(baseUrl: 'http://localhost:5000');

  group('AnnualReportScreen ("Das Freiwilligenzentrum in Zahlen")', () {
    testAcrossMatrix(
      'renders annual report screen without overflow',
      (tester, c) async {
        await tester.pumpWidget(
          wrapForTest(
            AnnualReportScreen(
              organizationId: '00000000-0000-0000-0000-000000000001',
              apiClient: testApiClient,
              initialYear: 2025,
            ),
            c,
          ),
        );
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 300));

        await expectNoOverflow(tester);
        expect(find.byType(AnnualReportScreen), findsOneWidget);
      },
      matrix: smokeMatrix(),
    );
  });
}
