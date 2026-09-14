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
import '../../features/community/presentation/event_detail_screen.dart';
import '../../features/community/presentation/my_appointments_screen.dart';
import '../../features/discovery/presentation/discovery_screen.dart';
import '../../features/family/presentation/family_dashboard_screen.dart';
import '../../features/family/presentation/senior_access_log_screen.dart';
import '../../features/help_requests/presentation/active_assignment_screen.dart';
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
import '../../features/profile/presentation/profile_edit_screen.dart';
import '../../features/profile/presentation/profile_view_screen.dart';
import '../../shared/senior/senior_shell.dart';
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
  static const helpRequestCreate = '/help-requests/create';
  static const volunteerFeed = '/help-requests/feed';
  static const activeAssignment = '/help-requests/active';
  static const emergency = '/emergency';
  static const logActivity = '/activities/log';
  static const community = '/community';
  static const organizations = '/organizations';
  static const discovery = '/discovery';
  static const myAppointments = '/community/my-appointments';
  static const family = '/family';
  static const familyAccessLog = '/family/access-log';
  static const helpFaq = '/help-faq';
}

IntakeFormType _parseFormType(String pathSegment) =>
    pathSegment == 'help_seeker' ? IntakeFormType.helpSeeker : IntakeFormType.volunteer;

/// The `formType` path segment for [IntakeRoutes] — matches the backend's
/// `GET /forms/{formType}` convention ("volunteer" / "help_seeker").
String intakeFormTypeSegment(IntakeFormType formType) =>
    formType == IntakeFormType.helpSeeker ? 'help_seeker' : 'volunteer';

GoRouter buildRouter({required ApiClient apiClient}) {
  return GoRouter(
    initialLocation: AppRoutes.phoneEntry,
    redirect: (context, state) async {
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
          return OtpVerifyScreen(phone: phone, apiClient: apiClient, purpose: purpose);
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
            builder: (context, state) =>
                SeniorRequestFlowScreen(apiClient: apiClient),
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
            path: AppRoutes.organizations,
            name: 'organizations',
            builder: (context, state) =>
                OrganizationsListScreen(apiClient: apiClient),
          ),
          GoRoute(
            path: AppRoutes.discovery,
            name: 'discovery',
            builder: (context, state) =>
                DiscoveryScreen(apiClient: apiClient),
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
                builder: (context, state) => CoordinatorAttentionDashboardScreen(
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
            builder: (context, state) => ProfileViewScreen(apiClient: apiClient),
            routes: [
              GoRoute(
                path: 'edit',
                name: 'profile-edit',
                builder: (context, state) => ProfileEditScreen(apiClient: apiClient),
              ),
              GoRoute(
                path: 'devices',
                name: 'device-list',
                builder: (context, state) => DeviceListScreen(apiClient: apiClient),
              ),
              GoRoute(
                path: 'totp-enroll',
                name: 'totp-enroll',
                builder: (context, state) => TotpEnrollmentScreen(apiClient: apiClient),
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
    return SeniorHome(
      greeting: 'home.greeting'.tr(args: ['']),
      actions: [
        SeniorAction(
          icon: Icons.handshake_outlined,
          label: 'home.senior.request_help'.tr(),
          semanticLabel: 'semantic.request_help_button'.tr(),
          onTap: () => context.push(AppRoutes.helpRequestCreate),
        ),
        SeniorAction(
          icon: Icons.volunteer_activism_outlined,
          label: 'home.senior.my_activities'.tr(),
          semanticLabel: 'home.senior.my_activities'.tr(),
          onTap: () => context.push(AppRoutes.volunteerFeed),
        ),
        SeniorAction(
          icon: Icons.edit_calendar_outlined,
          label: 'Einsatz erfassen',
          semanticLabel: 'Einsatz erfassen',
          onTap: () => context.push(AppRoutes.logActivity),
        ),
        SeniorAction(
          icon: Icons.groups_outlined,
          label: 'community.title'.tr(),
          semanticLabel: 'community.title'.tr(),
          onTap: () => context.push(AppRoutes.community),
        ),
        SeniorAction(
          icon: Icons.apartment_outlined,
          label: 'organizations.directory_title'.tr(),
          semanticLabel: 'organizations.directory_title'.tr(),
          onTap: () => context.push(AppRoutes.organizations),
        ),
        SeniorAction(
          icon: Icons.near_me_outlined,
          label: 'discovery.title'.tr(),
          semanticLabel: 'discovery.title'.tr(),
          onTap: () => context.push(AppRoutes.discovery),
        ),
        SeniorAction(
          icon: Icons.calendar_month_outlined,
          label: 'community.my_appointments'.tr(),
          semanticLabel: 'community.my_appointments'.tr(),
          onTap: () => context.push(AppRoutes.myAppointments),
        ),
        SeniorAction(
          icon: Icons.family_restroom_outlined,
          label: 'family.title'.tr(),
          semanticLabel: 'family.title'.tr(),
          onTap: () => context.push(AppRoutes.family),
        ),
        SeniorAction(
          icon: Icons.person_outline,
          label: 'profile.title'.tr(),
          semanticLabel: 'profile.title'.tr(),
          onTap: () => context.push(AppRoutes.profile),
        ),
      ],
      emergencyLabel: 'home.senior.emergency'.tr(),
      emergencySemanticLabel: 'semantic.emergency_button'.tr(),
      onEmergency: () => context.push(AppRoutes.emergency),
    );
  }
}

class _RouterErrorScreen extends StatelessWidget {
  const _RouterErrorScreen({this.error});
  final Exception? error;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Center(child: Text(error?.toString() ?? 'Navigation error')),
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
