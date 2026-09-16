// test/features_screens_test.dart
//
// Widget test suite for Phase 2, Phase 2.9, and Phase 3 presentation screens:
// - OnboardingPersonaScreen (PSG-05 / J1)
// - EmergencyScreen (P3-24)
// - SeniorRequestFlowScreen (P3-21)
// - VolunteerFeedScreen (P3-22)
// - ActiveAssignmentScreen (P3-23)
// - LogActivityScreen (P2-14)

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:senior_connect/core/network/api_client.dart';
import 'package:senior_connect/features/auth/presentation/onboarding_persona_screen.dart';
import 'package:senior_connect/features/community/presentation/community_feed_screen.dart';
import 'package:senior_connect/features/community/presentation/event_detail_screen.dart';
import 'package:senior_connect/features/community/presentation/my_appointments_screen.dart';
import 'package:senior_connect/features/family/presentation/family_dashboard_screen.dart';
import 'package:senior_connect/features/family/presentation/senior_access_log_screen.dart';
import 'package:senior_connect/features/help_requests/presentation/active_assignment_screen.dart';
import 'package:senior_connect/features/help_requests/presentation/emergency_screen.dart';
import 'package:senior_connect/features/help_requests/presentation/my_request_status_screen.dart';
import 'package:senior_connect/features/help_requests/presentation/senior_request_flow_screen.dart';
import 'package:senior_connect/features/help_requests/presentation/volunteer_feed_screen.dart';
import 'package:senior_connect/features/help_requests/presentation/widgets/first_meeting_protocol_dialog.dart';
import 'package:senior_connect/features/help_requests/presentation/widgets/safeguarding_concern_dialog.dart';
import 'package:senior_connect/features/organizations/presentation/coordinator_attention_dashboard_screen.dart';
import 'package:senior_connect/features/organizations/presentation/coordinator_bulk_entry_screen.dart';
import 'package:senior_connect/features/organizations/presentation/coordinator_hours_queue_screen.dart';
import 'package:senior_connect/features/organizations/presentation/coordinator_roster_screen.dart';
import 'package:senior_connect/features/organizations/presentation/log_activity_screen.dart';
import 'package:senior_connect/features/organizations/presentation/organization_post_form_screen.dart';
import 'package:senior_connect/features/organizations/presentation/organization_profile_screen.dart';
import 'package:senior_connect/features/organizations/presentation/organizations_list_screen.dart';
import 'package:senior_connect/features/profile/presentation/help_faq_screen.dart';

import 'matrix.dart';

void main() {
  final testApiClient = ApiClient(baseUrl: 'http://localhost:5000');

  group('OnboardingPersonaScreen (PSG-05)', () {
    testAcrossMatrix('renders 4 persona cards without overflow', (
      tester,
      c,
    ) async {
      PersonaOption? selected;
      await tester.pumpWidget(
        wrapForTest(
          OnboardingPersonaScreen(onPersonaSelected: (opt) => selected = opt),
          c,
        ),
      );
      await tester.pumpAndSettle();

      await expectNoOverflow(tester);

      // Verify the 4 cards exist
      expect(find.byKey(const Key('persona_family_support')), findsOneWidget);
      expect(find.byKey(const Key('persona_need_help')), findsOneWidget);
      expect(find.byKey(const Key('persona_new_in_austria')), findsOneWidget);
      expect(find.byKey(const Key('persona_want_to_help')), findsOneWidget);

      // Tap newcomer card and verify callback
      final newcomerCard = find.byKey(const Key('persona_new_in_austria'));
      await tester.ensureVisible(newcomerCard);
      await tester.tap(newcomerCard);
      await tester.pump();
      expect(selected, PersonaOption.newInAustria);
    }, matrix: smokeMatrix());
  });

  group('EmergencyScreen (P3-24)', () {
    testAcrossMatrix(
      'renders 144 and 112 emergency call options with disclaimers',
      (tester, c) async {
        await tester.pumpWidget(wrapForTest(const EmergencyScreen(), c));
        await tester.pumpAndSettle();

        await expectNoOverflow(tester);

        // Verify emergency numbers exist
        expect(find.textContaining('144'), findsWidgets);
        expect(find.textContaining('112'), findsWidgets);
      },
      matrix: smokeMatrix(),
    );
  });

  group('SeniorRequestFlowScreen (P3-21)', () {
    testAcrossMatrix('renders category picker and advances steps', (
      tester,
      c,
    ) async {
      await tester.pumpWidget(
        wrapForTest(
          SeniorRequestFlowScreen(
            apiClient: testApiClient,
            initialCategories: const [
              {'id': '00000000-0000-0000-0000-000000000001', 'code': 'shopping', 'isBlocked': false},
              {'id': '00000000-0000-0000-0000-000000000002', 'code': 'doctor', 'isBlocked': false},
              {'id': '00000000-0000-0000-0000-000000000003', 'code': 'authority', 'isBlocked': false},
              {'id': '00000000-0000-0000-0000-000000000004', 'code': 'accompaniment', 'isBlocked': false},
            ],
          ),
          c,
        ),
      );
      await tester.pumpAndSettle();

      await expectNoOverflow(tester);

      // Verify category cards render (including newcomer categories)
      expect(find.byType(InkWell), findsWidgets);

      // Select first category card
      final firstCategoryCard = find.byType(InkWell).first;
      await tester.ensureVisible(firstCategoryCard);
      await tester.tap(firstCategoryCard);
      await tester.pumpAndSettle();

      // Step 2: timing step renders
      await expectNoOverflow(tester);
    }, matrix: smokeMatrix());
  });

  group('VolunteerFeedScreen (P3-22)', () {
    testAcrossMatrix('renders feed list or state views without overflow', (
      tester,
      c,
    ) async {
      await tester.pumpWidget(
        wrapForTest(VolunteerFeedScreen(apiClient: testApiClient), c),
      );
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 300));

      await expectNoOverflow(tester);
    }, matrix: smokeMatrix());
  });

  group('ActiveAssignmentScreen (P3-23)', () {
    testAcrossMatrix('renders active assignment actions without overflow', (
      tester,
      c,
    ) async {
      await tester.pumpWidget(
        wrapForTest(ActiveAssignmentScreen(apiClient: testApiClient), c),
      );
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 300));

      await expectNoOverflow(tester);
    }, matrix: smokeMatrix());
  });

  group('MyRequestStatusScreen (P3-23 senior side / P4-08)', () {
    testAcrossMatrix('renders request status without overflow', (
      tester,
      c,
    ) async {
      await tester.pumpWidget(
        wrapForTest(MyRequestStatusScreen(apiClient: testApiClient), c),
      );
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 300));

      await expectNoOverflow(tester);
    }, matrix: smokeMatrix());
  });

  group('LogActivityScreen (P2-14)', () {
    testAcrossMatrix('renders self-log form and controls without overflow', (
      tester,
      c,
    ) async {
      await tester.pumpWidget(
        wrapForTest(LogActivityScreen(apiClient: testApiClient), c),
      );
      await tester.pumpAndSettle();

      await expectNoOverflow(tester);
    }, matrix: smokeMatrix());
  });

  group('FirstMeetingProtocolDialog (P4-08)', () {
    testAcrossMatrix('renders protocol checklist without overflow', (
      tester,
      c,
    ) async {
      await tester.pumpWidget(
        wrapForTest(const FirstMeetingProtocolDialog(), c),
      );
      await tester.pumpAndSettle();

      await expectNoOverflow(tester);
      expect(find.byType(FirstMeetingProtocolDialog), findsOneWidget);
    }, matrix: smokeMatrix());
  });

  group('SafeguardingConcernDialog (P4-10)', () {
    testAcrossMatrix('renders concern reporting dialog without overflow', (
      tester,
      c,
    ) async {
      await tester.pumpWidget(
        wrapForTest(
          const SafeguardingConcernDialog(subjectUserId: 'user-123'),
          c,
        ),
      );
      await tester.pumpAndSettle();

      await expectNoOverflow(tester);
      expect(find.byType(SafeguardingConcernDialog), findsOneWidget);
    }, matrix: smokeMatrix());
  });

  group('CommunityFeedScreen (P5-06)', () {
    testAcrossMatrix(
      'renders community feed with category chips without overflow',
      (tester, c) async {
        await tester.pumpWidget(
          wrapForTest(CommunityFeedScreen(apiClient: testApiClient), c),
        );
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 300));

        await expectNoOverflow(tester);
        expect(find.byType(CommunityFeedScreen), findsOneWidget);
      },
      matrix: smokeMatrix(),
    );
  });

  group('MyAppointmentsScreen (P5-07)', () {
    testAcrossMatrix(
      'renders vertical list of appointments in Senior Mode without overflow',
      (tester, c) async {
        await tester.pumpWidget(
          wrapForTest(MyAppointmentsScreen(apiClient: testApiClient), c),
        );
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 300));

        await expectNoOverflow(tester);
        expect(find.byType(MyAppointmentsScreen), findsOneWidget);
      },
      matrix: smokeMatrix(),
    );
  });

  group('EventDetailScreen (P5-03)', () {
    testAcrossMatrix('renders event detail and RSVP action without overflow', (
      tester,
      c,
    ) async {
      await tester.pumpWidget(
        wrapForTest(
          EventDetailScreen(eventId: 'ev-1', apiClient: testApiClient),
          c,
        ),
      );
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 300));

      await expectNoOverflow(tester);
      expect(find.byType(EventDetailScreen), findsOneWidget);
    }, matrix: smokeMatrix());
  });

  group('FamilyDashboardScreen (P6-07)', () {
    testAcrossMatrix(
      'renders family dashboard and connected seniors without overflow',
      (tester, c) async {
        await tester.pumpWidget(
          wrapForTest(FamilyDashboardScreen(apiClient: testApiClient), c),
        );
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 300));

        await expectNoOverflow(tester);
        expect(find.byType(FamilyDashboardScreen), findsOneWidget);
      },
      matrix: smokeMatrix(),
    );
  });

  group('SeniorAccessLogScreen (P6-08)', () {
    testAcrossMatrix(
      'renders 30-day transparency access log without overflow',
      (tester, c) async {
        await tester.pumpWidget(
          wrapForTest(SeniorAccessLogScreen(apiClient: testApiClient), c),
        );
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 300));

        await expectNoOverflow(tester);
        expect(find.byType(SeniorAccessLogScreen), findsOneWidget);
      },
      matrix: smokeMatrix(),
    );
  });

  group('HelpFaqScreen (P7-16)', () {
    testAcrossMatrix(
      'renders accessible help & FAQ accordion without overflow',
      (tester, c) async {
        await tester.pumpWidget(wrapForTest(const HelpFaqScreen(), c));
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 300));

        await expectNoOverflow(tester);
        expect(find.byType(HelpFaqScreen), findsOneWidget);
      },
      matrix: smokeMatrix(),
    );
  });

  group('OrganizationsListScreen (P2-25)', () {
    testAcrossMatrix('renders organization directory without overflow', (
      tester,
      c,
    ) async {
      await tester.pumpWidget(
        wrapForTest(OrganizationsListScreen(apiClient: testApiClient), c),
      );
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 300));

      await expectNoOverflow(tester);
      expect(find.byType(OrganizationsListScreen), findsOneWidget);
    }, matrix: smokeMatrix());
  });

  group('OrganizationProfileScreen (P2-25)', () {
    testAcrossMatrix(
      'renders organization header, news and events without overflow',
      (tester, c) async {
        await tester.pumpWidget(
          wrapForTest(
            OrganizationProfileScreen(
              organizationId: 'org-1',
              apiClient: testApiClient,
            ),
            c,
          ),
        );
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 300));

        await expectNoOverflow(tester);
        expect(find.byType(OrganizationProfileScreen), findsOneWidget);
      },
      matrix: smokeMatrix(),
    );
  });

  group('OrganizationPostFormScreen (P2-25)', () {
    testAcrossMatrix(
      'toggles date and capacity fields between news and event categories',
      (tester, c) async {
        await tester.pumpWidget(
          wrapForTest(
            OrganizationPostFormScreen(
              organizationId: 'org-1',
              apiClient: testApiClient,
            ),
            c,
          ),
        );
        await tester.pumpAndSettle();

        await expectNoOverflow(tester);

        // Defaults to "event" — date fields visible.
        expect(find.text('organizations.starts_at_label'.tr()), findsOneWidget);

        // Switch to "news" — date fields hidden.
        await tester.tap(find.text('organizations.post_type_news'.tr()));
        await tester.pumpAndSettle();
        expect(find.text('organizations.starts_at_label'.tr()), findsNothing);
      },
      matrix: smokeMatrix(),
    );
  });

  group('CoordinatorAttentionDashboardScreen (P2-26)', () {
    testAcrossMatrix(
      'renders attention dashboard triage metrics without overflow',
      (tester, c) async {
        await tester.pumpWidget(
          wrapForTest(
            CoordinatorAttentionDashboardScreen(
              organizationId: 'org-1',
              apiClient: testApiClient,
            ),
            c,
          ),
        );
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 300));

        await expectNoOverflow(tester);
        expect(find.byType(CoordinatorAttentionDashboardScreen), findsOneWidget);
      },
      matrix: smokeMatrix(),
    );
  });

  group('CoordinatorRosterScreen (P2-27)', () {
    testAcrossMatrix(
      'renders volunteer roster and status chips without overflow',
      (tester, c) async {
        await tester.pumpWidget(
          wrapForTest(
            CoordinatorRosterScreen(
              organizationId: 'org-1',
              apiClient: testApiClient,
            ),
            c,
          ),
        );
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 300));

        await expectNoOverflow(tester);
        expect(find.byType(CoordinatorRosterScreen), findsOneWidget);
      },
      matrix: smokeMatrix(),
    );
  });

  group('CoordinatorHoursQueueScreen (P2-28)', () {
    testAcrossMatrix(
      'renders hours confirmation queue without overflow',
      (tester, c) async {
        await tester.pumpWidget(
          wrapForTest(
            CoordinatorHoursQueueScreen(
              organizationId: 'org-1',
              apiClient: testApiClient,
            ),
            c,
          ),
        );
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 300));

        await expectNoOverflow(tester);
        expect(find.byType(CoordinatorHoursQueueScreen), findsOneWidget);
      },
      matrix: smokeMatrix(),
    );
  });

  group('CoordinatorBulkEntryScreen (P2-15)', () {
    testAcrossMatrix(
      'renders bulk entry form without overflow',
      (tester, c) async {
        await tester.pumpWidget(
          wrapForTest(
            CoordinatorBulkEntryScreen(
              organizationId: 'org-1',
              apiClient: testApiClient,
            ),
            c,
          ),
        );
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 300));

        await expectNoOverflow(tester);
        expect(find.byType(CoordinatorBulkEntryScreen), findsOneWidget);
      },
      matrix: smokeMatrix(),
    );
  });
}

