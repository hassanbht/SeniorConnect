// lib/shared/widgets/app_shell.dart
//
// Two navigation shells, one codebase.
//
//   SeniorShell    max 5 full-width actions, no tabs, no drawer, no gestures
//   StandardShell  bottom navigation, standard density
//
// Senior Mode is a device preference, not a role. It is labelled
// „Große Ansicht" in the UI — never „Seniorenmodus". Nobody wants to select
// the mode that identifies them as old.

import 'package:flutter/material.dart';

import '../../core/design_system/app_colors.dart';
import '../../core/design_system/app_tokens.dart';

// ---------------------------------------------------------------------------
// Senior shell
// ---------------------------------------------------------------------------

class SeniorAction {
  const SeniorAction({
    required this.label,
    required this.icon,
    required this.onTap,
    this.semanticLabel,
    this.isEmergency = false,
    this.badgeCount,
  });

  final String label;
  final IconData icon;
  final VoidCallback onTap;
  final String? semanticLabel;
  final bool isEmergency;
  final int? badgeCount;
}

class SeniorShell extends StatelessWidget {
  const SeniorShell({
    super.key,
    required this.greeting,
    required this.actions,
    this.settingsLabel,
    this.onSettings,
  });

  final String greeting;

  /// Maximum five. Asserted, because the constraint is the whole point.
  final List<SeniorAction> actions;

  final String? settingsLabel;
  final VoidCallback? onSettings;

  @override
  Widget build(BuildContext context) {
    assert(
      actions.length <= 5,
      'Senior Mode home allows at most 5 actions. Found ${actions.length}. '
      'See docs/design/accessibility.md §4.',
    );

    final theme = Theme.of(context);

    return Scaffold(
      body: SafeArea(
        child: LayoutBuilder(
          builder: (context, constraints) {
            return SingleChildScrollView(
              padding: EdgeInsets.symmetric(
                horizontal: context.pagePadding,
                vertical: AppSpacing.xl,
              ),
              child: ConstrainedBox(
                // Single column at EVERY breakpoint. A two-pane senior layout
                // increases scanning cost with no benefit.
                constraints: const BoxConstraints(maxWidth: 560),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text(greeting, style: theme.textTheme.displayMedium),
                    const SizedBox(height: AppSpacing.xxl),
                    for (final action in actions) ...[
                      _SeniorActionTile(action: action),
                      const SizedBox(height: AppSpacing.lg),
                    ],
                    if (onSettings != null && settingsLabel != null) ...[
                      const SizedBox(height: AppSpacing.xl),
                      Align(
                        alignment: AlignmentDirectional.center,
                        child: TextButton.icon(
                          onPressed: onSettings,
                          icon: const Icon(Icons.settings_outlined),
                          label: Text(settingsLabel!),
                        ),
                      ),
                    ],
                  ],
                ),
              ),
            );
          },
        ),
      ),
    );
  }
}

class _SeniorActionTile extends StatelessWidget {
  const _SeniorActionTile({required this.action});

  final SeniorAction action;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final semantic = context.appColors;

    final bg = action.isEmergency ? semantic.emergency : colors.primaryContainer;
    final fg =
        action.isEmergency ? semantic.onEmergency : colors.onPrimaryContainer;

    return Semantics(
      button: true,
      label: action.semanticLabel ?? action.label,
      excludeSemantics: true,
      child: Material(
        color: bg,
        borderRadius: AppRadius.card,
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: action.onTap,
          child: ConstrainedBox(
            // 96dp: well above the 64dp Senior Mode minimum. These are the
            // only five things on the screen; they can afford the space.
            constraints: const BoxConstraints(minHeight: 96),
            child: Padding(
              padding: const EdgeInsetsDirectional.symmetric(
                horizontal: AppSpacing.xl,
                vertical: AppSpacing.lg,
              ),
              child: Row(
                children: [
                  Icon(action.icon, size: 40, color: fg),
                  const SizedBox(width: AppSpacing.xl),
                  Expanded(
                    child: Text(
                      action.label,
                      style: theme.textTheme.headlineMedium?.copyWith(color: fg),
                    ),
                  ),
                  if (action.badgeCount != null && action.badgeCount! > 0)
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: AppSpacing.md,
                        vertical: AppSpacing.xs,
                      ),
                      decoration: BoxDecoration(
                        color: fg,
                        borderRadius:
                            BorderRadius.circular(AppRadius.full),
                      ),
                      child: Text(
                        '${action.badgeCount}',
                        style: theme.textTheme.titleMedium?.copyWith(color: bg),
                      ),
                    ),
                  // Directional: this mirrors correctly in RTL.
                  const SizedBox(width: AppSpacing.sm),
                  Icon(Icons.chevron_right_rounded, size: 32, color: fg),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

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
          constraints:
              const BoxConstraints(maxWidth: AppBreakpoints.maxTextWidth),
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
