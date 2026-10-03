// lib/shared/widgets/app_widgets.dart
//
// The shared widget set. Every widget here obeys, without exception:
//
//   · colours come from the theme — never Colors.* and never Color(0xFF…)
//   · sizes come from AppSpacing / AppRadius / AppTouch
//   · padding is DIRECTIONAL — EdgeInsetsDirectional, never left/right
//   · touch targets are 48dp, or 64dp in Senior Mode
//   · every interactive element carries a Semantics label describing the ACTION
//   · status is never conveyed by colour alone — always colour + icon + word
//   · text is never passed in pre-translated; callers pass a key and .tr() here
//     would hide the key from the linter, so callers pass the RESOLVED string
//     and the lint rule checks the call site
//
// Split this file per widget when it grows. It is one file here so the whole
// set can be read in one sitting.

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../core/design_system/app_colors.dart';
import '../../core/design_system/app_tokens.dart';

// =============================================================================
// Buttons
// =============================================================================

enum AppButtonVariant {
  /// The single main action of a screen.
  primary,

  /// Secondary actions.
  tonal,

  /// Tertiary, cancel.
  outlined,

  /// Inline links only. Never the main action of a screen.
  text,

  /// Emergency screen ONLY. Requires a two-step confirm at the call site.
  emergency,
}

class AppButton extends StatelessWidget {
  const AppButton({
    super.key,
    required this.label,
    required this.onPressed,
    this.semanticLabel,
    this.icon,
    this.variant = AppButtonVariant.primary,
    this.expand = false,
    this.isLoading = false,
  });

  /// Already-localised label. In Senior Mode this must be a VERB
  /// ("Hilfe anfragen"), never a bare noun ("Anfrage").
  final String label;

  /// Describes the action and its consequence, e.g.
  /// "Hilfe anfragen. Öffnet das Formular."
  /// Falls back to [label] — acceptable, but explicit is better.
  final String? semanticLabel;

  final IconData? icon;
  final VoidCallback? onPressed;
  final AppButtonVariant variant;
  final bool expand;
  final bool isLoading;

  @override
  Widget build(BuildContext context) {
    final senior = context.isSeniorMode;
    final scheme = Theme.of(context).colorScheme;
    final app = context.appColors;
    final height = AppTouch.buttonHeight(senior);

    final effectiveOnPressed = isLoading ? null : onPressed;

    // Senior Mode always pairs an icon with its label, and never shows an icon
    // alone. In standard mode an icon is optional decoration.
    final child = isLoading
        ? SizedBox(
            height: senior ? 28 : 22,
            width: senior ? 28 : 22,
            child: const CircularProgressIndicator(strokeWidth: 2.5),
          )
        : Row(
            mainAxisSize: expand ? MainAxisSize.max : MainAxisSize.min,
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              if (icon != null) ...[
                Icon(icon, size: senior ? 28 : 20),
                const SizedBox(width: AppSpacing.md),
              ],
              Flexible(
                child: Text(
                  label,
                  textAlign: TextAlign.center,
                  // Never truncate a button label. If it does not fit, the
                  // layout is wrong — let it wrap.
                  softWrap: true,
                ),
              ),
            ],
          );

    final button = switch (variant) {
      AppButtonVariant.primary => FilledButton(
          onPressed: effectiveOnPressed,
          style: FilledButton.styleFrom(minimumSize: Size.fromHeight(height)),
          child: child,
        ),
      AppButtonVariant.tonal => FilledButton.tonal(
          onPressed: effectiveOnPressed,
          style: FilledButton.styleFrom(minimumSize: Size.fromHeight(height)),
          child: child,
        ),
      AppButtonVariant.outlined => OutlinedButton(
          onPressed: effectiveOnPressed,
          style: OutlinedButton.styleFrom(minimumSize: Size.fromHeight(height)),
          child: child,
        ),
      AppButtonVariant.text => TextButton(
          onPressed: effectiveOnPressed,
          child: child,
        ),
      AppButtonVariant.emergency => FilledButton(
          onPressed: effectiveOnPressed,
          style: FilledButton.styleFrom(
            backgroundColor: app.emergency,
            foregroundColor: app.onEmergency,
            minimumSize: Size.fromHeight(senior ? 88 : 64),
          ),
          child: child,
        ),
    };

    return Semantics(
      button: true,
      enabled: effectiveOnPressed != null,
      label: semanticLabel ?? label,
      excludeSemantics: true,
      child: SizedBox(
        width: expand || senior ? double.infinity : null,
        child: button,
      ),
    );
  }
}

// =============================================================================
// Text field
//
// Labels ALWAYS sit above the field. A placeholder-only label is a documented
// accessibility defect in this product — it disappears the moment the user
// starts typing, which is exactly when they need it.
// =============================================================================

class AppTextField extends StatelessWidget {
  const AppTextField({
    super.key,
    required this.label,
    this.controller,
    this.hint,
    this.helper,
    this.errorText,
    this.keyboardType,
    this.textInputAction,
    this.obscureText = false,
    this.maxLines = 1,
    this.maxLength,
    this.autofillHints,
    this.onChanged,
    this.enabled = true,
  });

  final String label;
  final TextEditingController? controller;
  final String? hint;
  final String? helper;
  final String? errorText;
  final TextInputType? keyboardType;
  final TextInputAction? textInputAction;
  final bool obscureText;
  final int maxLines;
  final int? maxLength;
  final Iterable<String>? autofillHints;
  final ValueChanged<String>? onChanged;
  final bool enabled;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final hasError = errorText != null;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: theme.textTheme.titleSmall),
        const SizedBox(height: AppSpacing.sm),
        TextField(
          controller: controller,
          enabled: enabled,
          keyboardType: keyboardType,
          textInputAction: textInputAction,
          obscureText: obscureText,
          maxLines: obscureText ? 1 : maxLines,
          maxLength: maxLength,
          autofillHints: autofillHints,
          onChanged: onChanged,
          style: theme.textTheme.bodyLarge,
          decoration: InputDecoration(
            hintText: hint,
            // The label lives above the field, not inside it.
            labelText: null,
            errorText: null, // rendered below, with an icon
          ),
        ),
        if (hasError) ...[
          const SizedBox(height: AppSpacing.sm),
          // Colour is never the only signal — icon + colour + words.
          Semantics(
            liveRegion: true,
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Icon(Icons.error_outline, size: 20, color: scheme.error),
                const SizedBox(width: AppSpacing.sm),
                Expanded(
                  child: Text(
                    errorText!,
                    style: theme.textTheme.bodyMedium
                        ?.copyWith(color: scheme.error),
                  ),
                ),
              ],
            ),
          ),
        ] else if (helper != null) ...[
          const SizedBox(height: AppSpacing.sm),
          Text(
            helper!,
            style: theme.textTheme.bodySmall
                ?.copyWith(color: scheme.onSurfaceVariant),
          ),
        ],
      ],
    );
  }
}

// =============================================================================
// Status chip — colour + icon + word, always all three
// =============================================================================

enum AppStatusTone { neutral, info, pending, success, error }

class AppStatusChip extends StatelessWidget {
  const AppStatusChip({
    super.key,
    required this.label,
    required this.tone,
  });

  final String label;
  final AppStatusTone tone;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final app = context.appColors;

    final (Color fg, Color bg, IconData icon) = switch (tone) {
      AppStatusTone.neutral => (
          scheme.onSurfaceVariant,
          scheme.surfaceContainerHighest,
          Icons.remove,
        ),
      AppStatusTone.info => (
          scheme.onPrimaryContainer,
          scheme.primaryContainer,
          Icons.info_outline,
        ),
      AppStatusTone.pending => (
          app.onWarningContainer,
          app.warningContainer,
          Icons.hourglass_empty,
        ),
      AppStatusTone.success => (
          app.onSuccessContainer,
          app.successContainer,
          Icons.check,
        ),
      AppStatusTone.error => (
          scheme.onErrorContainer,
          scheme.errorContainer,
          Icons.close,
        ),
    };

    return MergeSemantics(
      child: Container(
        padding: const EdgeInsetsDirectional.symmetric(
          horizontal: AppSpacing.md,
          vertical: AppSpacing.sm,
        ),
        decoration: BoxDecoration(
          color: bg,
          borderRadius: AppRadius.chip,
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            // Decorative: the word next to it carries the meaning.
            ExcludeSemantics(child: Icon(icon, size: 18, color: fg)),
            const SizedBox(width: AppSpacing.xs),
            Text(
              label,
              style: Theme.of(context)
                  .textTheme
                  .labelMedium
                  ?.copyWith(color: fg),
            ),
          ],
        ),
      ),
    );
  }
}

// =============================================================================
// Trust badge — FACTUAL, never evaluative.
//
// Never renders a green tick meaning "safe". States what was verified, by whom,
// and when. Tapping opens a plain-language explanation, never the document.
// (BR-TRUST-04)
// =============================================================================

enum TrustBadgeKind {
  identity,
  phone,
  address,
  organization,
  training,
  backgroundCheck,
  drivingLicence,
}

class TrustBadge extends StatelessWidget {
  const TrustBadge({
    super.key,
    required this.kind,
    required this.label,
    required this.verifiedOn,
    this.onTap,
  });

  final TrustBadgeKind kind;

  /// e.g. "Identität bestätigt", "Von Caritas Tirol bestätigt".
  /// Never "Verified — safe".
  final String label;

  /// Already-formatted, e.g. "Bestätigt am 12.05.2026".
  final String verifiedOn;

  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final app = context.appColors;
    final theme = Theme.of(context);
    final senior = context.isSeniorMode;

    final (Color accent, IconData icon) = switch (kind) {
      TrustBadgeKind.identity => (app.trustIdentity, Icons.badge_outlined),
      TrustBadgeKind.phone => (app.trustPhone, Icons.phone_outlined),
      TrustBadgeKind.address => (app.trustAddress, Icons.home_outlined),
      TrustBadgeKind.organization =>
        (app.trustOrganization, Icons.apartment_outlined),
      TrustBadgeKind.training => (app.trustTraining, Icons.school_outlined),
      TrustBadgeKind.backgroundCheck =>
        (app.trustBackgroundCheck, Icons.verified_user_outlined),
      TrustBadgeKind.drivingLicence =>
        (app.trustOrganization, Icons.directions_car_outlined),
    };

    return Semantics(
      button: onTap != null,
      label: '$label. $verifiedOn.',
      excludeSemantics: true,
      child: InkWell(
        onTap: onTap,
        borderRadius: AppRadius.card,
        child: ConstrainedBox(
          constraints: BoxConstraints(minHeight: context.minTouchTarget),
          child: Padding(
            padding: const EdgeInsetsDirectional.all(AppSpacing.md),
            child: Row(
              children: [
                Icon(icon, size: senior ? 32 : 24, color: accent),
                const SizedBox(width: AppSpacing.md),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(label, style: theme.textTheme.titleSmall),
                      Text(
                        verifiedOn,
                        style: theme.textTheme.bodySmall?.copyWith(
                          color: theme.colorScheme.onSurfaceVariant,
                        ),
                      ),
                    ],
                  ),
                ),
                if (onTap != null)
                  // Mirrors correctly in RTL because it is a directional icon.
                  Icon(
                    Icons.chevron_right,
                    color: theme.colorScheme.onSurfaceVariant,
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

// =============================================================================
// State views — loading, empty, error.
//
// Every list has all three. "Keine Daten" is a bug, not an empty state.
// =============================================================================

class AppLoading extends StatelessWidget {
  const AppLoading({super.key, required this.message});

  /// Never an unexplained spinner. Say what is happening.
  final String message;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      liveRegion: true,
      label: message,
      child: Center(
        child: Padding(
          padding: const EdgeInsetsDirectional.all(AppSpacing.xl),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const CircularProgressIndicator(),
              const SizedBox(height: AppSpacing.lg),
              Text(message, style: Theme.of(context).textTheme.bodyLarge),
            ],
          ),
        ),
      ),
    );
  }
}

class AppEmptyState extends StatelessWidget {
  const AppEmptyState({
    super.key,
    required this.icon,
    required this.message,
    this.actionLabel,
    this.onAction,
  });

  final IconData icon;

  /// One sentence, plain language.
  final String message;
  final String? actionLabel;
  final VoidCallback? onAction;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;

    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: AppBreakpoints.maxTextWidth),
        child: Padding(
          padding: const EdgeInsetsDirectional.all(AppSpacing.xl),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              ExcludeSemantics(
                child: Icon(icon, size: 64, color: scheme.onSurfaceVariant),
              ),
              const SizedBox(height: AppSpacing.lg),
              Text(
                message,
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.bodyLarge,
              ),
              if (actionLabel != null && onAction != null) ...[
                const SizedBox(height: AppSpacing.xl),
                AppButton(
                  label: actionLabel!,
                  onPressed: onAction,
                  variant: AppButtonVariant.tonal,
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class AppErrorView extends StatelessWidget {
  const AppErrorView({
    super.key,
    required this.message,
    required this.retryLabel,
    this.onRetry,
  });

  /// Must say what to do next, not what went wrong internally.
  /// "Das hat nicht geklappt. Bitte versuchen Sie es nochmal."
  final String message;
  final String retryLabel;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;

    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: AppBreakpoints.maxTextWidth),
        child: Padding(
          padding: const EdgeInsetsDirectional.all(AppSpacing.xl),
          child: Semantics(
            liveRegion: true,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(Icons.error_outline, size: 56, color: scheme.error),
                const SizedBox(height: AppSpacing.lg),
                Text(
                  message,
                  textAlign: TextAlign.center,
                  style: Theme.of(context).textTheme.bodyLarge,
                ),
                if (onRetry != null) ...[
                  const SizedBox(height: AppSpacing.xl),
                  AppButton(
                    label: retryLabel,
                    onPressed: onRetry,
                    icon: Icons.refresh,
                    variant: AppButtonVariant.tonal,
                  ),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}

// =============================================================================
// Confirmation sheet
//
// Senior Mode requires a confirmation for EVERY consequential action, not only
// destructive ones. Returns true only on explicit confirmation.
// =============================================================================

class AppConfirmSheet {
  static Future<bool> show(
    BuildContext context, {
    required String title,
    required String message,
    required String confirmLabel,
    required String cancelLabel,
    bool isDestructive = false,
  }) async {
    HapticFeedback.mediumImpact();

    final result = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      // Tapping outside is a gesture. Seniors get an explicit cancel button,
      // and dismissal must never be mistaken for confirmation.
      isDismissible: true,
      builder: (context) {
        final theme = Theme.of(context);
        final scheme = theme.colorScheme;

        return SafeArea(
          child: Padding(
            padding: EdgeInsetsDirectional.only(
              start: context.pagePadding,
              end: context.pagePadding,
              top: AppSpacing.lg,
              bottom: AppSpacing.xl +
                  MediaQuery.viewInsetsOf(context).bottom,
            ),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(title, style: theme.textTheme.headlineSmall),
                const SizedBox(height: AppSpacing.md),
                Text(message, style: theme.textTheme.bodyLarge),
                const SizedBox(height: AppSpacing.xl),
                AppButton(
                  label: confirmLabel,
                  expand: true,
                  variant: isDestructive
                      ? AppButtonVariant.emergency
                      : AppButtonVariant.primary,
                  onPressed: () => Navigator.of(context).pop(true),
                ),
                const SizedBox(height: AppSpacing.md),
                AppButton(
                  label: cancelLabel,
                  expand: true,
                  variant: AppButtonVariant.outlined,
                  onPressed: () => Navigator.of(context).pop(false),
                ),
              ],
            ),
          ),
        );
      },
    );

    return result ?? false;
  }
}
