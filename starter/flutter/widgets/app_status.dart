// lib/shared/widgets/app_status.dart
//
// Status chips and trust badges.
//
// Two rules this file exists to enforce:
//
//  1. Colour is NEVER the only carrier of meaning. Every status renders as
//     colour + icon + word. This survives grayscale, colour blindness and a
//     photocopied screenshot in a funding report.
//
//  2. A trust badge is FACTUAL, never evaluative. It states what was verified,
//     by whom, and when. It never renders a green tick meaning "safe".
//     BR-TRUST-04.

import 'package:flutter/material.dart';

import '../../core/design_system/app_colors.dart';
import '../../core/design_system/app_tokens.dart';

// ---------------------------------------------------------------------------
// Status chip
// ---------------------------------------------------------------------------

enum AppStatusTone { neutral, info, pending, success, error, emergency }

class AppStatusChip extends StatelessWidget {
  const AppStatusChip({
    super.key,
    required this.label,
    required this.tone,
    this.icon,
    this.dense = false,
  });

  /// Already localised.
  final String label;
  final AppStatusTone tone;

  /// Defaults per tone. Override only when the domain has a clearer icon.
  final IconData? icon;

  final bool dense;

  /// Maps a help-request or activity status code to a chip.
  /// The CODE is the business value; the label is presentation. Never branch
  /// on translated text. BR: mobile/AGENTS.md, localization.
  factory AppStatusChip.forStatusCode({
    Key? key,
    required String code,
    required String label,
    bool dense = false,
  }) {
    final tone = switch (code) {
      'draft' => AppStatusTone.neutral,
      'open' || 'matching' => AppStatusTone.pending,
      'offered' => AppStatusTone.info,
      'assigned' || 'confirmed' => AppStatusTone.success,
      'in_progress' => AppStatusTone.info,
      'completed' => AppStatusTone.neutral,
      'cancelled' || 'no_show' || 'disputed' => AppStatusTone.error,
      'expired' => AppStatusTone.neutral,
      _ => AppStatusTone.neutral,
    };
    return AppStatusChip(key: key, label: label, tone: tone, dense: dense);
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final semantic = context.appColors;
    final senior = context.isSeniorMode;

    final (Color bg, Color fg, IconData defaultIcon) = switch (tone) {
      AppStatusTone.neutral => (
          colors.surfaceContainerHighest,
          colors.onSurfaceVariant,
          Icons.remove_rounded,
        ),
      AppStatusTone.info => (
          colors.primaryContainer,
          colors.onPrimaryContainer,
          Icons.info_outline_rounded,
        ),
      AppStatusTone.pending => (
          semantic.warningContainer,
          semantic.onWarningContainer,
          Icons.hourglass_empty_rounded,
        ),
      AppStatusTone.success => (
          semantic.successContainer,
          semantic.onSuccessContainer,
          Icons.check_rounded,
        ),
      AppStatusTone.error => (
          colors.errorContainer,
          colors.onErrorContainer,
          Icons.close_rounded,
        ),
      AppStatusTone.emergency => (
          semantic.emergency,
          semantic.onEmergency,
          Icons.emergency_rounded,
        ),
    };

    final iconSize = senior ? 20.0 : (dense ? 14.0 : 16.0);

    return Semantics(
      // Announce the meaning, not the decoration.
      label: label,
      excludeSemantics: true,
      child: Container(
        padding: EdgeInsetsDirectional.symmetric(
          horizontal: dense ? AppSpacing.sm : AppSpacing.md,
          vertical: dense ? AppSpacing.xs : AppSpacing.sm,
        ),
        decoration: BoxDecoration(
          color: bg,
          borderRadius: AppRadius.chip,
          // Outline as well as fill: at high contrast the fill is removed.
          border: Border.all(color: fg.withValues(alpha: 0.25)),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon ?? defaultIcon, size: iconSize, color: fg),
            SizedBox(width: dense ? AppSpacing.xs : AppSpacing.sm),
            Flexible(
              child: Text(
                label,
                style: (dense
                        ? theme.textTheme.labelSmall
                        : theme.textTheme.labelMedium)
                    ?.copyWith(color: fg),
                maxLines: 2,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Trust badge
// ---------------------------------------------------------------------------

enum TrustBadgeType {
  identity,
  phone,
  address,
  organization,
  training,
  backgroundCheck,
  drivingLicence,
}

/// A factual statement about what was verified. Never an assessment of a person.
///
/// Tapping it opens a plain-language explanation of *what was checked and by
/// whom* — never the underlying document and never personal data. BR-TRUST-04/05.
class TrustBadge extends StatelessWidget {
  const TrustBadge({
    super.key,
    required this.type,
    required this.label,
    required this.explanation,
    this.verifiedOnLabel,
    this.isExpired = false,
  });

  final TrustBadgeType type;

  /// e.g. "Identität bestätigt", "Von Caritas Tirol bestätigt"
  final String label;

  /// Plain-language: what was checked, by whom. Shown on tap.
  final String explanation;

  /// e.g. "Bestätigt am 12.05.2026"
  final String? verifiedOnLabel;

  final bool isExpired;

  IconData get _icon => switch (type) {
        TrustBadgeType.identity => Icons.badge_outlined,
        TrustBadgeType.phone => Icons.phone_outlined,
        TrustBadgeType.address => Icons.home_outlined,
        TrustBadgeType.organization => Icons.apartment_outlined,
        TrustBadgeType.training => Icons.school_outlined,
        TrustBadgeType.backgroundCheck => Icons.gavel_outlined,
        TrustBadgeType.drivingLicence => Icons.directions_car_outlined,
      };

  Color _accent(BuildContext context) {
    final c = context.appColors;
    return switch (type) {
      TrustBadgeType.identity => c.trustIdentity,
      TrustBadgeType.phone => c.trustPhone,
      TrustBadgeType.address => c.trustAddress,
      TrustBadgeType.organization => c.trustOrganization,
      TrustBadgeType.training => c.trustTraining,
      TrustBadgeType.backgroundCheck => c.trustBackgroundCheck,
      TrustBadgeType.drivingLicence => c.trustPhone,
    };
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final senior = context.isSeniorMode;
    final accent = isExpired ? colors.onSurfaceVariant : _accent(context);

    return Semantics(
      button: true,
      label: [
        label,
        if (verifiedOnLabel != null) verifiedOnLabel!,
        if (isExpired) 'abgelaufen',
      ].join('. '),
      excludeSemantics: true,
      child: InkWell(
        borderRadius: AppRadius.chip,
        onTap: () => _showExplanation(context),
        child: ConstrainedBox(
          constraints: BoxConstraints(minHeight: AppTouch.min(senior)),
          child: Padding(
            padding: const EdgeInsetsDirectional.symmetric(
              horizontal: AppSpacing.md,
              vertical: AppSpacing.sm,
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(_icon, size: senior ? 26 : 20, color: accent),
                const SizedBox(width: AppSpacing.sm),
                Flexible(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        label,
                        style: theme.textTheme.labelLarge?.copyWith(
                          color: colors.onSurface,
                          decoration:
                              isExpired ? TextDecoration.lineThrough : null,
                        ),
                      ),
                      if (verifiedOnLabel != null)
                        Text(
                          verifiedOnLabel!,
                          style: theme.textTheme.labelSmall
                              ?.copyWith(color: colors.onSurfaceVariant),
                        ),
                    ],
                  ),
                ),
                const SizedBox(width: AppSpacing.xs),
                Icon(
                  Icons.info_outline_rounded,
                  size: senior ? 22 : 16,
                  color: colors.onSurfaceVariant,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  void _showExplanation(BuildContext context) {
    showModalBottomSheet<void>(
      context: context,
      builder: (sheetContext) => Padding(
        padding: EdgeInsetsDirectional.fromSTEB(
          sheetContext.pagePadding,
          AppSpacing.lg,
          sheetContext.pagePadding,
          AppSpacing.xl + MediaQuery.viewPaddingOf(sheetContext).bottom,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(_icon, color: _accent(sheetContext)),
                const SizedBox(width: AppSpacing.md),
                Expanded(
                  child: Text(
                    label,
                    style: Theme.of(sheetContext).textTheme.headlineSmall,
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.lg),
            Text(
              explanation,
              style: Theme.of(sheetContext).textTheme.bodyLarge,
            ),
            if (verifiedOnLabel != null) ...[
              const SizedBox(height: AppSpacing.md),
              Text(
                verifiedOnLabel!,
                style: Theme.of(sheetContext).textTheme.bodyMedium?.copyWith(
                      color: Theme.of(sheetContext).colorScheme.onSurfaceVariant,
                    ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
