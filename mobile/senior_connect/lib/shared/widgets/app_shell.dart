// lib/shared/widgets/app_shell.dart
//
// Standard navigation shell (bottom navigation, standard density).
//
// The senior shell (max 5 full-width actions, no tabs, no drawer, no
// gestures) lives in lib/shared/senior/senior_shell.dart.
//
// Senior Mode is a device preference, not a role. It is labelled
// „Große Ansicht" in the UI — never „Seniorenmodus". Nobody wants to select
// the mode that identifies them as old.

import 'package:flutter/material.dart';

import '../../core/design_system/app_tokens.dart';

// ---------------------------------------------------------------------------
// Standard shell
// ---------------------------------------------------------------------------

class StandardShellDestination {
  const StandardShellDestination({
    required this.label,
    required this.icon,
    required this.selectedIcon,
  });

  final String label;
  final IconData icon;
  final IconData selectedIcon;
}

class StandardShell extends StatelessWidget {
  const StandardShell({
    super.key,
    required this.destinations,
    required this.currentIndex,
    required this.onDestinationSelected,
    required this.child,
  });

  final List<StandardShellDestination> destinations;
  final int currentIndex;
  final ValueChanged<int> onDestinationSelected;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final windowClass = AppBreakpoints.of(context);

    // Rail on wider windows, bottom bar on phones. Labels always visible in
    // both — icon-only navigation is a documented defect.
    final useRail = windowClass != AppWindowClass.compact;

    if (useRail) {
      return Scaffold(
        body: Row(
          children: [
            NavigationRail(
              selectedIndex: currentIndex,
              onDestinationSelected: onDestinationSelected,
              labelType: NavigationRailLabelType.all,
              minWidth: 88,
              destinations: [
                for (final d in destinations)
                  NavigationRailDestination(
                    icon: Icon(d.icon),
                    selectedIcon: Icon(d.selectedIcon),
                    label: Text(d.label),
                  ),
              ],
            ),
            const VerticalDivider(width: 1),
            Expanded(child: SafeArea(child: child)),
          ],
        ),
      );
    }

    return Scaffold(
      body: SafeArea(child: child),
      bottomNavigationBar: NavigationBar(
        selectedIndex: currentIndex,
        onDestinationSelected: onDestinationSelected,
        destinations: [
          for (final d in destinations)
            NavigationDestination(
              icon: Icon(d.icon),
              selectedIcon: Icon(d.selectedIcon),
              label: d.label,
            ),
        ],
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Page scaffold
// ---------------------------------------------------------------------------

/// Standard page chrome. Constrains text width on wide windows so a paragraph
/// never spans 1000px, and applies the correct page padding for the mode.
class AppPage extends StatelessWidget {
  const AppPage({
    super.key,
    required this.title,
    required this.child,
    this.actions,
    this.onBack,
    this.backSemanticLabel,
    this.bottomBar,
    this.scrollable = true,
    this.constrainWidth = true,
  });

  final String title;
  final Widget child;
  final List<Widget>? actions;
  final VoidCallback? onBack;
  final String? backSemanticLabel;

  /// Sticky primary action. In Senior Mode the main action belongs here, not
  /// at the bottom of a long scroll where it can be missed.
  final Widget? bottomBar;

  final bool scrollable;
  final bool constrainWidth;

  @override
  Widget build(BuildContext context) {
    Widget content = Padding(
      padding: EdgeInsets.symmetric(horizontal: context.pagePadding),
      child: child,
    );

    if (constrainWidth) {
      content = Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(
            maxWidth: AppBreakpoints.maxTextWidth,
          ),
          child: content,
        ),
      );
    }

    if (scrollable) {
      content = SingleChildScrollView(
        padding: const EdgeInsets.symmetric(vertical: AppSpacing.lg),
        child: content,
      );
    }

    return Scaffold(
      appBar: AppBar(
        title: Text(title),
        leading: onBack == null
            ? null
            : IconButton(
                // Mirrors automatically in RTL.
                icon: const Icon(Icons.arrow_back),
                tooltip: backSemanticLabel,
                onPressed: onBack,
              ),
        actions: actions,
      ),
      body: SafeArea(child: content),
      bottomNavigationBar: bottomBar == null
          ? null
          : SafeArea(
              child: Padding(
                padding: EdgeInsets.fromLTRB(
                  context.pagePadding,
                  AppSpacing.md,
                  context.pagePadding,
                  AppSpacing.lg,
                ),
                child: bottomBar,
              ),
            ),
    );
  }
}
