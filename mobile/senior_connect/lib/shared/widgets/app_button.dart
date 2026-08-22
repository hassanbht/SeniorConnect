// lib/shared/widgets/app_button.dart
//
// The only button in the app. Every screen uses a variant of this — nothing
// constructs a raw FilledButton or ElevatedButton.
//
// Guarantees this widget provides so no screen has to remember them:
//   - minimum touch target from AppTouch, scaled for Senior Mode
//   - Senior Mode buttons are always full width with a visible label
//   - no icon-only buttons in Senior Mode (asserted in debug)
//   - a semantic label describing the ACTION, not the widget
//   - loading state that stays the same size, so nothing jumps
//   - double-tap protection (a senior tapping twice must not submit twice)
//   - the emergency variant requires a two-step confirmation

import 'package:flutter/material.dart';

import '../../core/design_system/app_colors.dart';
import '../../core/design_system/app_tokens.dart';

enum AppButtonVariant {
  /// The one main action of the screen.
  primary,

  /// Secondary actions.
  tonal,

  /// Tertiary, cancel.
  outlined,

  /// Inline links only. Never the main action of a screen.
  text,

  /// Destructive. Always confirms first.
  destructive,

  /// SOS only. Never for validation errors. Two-step confirm is mandatory.
  emergency,
}

class AppButton extends StatefulWidget {
  const AppButton({
    super.key,
    required this.label,
    required this.onPressed,
    this.variant = AppButtonVariant.primary,
    this.icon,
    this.semanticLabel,
    this.isLoading = false,
    this.fullWidth,
    this.confirmationText,
  });

  /// Already localised. A raw literal here is a defect — see mobile/AGENTS.md.
  final String label;

  /// Null disables the button. The screen must then explain why nearby.
  final VoidCallback? onPressed;

  final AppButtonVariant variant;
  final IconData? icon;

  /// Describes the ACTION and its outcome:
  /// "Hilfe anfragen. Öffnet das Formular." — not just "Hilfe anfragen".
  final String? semanticLabel;

  final bool isLoading;

  /// Defaults to true in Senior Mode, false otherwise.
  final bool? fullWidth;

  /// Required for [AppButtonVariant.destructive] and [AppButtonVariant.emergency].
  final String? confirmationText;

  @override
  State<AppButton> createState() => _AppButtonState();
}

class _AppButtonState extends State<AppButton> {
  bool _busy = false;

  Future<void> _handleTap() async {
    // Double-tap protection. A senior tapping twice must not submit twice.
    if (_busy || widget.isLoading || widget.onPressed == null) return;

    final needsConfirm = widget.variant == AppButtonVariant.destructive ||
        widget.variant == AppButtonVariant.emergency;

    if (needsConfirm) {
      final confirmed = await _confirm(context);
      if (!confirmed || !mounted) return;
    }

    setState(() => _busy = true);
    try {
      widget.onPressed!();
    } finally {
      if (mounted) {
        await Future<void>.delayed(const Duration(milliseconds: 400));
        if (mounted) setState(() => _busy = false);
      }
    }
  }

  Future<bool> _confirm(BuildContext context) async {
    final theme = Theme.of(context);
    final result = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      isDismissible: true,
      builder: (sheetContext) {
        final senior = sheetContext.isSeniorMode;
        return SingleChildScrollView(
          child: Padding(
            padding: EdgeInsetsDirectional.fromSTEB(
              sheetContext.pagePadding,
              AppSpacing.lg,
              sheetContext.pagePadding,
              AppSpacing.xl + MediaQuery.viewPaddingOf(sheetContext).bottom,
            ),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  widget.confirmationText ?? widget.label,
                  style: theme.textTheme.headlineSmall,
                  textAlign: TextAlign.start,
                ),
                SizedBox(height: senior ? AppSpacing.xl : AppSpacing.lg),
                AppButton(
                  label: widget.label,
                  variant: widget.variant == AppButtonVariant.emergency
                      ? AppButtonVariant.emergency
                      : AppButtonVariant.destructive,
                  confirmationText: widget.confirmationText ?? widget.label, // already confirming
                  onPressed: () => Navigator.of(sheetContext).pop(true),
                ),
                SizedBox(height: AppSpacing.md),
                AppButton(
                  // The cancel option is deliberately the outlined one, not a
                  // text button — it must be as easy to hit as the destructive one.
                  label: MaterialLocalizations.of(sheetContext).cancelButtonLabel,
                  variant: AppButtonVariant.outlined,
                  onPressed: () => Navigator.of(sheetContext).pop(false),
                ),
              ],
            ),
          ),
        );
      },
    );
    return result ?? false;
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final semantic = context.appColors;
    final senior = context.isSeniorMode;

    assert(
      !(senior && widget.label.isEmpty),
      'Senior Mode buttons must always carry a visible text label. '
      'Icon-only actions are a documented accessibility defect.',
    );
    assert(
      !((widget.variant == AppButtonVariant.destructive ||
              widget.variant == AppButtonVariant.emergency) &&
          widget.confirmationText == null &&
          widget.onPressed != null),
      'Destructive and emergency buttons require confirmationText.',
    );

    final loading = widget.isLoading || _busy;
    final enabled = widget.onPressed != null && !loading;
    final expand = widget.fullWidth ?? senior;
    final height = AppTouch.buttonHeight(senior);

    final (Color bg, Color fg, BorderSide? side) = switch (widget.variant) {
      AppButtonVariant.primary => (colors.primary, colors.onPrimary, null),
      AppButtonVariant.tonal => (
          colors.secondaryContainer,
          colors.onSecondaryContainer,
          null
        ),
      AppButtonVariant.outlined => (
          Colors.transparent,
          colors.primary,
          BorderSide(color: colors.outline, width: 1.5)
        ),
      AppButtonVariant.text => (Colors.transparent, colors.primary, null),
      AppButtonVariant.destructive => (colors.error, colors.onError, null),
      AppButtonVariant.emergency => (semantic.emergency, semantic.onEmergency, null),
    };

    final child = loading
        ? SizedBox(
            height: senior ? 28 : 22,
            width: senior ? 28 : 22,
            child: CircularProgressIndicator(strokeWidth: 2.5, color: fg),
          )
        : Row(
            mainAxisSize: expand ? MainAxisSize.max : MainAxisSize.min,
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              if (widget.icon != null) ...[
                Icon(widget.icon, size: senior ? 28 : 22, color: fg),
                SizedBox(width: senior ? AppSpacing.md : AppSpacing.sm),
              ],
              Flexible(
                child: Text(
                  widget.label,
                  style: theme.textTheme.labelLarge?.copyWith(color: fg),
                  textAlign: TextAlign.center,
                  // Text must wrap rather than ellipsise: at 200% scale a
                  // truncated button label is unreadable.
                  maxLines: 2,
                  overflow: TextOverflow.visible,
                ),
              ),
            ],
          );

    final button = Semantics(
      button: true,
      enabled: enabled,
      label: widget.semanticLabel ?? widget.label,
      // The visual label is already announced via `label`; do not announce twice.
      excludeSemantics: true,
      child: Material(
        color: enabled
            ? bg
            : Color.alphaBlend(colors.surface.withValues(alpha: 0.6), bg),
        shape: RoundedRectangleBorder(
          borderRadius: AppRadius.field,
          side: side ?? BorderSide.none,
        ),
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: enabled ? _handleTap : null,
          child: ConstrainedBox(
            constraints: BoxConstraints(
              minHeight: height,
              minWidth: AppTouch.min(senior),
            ),
            child: Padding(
              padding: EdgeInsetsDirectional.symmetric(
                horizontal: senior ? AppSpacing.xl : AppSpacing.lg,
                vertical: AppSpacing.md,
              ),
              child: Center(child: child),
            ),
          ),
        ),
      ),
    );

    return expand ? SizedBox(width: double.infinity, child: button) : button;
  }
}
