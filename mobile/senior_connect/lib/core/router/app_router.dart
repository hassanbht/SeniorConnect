// lib/core/router/app_router.dart
//
// P1-25: go_router routing with server-capability guards.
//
// Route guard strategy:
//  - Auth state comes from token presence in secure storage (fast local check).
//  - Capability checks are server-side; client-side is a best-effort redirect only.
//  - The server is ALWAYS the final authority (401/403 from API kicks user out).

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../features/auth/presentation/phone_entry_screen.dart';
import '../../features/auth/presentation/otp_verify_screen.dart';
import '../../features/profile/presentation/profile_view_screen.dart';
import '../../features/profile/presentation/profile_edit_screen.dart';
import '../network/api_client.dart';
import '../../shared/senior/senior_shell.dart';

// Route names — use these constants everywhere, never raw strings
abstract final class AppRoutes {
  static const phoneEntry = '/auth/phone';
  static const otpVerify = '/auth/otp';
  static const home = '/';
  static const profile = '/profile';
  static const profileEdit = '/profile/edit';
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

      // ---------- Authenticated shell ----------------------------------------
      // Phase 1: minimal scaffold shell — full navigation rail in Phase 2 (P2-25)
      ShellRoute(
        builder: (context, state, child) => _AuthenticatedShell(child: child),
        routes: [
          GoRoute(
            path: AppRoutes.home,
            name: 'home',
            builder: (context, state) => const _HomeScreen(),
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

// Placeholder home screen — will be replaced by SeniorHome/VolunteerHome in P1-24
class _HomeScreen extends StatelessWidget {
  const _HomeScreen();

  @override
  Widget build(BuildContext context) {
    return const SeniorHome(
      greeting: 'Hallo',
      actions: [],
      emergencyLabel: 'Notfall',
      emergencySemanticLabel: 'Notfall. Öffnet die Notfallhilfe.',
      onEmergency: _onEmergency,
    );
  }

  static void _onEmergency() {
    // Phase 3: P3-05 emergency flow (routes to 144/112)
    // NEVER says "help is on the way"
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

/// Minimal authenticated shell for Phase 1.
/// Full navigation rail/bottom bar wired in Phase 2 (P2-25).
class _AuthenticatedShell extends StatelessWidget {
  const _AuthenticatedShell({required this.child});
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Scaffold(body: child);
  }
}
