// lib/shared/senior/senior_shell.dart
//
// The Senior Mode shell. This is a SEPARATE navigation structure, not the
// standard shell with things hidden. Per docs/architecture/system-design.md §3:
//
//   if (seniorMode) → SeniorShell    max 5 full-width actions, no tabs
//   else            → StandardShell  bottom navigation, standard density
//
// Constraints enforced here, from docs/design/accessibility.md §4:
//   · maximum 5 actions on the home screen
//   · full width, one per row, 72dp+ tall
//   · icon ALWAYS paired with a text label
//   · no tabs, no drawer, no carousel, no gesture-only actions
//   · single column at every breakpoint — no two-pane tablet layout
//   · emergency is visually separated and never adjacent to a normal action

import 'package:flutter/material.dart';

import '../../core/design_system/app_tokens.dart';
import '../widgets/app_widgets.dart';

/// One action on the Senior Mode home screen.
class SeniorAction {
  const SeniorAction({
    required this.icon,
    required this.label,
    required this.semanticLabel,
    required this.onTap,
    this.badgeCount,
  });

  final IconData icon;

  /// A VERB, always. "Hilfe anfragen", not "Anfrage".
  final String label;

  /// "Hilfe anfragen. Öffnet das Formular."
  final String semanticLabel;

  final VoidCallback onTap;

  /// e.g. 2 upcoming appointments. Rendered as a number AND announced in words.
  final int? badgeCount;
}

class SeniorHome extends StatelessWidget {
  const SeniorHome({
    super.key,
    required this.greeting,
    required this.actions,
    required this.emergencyLabel,
    required this.emergencySemanticLabel,
    required this.onEmergency,
  });

  /// "Hallo Maria" — already localised and interpolated.
  final String greeting;

  /// Maximum 5. Asserted in debug.
  final List<SeniorAction> actions;

  final String emergencyLabel;
  final String emergencySemanticLabel;
  final VoidCallback onEmergency;

  @override
  Widget build(BuildContext context) {
    assert(
      actions.length <= 5,
      'Senior Mode allows at most 5 home-screen actions '
      '(docs/design/accessibility.md §4). Got ${actions.length}.',
    );

    final theme = Theme.of(context);

    return Scaffold(
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            // Single column at EVERY breakpoint. On a tablet the column simply
            // centres — it does not become two panes.
            constraints: const BoxConstraints(maxWidth: 560),
            child: ListView(
              padding: EdgeInsetsDirectional.symmetric(
                horizontal: context.pagePadding,
                vertical: AppSpacing.xl,
              ),
              children: [
                Text(greeting, style: theme.textTheme.displayMedium),
                const SizedBox(height: AppSpacing.xxl),

                for (final action in actions) ...[
                  _SeniorActionTile(action: action),
                  const SizedBox(height: AppSpacing.lg),
                ],

                // Emergency is deliberately separated by a divider and extra
                // space so it can never be tapped by accident while reaching
                // for the action above it.
                const SizedBox(height: AppSpacing.xl),
                Divider(color: theme.colorScheme.outlineVariant),
                const SizedBox(height: AppSpacing.xl),

                AppButton(
                  label: emergencyLabel,
                  semanticLabel: emergencySemanticLabel,
                  icon: Icons.emergency_outlined,
                  variant: AppButtonVariant.emergency,
                  expand: true,
                  onPressed: onEmergency,
                ),
                const SizedBox(height: AppSpacing.xl),
              ],
            ),
          ),
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
    final scheme = theme.colorScheme;

    final badge = action.badgeCount;

    return Semantics(
      button: true,
      label: badge != null && badge > 0
          // Announce the count in words as part of the label, because a screen
          // reader user cannot see the badge.
          ? '${action.semanticLabel} $badge neu.'
          : action.semanticLabel,
      excludeSemantics: true,
      child: Material(
        color: scheme.surfaceContainer,
        borderRadius: AppRadius.card,
        child: InkWell(
          onTap: action.onTap,
          borderRadius: AppRadius.card,
          child: ConstrainedBox(
            // 72dp minimum, not the 64dp general Senior Mode target: this is
            // the primary action surface of the whole app.
            constraints: const BoxConstraints(minHeight: 88),
            child: Padding(
              padding: const EdgeInsetsDirectional.symmetric(
                horizontal: AppSpacing.xl,
                vertical: AppSpacing.lg,
              ),
              child: Row(
                children: [
                  Icon(action.icon, size: 40, color: scheme.primary),
                  const SizedBox(width: AppSpacing.xl),
                  Expanded(
                    child: Text(
                      action.label,
                      style: theme.textTheme.headlineSmall,
                      // Wraps rather than truncates at 200% text scale.
                      softWrap: true,
                    ),
                  ),
                  if (badge != null && badge > 0) ...[
                    const SizedBox(width: AppSpacing.md),
                    _CountBadge(count: badge),
                  ],
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _CountBadge extends StatelessWidget {
  const _CountBadge({required this.count});

  final int count;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;

    // Excluded from semantics — the count is already in the parent's label.
    return ExcludeSemantics(
      child: Container(
        constraints: const BoxConstraints(minWidth: 36, minHeight: 36),
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: scheme.primary,
          borderRadius: BorderRadius.circular(AppRadius.full),
        ),
        child: Padding(
          padding: const EdgeInsetsDirectional.symmetric(
            horizontal: AppSpacing.sm,
          ),
          child: Text(
            '$count',
            style: Theme.of(context)
                .textTheme
                .titleMedium
                ?.copyWith(color: scheme.onPrimary),
          ),
        ),
      ),
    );
  }
}

// =============================================================================
// A scaffold for inner Senior Mode screens.
//
// One screen, one question, one primary action. Always an explicit Back button
// — never a swipe-only back gesture.
// =============================================================================

class SeniorScaffold extends StatelessWidget {
  const SeniorScaffold({
    super.key,
    required this.title,
    required this.child,
    required this.backLabel,
    this.primaryAction,
    this.onBack,
  });

  final String title;
  final Widget child;

  /// "Zurück zur vorherigen Seite."
  final String backLabel;

  /// The single main action of this screen, pinned to the bottom.
  final Widget? primaryAction;

  final VoidCallback? onBack;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        // Explicit, labelled, and it mirrors correctly in RTL because
        // arrow_back is a directional icon.
        leading: Semantics(
          button: true,
          label: backLabel,
          excludeSemantics: true,
          child: IconButton(
            icon: const Icon(Icons.arrow_back),
            iconSize: 32,
            onPressed: onBack ?? () => Navigator.of(context).maybePop(),
          ),
        ),
        title: Text(title, style: theme.textTheme.headlineSmall),
        toolbarHeight: 80,
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 560),
            child: Padding(
              padding: EdgeInsetsDirectional.symmetric(
                horizontal: context.pagePadding,
              ),
              child: child,
            ),
          ),
        ),
      ),
      bottomNavigationBar: primaryAction == null
          ? null
          : SafeArea(
              child: Padding(
                padding: EdgeInsetsDirectional.only(
                  start: context.pagePadding,
                  end: context.pagePadding,
                  top: AppSpacing.lg,
                  bottom: AppSpacing.lg,
                ),
                child: primaryAction,
              ),
            ),
    );
  }
}
