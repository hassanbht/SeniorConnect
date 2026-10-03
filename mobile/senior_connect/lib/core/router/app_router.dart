// lib/core/router/app_router.dart
//
// P1-25 / Phase 3 router with server-capability guards and complete core loop routes.
//
// Route guard strategy:
//  - Auth state comes from token presence in secure storage (fast local check).
//  - Capability checks are server-side; client-side is a best-effort redirect only.
//  - The server is ALWAYS the final authority (401/403 from API kicks user out).

import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../features/auth/presentation/auth_screen.dart';
import '../../features/auth/presentation/device_list_screen.dart';
import '../../features/auth/presentation/email_link_screen.dart';
import '../../features/auth/presentation/onboarding_persona_screen.dart';
import '../../features/auth/presentation/otp_verify_screen.dart';
import '../../features/auth/presentation/staff_login_screen.dart';
import '../../features/auth/presentation/totp_enrollment_screen.dart';
import '../../features/community/presentation/community_feed_screen.dart';
import '../../features/community/presentation/create_group_screen.dart';
import '../../features/community/presentation/group_detail_screen.dart';
import '../../features/community/presentation/my_appointments_screen.dart';
import '../../features/discovery/presentation/discovery_screen.dart';
import '../../features/family/presentation/family_dashboard_screen.dart';
import '../../features/family/presentation/senior_access_log_screen.dart';
import '../../features/help_requests/presentation/active_assignment_screen.dart';
import '../../features/help_requests/presentation/my_request_status_screen.dart';
import '../../features/help_requests/presentation/emergency_screen.dart';
import '../../features/help_requests/presentation/senior_request_flow_screen.dart';
import '../../features/help_requests/presentation/volunteer_feed_screen.dart';
import '../../features/organizations/data/intake_form_repository.dart';
import '../../features/organizations/presentation/coordinator_attention_dashboard_screen.dart';
import '../../features/organizations/presentation/coordinator_bulk_entry_screen.dart';
import '../../features/organizations/presentation/coordinator_hours_queue_screen.dart';
import '../../features/organizations/presentation/coordinator_roster_screen.dart';
import '../../features/organizations/presentation/intake_form_screen.dart';
import '../../features/organizations/presentation/intake_form_submissions_screen.dart';
import '../../features/organizations/presentation/log_activity_screen.dart';
import '../../features/organizations/presentation/organization_profile_screen.dart';
import '../../features/organizations/presentation/organizations_list_screen.dart';
import '../../features/profile/presentation/help_faq_screen.dart';
import '../../features/profile/presentation/notification_preferences_screen.dart';
import '../../features/profile/presentation/discovery_settings_screen.dart';
import '../../features/profile/presentation/location_sharing_screen.dart';
import '../../features/profile/presentation/privacy_settings_screen.dart';
import '../../features/profile/presentation/profile_edit_screen.dart';
import '../../features/profile/presentation/profile_view_screen.dart';
import '../../shared/senior/senior_shell.dart';
import '../design_system/app_tokens.dart';
import '../network/api_client.dart';

// Route names — use these constants everywhere, never raw strings
abstract final class AppRoutes {
  static const onboarding = '/auth/onboarding';
  static const phoneEntry = '/auth/phone';
  static const otpVerify = '/auth/otp';
  static const emailLink = '/auth/email-link';
  static const staffLogin = '/auth/staff-login';
  static const home = '/';
  static const profile = '/profile';
  static const profileEdit = '/profile/edit';
  static const deviceList = '/profile/devices';
  static const totpEnrollment = '/profile/totp-enroll';
  static const privacySettings = '/profile/privacy';
  static const locationSharing = '/profile/location-sharing';
  static const discoverySettings = '/profile/discovery';
  static const notificationPreferences = '/profile/notifications';
  static const helpRequestCreate = '/help-requests/create';
  static const volunteerFeed = '/help-requests/feed';
  static const activeAssignment = '/help-requests/active';
  static const myRequestStatus = '/help-requests/status';
  static const emergency = '/emergency';
  static const logActivity = '/activities/log';
  static const community = '/community';
  static const groupCreate = '/community/groups/create';
  static const organizations = '/organizations';
  static const discovery = '/discovery';
  static const myAppointments = '/community/my-appointments';
  static const family = '/family';
  static const familyAccessLog = '/family/access-log';
  static const helpFaq = '/help-faq';
}

IntakeFormType _parseFormType(String pathSegment) =>
    pathSegment == 'help_seeker'
    ? IntakeFormType.helpSeeker
    : IntakeFormType.volunteer;

/// The `formType` path segment for [IntakeRoutes] — matches the backend's
/// `GET /forms/{formType}` convention ("volunteer" / "help_seeker").
String intakeFormTypeSegment(IntakeFormType formType) =>
    formType == IntakeFormType.helpSeeker ? 'help_seeker' : 'volunteer';

GoRouter buildRouter({required ApiClient apiClient}) {
  return GoRouter(
    initialLocation: AppRoutes.phoneEntry,
    // Re-run the redirect when the server ends the session (refresh rejected).
    refreshListenable: apiClient.sessionEnded,
    redirect: (context, state) async {
      // The emergency screen must be reachable signed in or not.
      if (state.matchedLocation == AppRoutes.emergency) return null;

      final token = await apiClient.readAccessToken();
      final isLoggedIn = token != null;
      final isOnAuth = state.matchedLocation.startsWith('/auth');

      if (!isLoggedIn && !isOnAuth) return AppRoutes.phoneEntry;
      if (isLoggedIn && isOnAuth) return AppRoutes.home;
      return null;
    },
    routes: [
      // ---------- Auth routes (no shell) ------------------------------------
      GoRoute(
        path: AppRoutes.onboarding,
        name: 'onboarding',
        builder: (context, state) => const OnboardingPersonaScreen(),
      ),
      GoRoute(
        path: AppRoutes.phoneEntry,
        name: 'phone-entry',
        builder: (context, state) => AuthScreen(apiClient: apiClient),
      ),
      GoRoute(
        path: AppRoutes.otpVerify,
        name: 'otp-verify',
        builder: (context, state) {
          final phone = state.uri.queryParameters['phone'] ?? '';
          final purpose = switch (state.uri.queryParameters['purpose']) {
            'phone_verification' => OtpPurpose.phoneVerification,
            'phone_change' => OtpPurpose.phoneChange,
            _ => OtpPurpose.login,
          };
          return OtpVerifyScreen(
            phone: phone,
            apiClient: apiClient,
            purpose: purpose,
          );
        },
      ),
      GoRoute(
        path: AppRoutes.emailLink,
        name: 'email-link',
        builder: (context, state) => EmailLinkScreen(apiClient: apiClient),
      ),
      GoRoute(
        path: AppRoutes.staffLogin,
        name: 'staff-login',
        builder: (context, state) => StaffLoginScreen(apiClient: apiClient),
      ),
      GoRoute(
        path: AppRoutes.emergency,
        name: 'emergency',
        builder: (context, state) => const EmergencyScreen(),
      ),

      // ---------- Authenticated shell ----------------------------------------
      ShellRoute(
        builder: (context, state, child) => _AuthenticatedShell(child: child),
        routes: [
          GoRoute(
            path: AppRoutes.home,
            name: 'home',
            builder: (context, state) => _HomeScreen(apiClient: apiClient),
          ),
          GoRoute(
            path: AppRoutes.helpRequestCreate,
            name: 'help-request-create',
            builder: (context, state) {
              final extra = state.extra as Map<String, dynamic>?;
              return SeniorRequestFlowScreen(
                apiClient: apiClient,
                seniorUserId: extra?['seniorUserId'] as String?,
              );
            },
          ),
          GoRoute(
            path: AppRoutes.volunteerFeed,
            name: 'volunteer-feed',
            builder: (context, state) =>
                VolunteerFeedScreen(apiClient: apiClient),
          ),
          GoRoute(
            path: '${AppRoutes.activeAssignment}/:id',
            name: 'active-assignment',
            builder: (context, state) => ActiveAssignmentScreen(
              apiClient: apiClient,
              assignmentId: state.pathParameters['id'],
            ),
          ),
          GoRoute(
            path: '${AppRoutes.myRequestStatus}/:id',
            name: 'my-request-status',
            builder: (context, state) => MyRequestStatusScreen(
              apiClient: apiClient,
              requestId: state.pathParameters['id'],
            ),
          ),
          GoRoute(
            path: AppRoutes.logActivity,
            name: 'log-activity',
            builder: (context, state) =>
                LogActivityScreen(apiClient: apiClient),
          ),
          GoRoute(
            path: AppRoutes.community,
            name: 'community',
            builder: (context, state) =>
                CommunityFeedScreen(apiClient: apiClient),
          ),
          GoRoute(
            path: AppRoutes.groupCreate,
            name: 'group-create',
            builder: (context, state) =>
                CreateGroupScreen(apiClient: apiClient),
          ),
          GoRoute(
            path: '${AppRoutes.community}/groups/:id',
            name: 'group-detail',
            builder: (context, state) => GroupDetailScreen(
              groupId: state.pathParameters['id']!,
              apiClient: apiClient,
            ),
          ),
          GoRoute(
            path: AppRoutes.organizations,
            name: 'organizations',
            builder: (context, state) =>
                OrganizationsListScreen(apiClient: apiClient),
          ),
          GoRoute(
            path: AppRoutes.discovery,
            name: 'discovery',
            builder: (context, state) => DiscoveryScreen(apiClient: apiClient),
          ),
          GoRoute(
            path: '${AppRoutes.organizations}/:id',
            name: 'organization-profile',
            builder: (context, state) => OrganizationProfileScreen(
              organizationId: state.pathParameters['id']!,
              apiClient: apiClient,
            ),
            routes: [
              GoRoute(
                path: 'forms/:formType',
                name: 'intake-form',
                builder: (context, state) => IntakeFormScreen(
                  organizationId: state.pathParameters['id']!,
                  formType: _parseFormType(state.pathParameters['formType']!),
                  apiClient: apiClient,
                ),
              ),
              GoRoute(
                path: 'forms/:formType/submissions',
                name: 'intake-form-submissions',
                builder: (context, state) => IntakeFormSubmissionsScreen(
                  organizationId: state.pathParameters['id']!,
                  formType: _parseFormType(state.pathParameters['formType']!),
                  apiClient: apiClient,
                ),
              ),
              GoRoute(
                path: 'coordinator/attention',
                name: 'coordinator-attention',
                builder: (context, state) =>
                    CoordinatorAttentionDashboardScreen(
                      organizationId: state.pathParameters['id']!,
                      apiClient: apiClient,
                    ),
              ),
              GoRoute(
                path: 'coordinator/roster',
                name: 'coordinator-roster',
                builder: (context, state) => CoordinatorRosterScreen(
                  organizationId: state.pathParameters['id']!,
                  apiClient: apiClient,
                ),
              ),
              GoRoute(
                path: 'coordinator/hours-queue',
                name: 'coordinator-hours-queue',
                builder: (context, state) => CoordinatorHoursQueueScreen(
                  organizationId: state.pathParameters['id']!,
                  apiClient: apiClient,
                ),
              ),
              GoRoute(
                path: 'coordinator/bulk-entry',
                name: 'coordinator-bulk-entry',
                builder: (context, state) => CoordinatorBulkEntryScreen(
                  organizationId: state.pathParameters['id']!,
                  apiClient: apiClient,
                ),
              ),
            ],
          ),
          GoRoute(
            path: AppRoutes.myAppointments,
            name: 'my-appointments',
            builder: (context, state) =>
                MyAppointmentsScreen(apiClient: apiClient),
          ),
          GoRoute(
            path: AppRoutes.family,
            name: 'family',
            builder: (context, state) =>
                FamilyDashboardScreen(apiClient: apiClient),
          ),
          GoRoute(
            path: AppRoutes.familyAccessLog,
            name: 'family-access-log',
            builder: (context, state) =>
                SeniorAccessLogScreen(apiClient: apiClient),
          ),
          GoRoute(
            path: AppRoutes.helpFaq,
            name: 'help-faq',
            builder: (context, state) => const HelpFaqScreen(),
          ),
          GoRoute(
            path: AppRoutes.profile,
            name: 'profile',
            builder: (context, state) =>
                ProfileViewScreen(apiClient: apiClient),
            routes: [
              GoRoute(
                path: 'edit',
                name: 'profile-edit',
                builder: (context, state) =>
                    ProfileEditScreen(apiClient: apiClient),
              ),
              GoRoute(
                path: 'devices',
                name: 'device-list',
                builder: (context, state) =>
                    DeviceListScreen(apiClient: apiClient),
              ),
              GoRoute(
                path: 'totp-enroll',
                name: 'totp-enroll',
                builder: (context, state) =>
                    TotpEnrollmentScreen(apiClient: apiClient),
              ),
              GoRoute(
                path: 'location-sharing',
                name: 'location-sharing',
                builder: (context, state) =>
                    LocationSharingScreen(apiClient: apiClient),
              ),
              GoRoute(
                path: 'discovery',
                name: 'discovery-settings',
                builder: (context, state) =>
                    DiscoverySettingsScreen(apiClient: apiClient),
              ),
              GoRoute(
                path: 'privacy',
                name: 'privacy-settings',
                builder: (context, state) =>
                    PrivacySettingsScreen(apiClient: apiClient),
              ),
              GoRoute(
                path: 'notifications',
                name: 'notification-preferences',
                builder: (context, state) =>
                    NotificationPreferencesScreen(apiClient: apiClient),
              ),
            ],
          ),
        ],
      ),
    ],
    errorBuilder: (context, state) => _RouterErrorScreen(error: state.error),
  );
}

class _HomeScreen extends StatelessWidget {
  const _HomeScreen({required this.apiClient});

  final ApiClient apiClient;

  @override
  Widget build(BuildContext context) {
    SeniorAction action(
      IconData icon,
      String labelKey,
      String route, {
      String? semanticKey,
    }) => SeniorAction(
      icon: icon,
      label: labelKey.tr(),
      semanticLabel: (semanticKey ?? labelKey).tr(),
      onTap: () => context.push(route),
    );

    final requestHelp = action(
      Icons.handshake_outlined,
      'home.senior.request_help',
      AppRoutes.helpRequestCreate,
      semanticKey: 'semantic.request_help_button',
    );
    final myAppointments = action(
      Icons.calendar_month_outlined,
      'community.my_appointments',
      AppRoutes.myAppointments,
    );
    final community = action(
      Icons.groups_outlined,
      'community.title',
      AppRoutes.community,
    );
    final family = action(
      Icons.family_restroom_outlined,
      'family.title',
      AppRoutes.family,
    );
    final profile = action(
      Icons.person_outline,
      'profile.title',
      AppRoutes.profile,
    );

    // Senior Mode ("Große Ansicht"): at most 5 home actions (AGENTS.md,
    // docs/design/accessibility.md §4). Everything else stays reachable in
    // the standard layout.
    if (SeniorModeScope.of(context)) {
      return SeniorHome(
        greeting: 'home.greeting'.tr(namedArgs: {'name': ''}).trim(),
        actions: [requestHelp, myAppointments, community, family, profile],
        emergencyLabel: 'home.senior.emergency'.tr(),
        emergencySemanticLabel: 'semantic.emergency_button'.tr(),
        onEmergency: () => context.push(AppRoutes.emergency),
      );
    }

    return _StandardHome(
      actions: [
        requestHelp,
        action(
          Icons.volunteer_activism_outlined,
          'home.volunteer_feed',
          AppRoutes.volunteerFeed,
        ),
        action(
          Icons.edit_calendar_outlined,
          'home.log_activity',
          AppRoutes.logActivity,
        ),
        myAppointments,
        community,
        action(
          Icons.apartment_outlined,
          'organizations.directory_title',
          AppRoutes.organizations,
        ),
        action(Icons.near_me_outlined, 'discovery.title', AppRoutes.discovery),
        family,
        profile,
      ],
    );
  }
}

/// Standard (non-Senior-Mode) home: every destination as a plain list, with
/// the emergency action always first.
class _StandardHome extends StatelessWidget {
  const _StandardHome({required this.actions});

  final List<SeniorAction> actions;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    return Scaffold(
      appBar: AppBar(
        title: Text('home.greeting'.tr(namedArgs: {'name': ''}).trim()),
      ),
      body: ListView(
        children: [
          Semantics(
            button: true,
            label: 'semantic.emergency_button'.tr(),
            excludeSemantics: true,
            child: ListTile(
              leading: Icon(Icons.emergency_outlined, color: colors.error),
              title: Text(
                'home.senior.emergency'.tr(),
                style: TextStyle(color: colors.error),
              ),
              onTap: () => context.push(AppRoutes.emergency),
            ),
          ),
          const Divider(height: 1),
          for (final a in actions)
            Semantics(
              button: true,
              label: a.semanticLabel,
              excludeSemantics: true,
              child: ListTile(
                leading: Icon(a.icon),
                title: Text(a.label),
                trailing: const Icon(Icons.chevron_right),
                onTap: a.onTap,
              ),
            ),
        ],
      ),
    );
  }
}

class _RouterErrorScreen extends StatelessWidget {
  const _RouterErrorScreen({this.error});
  final Exception? error;

  @override
  Widget build(BuildContext context) {
    // The exception text is for developers, never shown to users.
    return Scaffold(
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text('error.navigation'.tr()),
            const SizedBox(height: 16),
            FilledButton(
              onPressed: () => context.go(AppRoutes.home),
              child: Text('error.go_home'.tr()),
            ),
          ],
        ),
      ),
    );
  }
}

class _AuthenticatedShell extends StatelessWidget {
  const _AuthenticatedShell({required this.child});
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Scaffold(body: child);
  }
}
