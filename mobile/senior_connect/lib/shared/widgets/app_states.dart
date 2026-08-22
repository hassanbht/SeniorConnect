// lib/shared/widgets/app_states.dart
//
// Loading, empty and error states.
//
// Every list and every screen must have all three. "Keine Daten" is a bug.
// AppAsyncView exists so a screen cannot accidentally ship without them —
// it takes all four builders and will not compile if one is missing.

import 'package:flutter/material.dart';
import 'package:flutter/semantics.dart';

import '../../core/design_system/app_tokens.dart';
import 'app_button.dart';

// ---------------------------------------------------------------------------
// Loading
// ---------------------------------------------------------------------------

class AppLoading extends StatefulWidget {
  const AppLoading({super.key, required this.message});

  /// Always labelled. An unexplained spinner is a defect for this audience.
  final String message;

  @override
  State<AppLoading> createState() => _AppLoadingState();
}

class _AppLoadingState extends State<AppLoading> {
  @override
  void initState() {
    super.initState();
    // Screen-reader users get no signal from a spinner.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) {
        SemanticsService.sendAnnouncement(
          View.of(context),
          widget.message,
          Directionality.of(context),
        );
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Semantics(
      liveRegion: true,
      label: widget.message,
      excludeSemantics: true,
      child: Center(
        child: Padding(
          padding: EdgeInsets.all(context.pagePadding),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const CircularProgressIndicator(),
              const SizedBox(height: AppSpacing.lg),
              Text(
                widget.message,
                style: theme.textTheme.bodyLarge,
                textAlign: TextAlign.center,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Empty
// ---------------------------------------------------------------------------

class AppEmptyState extends StatelessWidget {
  const AppEmptyState({
    super.key,
    required this.icon,
    required this.message,
    this.actionLabel,
    this.onAction,
  });

  final IconData icon;

  /// One sentence of plain language. Not "Keine Daten".
  final String message;

  /// An empty state without an action is usually a design gap. Prefer to have one.
  final String? actionLabel;
  final VoidCallback? onAction;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final senior = context.isSeniorMode;

    return Center(
      child: SingleChildScrollView(
        padding: EdgeInsets.all(context.pagePadding),
        child: ConstrainedBox(
          constraints:
              const BoxConstraints(maxWidth: AppBreakpoints.maxTextWidth),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              // Decorative — the message carries the meaning.
              ExcludeSemantics(
                child: Icon(
                  icon,
                  size: senior ? 72 : 56,
                  color: theme.colorScheme.onSurfaceVariant,
                ),
              ),
              SizedBox(height: senior ? AppSpacing.xl : AppSpacing.lg),
              Text(
                message,
                style: theme.textTheme.bodyLarge,
                textAlign: TextAlign.center,
              ),
              if (actionLabel != null && onAction != null) ...[
                SizedBox(height: senior ? AppSpacing.xl : AppSpacing.lg),
                AppButton(
                  label: actionLabel!,
                  variant: AppButtonVariant.tonal,
                  onPressed: onAction,
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Error
// ---------------------------------------------------------------------------

class AppErrorView extends StatelessWidget {
  const AppErrorView({
    super.key,
    required this.message,
    required this.retryLabel,
    this.onRetry,
    this.nextSteps = const <String>[],
  });

  /// Human-readable, and it must say what to do next. Never a stack trace,
  /// never "Error 500", never a raw exception message.
  final String message;

  final String retryLabel;
  final VoidCallback? onRetry;

  /// For a 403 from the API: the `missing[]` array rendered as a checklist.
  /// See docs/architecture/authorization.md §5 — a refusal must be explainable.
  final List<String> nextSteps;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final senior = context.isSeniorMode;

    return Center(
      child: SingleChildScrollView(
        padding: EdgeInsets.all(context.pagePadding),
        child: ConstrainedBox(
          constraints:
              const BoxConstraints(maxWidth: AppBreakpoints.maxTextWidth),
          child: Semantics(
            liveRegion: true,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.center,
              children: [
                Icon(
                  Icons.error_outline_rounded,
                  size: senior ? 64 : 48,
                  color: colors.error,
                ),
                SizedBox(height: senior ? AppSpacing.xl : AppSpacing.lg),
                Text(
                  message,
                  style: theme.textTheme.bodyLarge,
                  textAlign: TextAlign.center,
                ),
                if (nextSteps.isNotEmpty) ...[
                  const SizedBox(height: AppSpacing.lg),
                  ...nextSteps.map(
                    (step) => Padding(
                      padding:
                          const EdgeInsets.only(bottom: AppSpacing.sm),
                      child: Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Icon(
                            Icons.radio_button_unchecked_rounded,
                            size: senior ? 24 : 18,
                            color: colors.onSurfaceVariant,
                          ),
                          const SizedBox(width: AppSpacing.md),
                          Expanded(
                            child: Text(step, style: theme.textTheme.bodyMedium),
                          ),
                        ],
                      ),
                    ),
                  ),
                ],
                if (onRetry != null) ...[
                  SizedBox(height: senior ? AppSpacing.xl : AppSpacing.lg),
                  AppButton(
                    label: retryLabel,
                    variant: AppButtonVariant.tonal,
                    icon: Icons.refresh_rounded,
                    onPressed: onRetry,
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

// ---------------------------------------------------------------------------
// AppAsyncView — the five-state switcher
// ---------------------------------------------------------------------------

/// Forces a screen to handle every state. All builders are required, so
/// "I forgot the empty state" is a compile error rather than a bug report.
class AppAsyncView<T> extends StatelessWidget {
  const AppAsyncView({
    super.key,
    required this.isLoading,
    required this.error,
    required this.data,
    required this.isEmpty,
    required this.loadingMessage,
    required this.retryLabel,
    required this.onRetry,
    required this.emptyBuilder,
    required this.builder,
  });

  final bool isLoading;

  /// Already mapped from a ProblemDetails `code` to a localised message.
  /// Never the raw `detail` from the server. See docs/api/api-conventions.md.
  final String? error;

  final T? data;
  final bool Function(T data) isEmpty;

  final String loadingMessage;
  final String retryLabel;
  final VoidCallback onRetry;

  final WidgetBuilder emptyBuilder;
  final Widget Function(BuildContext context, T data) builder;

  @override
  Widget build(BuildContext context) {
    if (isLoading) return AppLoading(message: loadingMessage);
    if (error != null) {
      return AppErrorView(
        message: error!,
        retryLabel: retryLabel,
        onRetry: onRetry,
      );
    }
    final value = data;
    if (value == null) return AppLoading(message: loadingMessage);
    if (isEmpty(value)) return emptyBuilder(context);
    return builder(context, value);
  }
}
