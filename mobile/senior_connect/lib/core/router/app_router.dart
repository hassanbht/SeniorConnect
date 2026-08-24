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

import '../../features/auth/presentation/otp_verify_screen.dart';
import '../../features/auth/presentation/phone_entry_screen.dart';
import '../../features/help_requests/presentation/active_assignment_screen.dart';
import '../../features/help_requests/presentation/emergency_screen.dart';
import '../../features/help_requests/presentation/senior_request_flow_screen.dart';
import '../../features/help_requests/presentation/volunteer_feed_screen.dart';
import '../../features/organizations/presentation/log_activity_screen.dart';
import '../../features/profile/presentation/profile_edit_screen.dart';
import '../../features/profile/presentation/profile_view_screen.dart';
import '../../shared/senior/senior_shell.dart';
import '../network/api_client.dart';

// Route names — use these constants everywhere, never raw strings
abstract final class AppRoutes {
  static const phoneEntry = '/auth/phone';
  static const otpVerify = '/auth/otp';
  static const home = '/';
  static const profile = '/profile';
  static const profileEdit = '/profile/edit';
  static const helpRequestCreate = '/help-requests/create';
  static const volunteerFeed = '/help-requests/feed';
  static const activeAssignment = '/help-requests/active';
  static const emergency = '/emergency';
  static const logActivity = '/activities/log';
}

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
        path: AppRoutes.phoneEntry,
        name: 'phone-entry',
        builder: (context, state) => const PhoneEntryScreen(),
      ),
      GoRoute(
        path: AppRoutes.otpVerify,
        name: 'otp-verify',
        builder: (context, state) {
          final phone = state.uri.queryParameters['phone'] ?? '';
          return OtpVerifyScreen(phone: phone);
        },
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
            builder: (context, state) => SeniorRequestFlowScreen(apiClient: apiClient),
          ),
          GoRoute(
            path: AppRoutes.volunteerFeed,
            name: 'volunteer-feed',
            builder: (context, state) => VolunteerFeedScreen(apiClient: apiClient),
          ),
          GoRoute(
            path: AppRoutes.activeAssignment,
            name: 'active-assignment',
            builder: (context, state) => ActiveAssignmentScreen(apiClient: apiClient),
          ),
          GoRoute(
            path: AppRoutes.logActivity,
            name: 'log-activity',
            builder: (context, state) => LogActivityScreen(apiClient: apiClient),
          ),
          GoRoute(
            path: AppRoutes.profile,
            name: 'profile',
            builder: (context, state) => const ProfileViewScreen(),
            routes: [
              GoRoute(
                path: 'edit',
                name: 'profile-edit',
                builder: (context, state) => const ProfileEditScreen(),
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
      body: Center(
        child: Text(error?.toString() ?? 'Navigation error'),
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
