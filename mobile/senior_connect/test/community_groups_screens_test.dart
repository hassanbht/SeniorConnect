// test/community_groups_screens_test.dart
//
// Widget smoke tests for the Community Groups feature:
// - CommunityFeedScreen's "Groups" tab (GroupListTab)
// - GroupDetailScreen
// - CreateGroupScreen
//
// No mock HTTP layer exists in this project (see other *_screens_test.dart
// files) — these point ApiClient at an unreachable localhost port and assert
// the screens degrade to their loading/error states without overflowing,
// exactly like the existing CommunityFeedScreen (events tab) test.

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:senior_connect/core/network/api_client.dart';
import 'package:senior_connect/features/community/presentation/community_feed_screen.dart';
import 'package:senior_connect/features/community/presentation/create_group_screen.dart';
import 'package:senior_connect/features/community/presentation/group_detail_screen.dart';
import 'package:senior_connect/features/community/presentation/group_list_tab.dart';

import 'matrix.dart';

void main() {
  final testApiClient = ApiClient(baseUrl: 'http://localhost:5000');

  group('CommunityFeedScreen', () {
    testAcrossMatrix('renders the Events/Groups tab bar without overflow', (
      tester,
      c,
    ) async {
      await tester.pumpWidget(
        wrapForTest(CommunityFeedScreen(apiClient: testApiClient), c),
      );
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 300));

      await expectNoOverflow(tester);
      expect(find.text('community.tab_events'.tr()), findsOneWidget);
      expect(find.text('community.tab_groups'.tr()), findsOneWidget);
    }, matrix: smokeMatrix());
  });

  group('GroupListTab', () {
    testAcrossMatrix('renders the group discovery list without overflow', (
      tester,
      c,
    ) async {
      await tester.pumpWidget(
        wrapForTest(GroupListTab(apiClient: testApiClient), c),
      );
      await tester.pump();
      await tester.pump(const Duration(seconds: 1));

      await expectNoOverflow(tester);
      expect(find.byType(GroupListTab), findsOneWidget);
    }, matrix: smokeMatrix());
  });

  group('GroupDetailScreen', () {
    testAcrossMatrix(
      'renders group detail (error state offline) without overflow',
      (tester, c) async {
        await tester.pumpWidget(
          wrapForTest(
            GroupDetailScreen(
              groupId: '00000000-0000-0000-0000-000000000001',
              apiClient: testApiClient,
            ),
            c,
          ),
        );
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 300));

        await expectNoOverflow(tester);
        expect(find.byType(GroupDetailScreen), findsOneWidget);
      },
      matrix: smokeMatrix(),
    );
  });

  group('CreateGroupScreen', () {
    testAcrossMatrix('renders the create-group form without overflow', (
      tester,
      c,
    ) async {
      await tester.pumpWidget(
        wrapForTest(CreateGroupScreen(apiClient: testApiClient), c),
      );
      await tester.pump();

      await expectNoOverflow(tester);
      expect(find.byType(CreateGroupScreen), findsOneWidget);
    }, matrix: smokeMatrix());
  });
}
