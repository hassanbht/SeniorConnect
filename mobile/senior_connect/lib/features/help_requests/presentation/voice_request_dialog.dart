import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:senior_connect/core/design_system/app_tokens.dart';
import 'package:senior_connect/shared/widgets/app_button.dart';

import '../application/voice_request_notifier.dart';

/// Modal dialog allowing seniors to speak their request or type via speech-to-text,
/// handing the transcript to the request flow; emergency triage happens server-side (P8-04, P8-05).
class VoiceRequestDialog extends ConsumerWidget {
  final ValueChanged<String> onTranscriptConfirmed;

  const VoiceRequestDialog({super.key, required this.onTranscriptConfirmed});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final state = ref.watch(voiceRequestProvider);
    final notifier = ref.read(voiceRequestProvider.notifier);

    return Dialog(
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppRadius.lg),
      ),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.lg),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Icon(Icons.mic, color: theme.colorScheme.primary, size: 28),
                const SizedBox(width: AppSpacing.sm),
                Expanded(
                  child: Text(
                    'voice_input.title'.tr(),
                    style: theme.textTheme.titleMedium?.copyWith(
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
                IconButton(
                  icon: const Icon(Icons.close),
                  onPressed: () => Navigator.of(context).pop(),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.md),
            Container(
              padding: const EdgeInsets.all(AppSpacing.md),
              decoration: BoxDecoration(
                color: state.isListening
                    ? theme.colorScheme.primaryContainer.withAlpha(51)
                    : theme.colorScheme.surfaceContainerHighest.withAlpha(76),
                borderRadius: BorderRadius.circular(AppRadius.md),
                border: Border.all(
                  color: state.isListening
                      ? theme.colorScheme.primary
                      : theme.colorScheme.outlineVariant,
                ),
              ),
              child: Column(
                children: [
                  if (state.isListening)
                    Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        const SizedBox(
                          width: 16,
                          height: 16,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        ),
                        const SizedBox(width: AppSpacing.sm),
                        Text(
                          'voice_input.listening'.tr(),
                          style: theme.textTheme.bodyMedium?.copyWith(
                            fontStyle: FontStyle.italic,
                          ),
                        ),
                      ],
                    ),
                  const SizedBox(height: AppSpacing.sm),
                  Text(
                    state.currentTranscript.isEmpty
                        ? 'voice_input.tap_to_speak'.tr()
                        : state.currentTranscript,
                    textAlign: TextAlign.center,
                    style: theme.textTheme.bodyLarge,
                  ),
                ],
              ),
            ),
            const SizedBox(height: AppSpacing.lg),
            Row(
              children: [
                Expanded(
                  child: AppButton(
                    label: state.isListening
                        ? 'voice_input.stop_recording'.tr()
                        : 'voice_input.start_recording'.tr(),
                    icon: state.isListening ? Icons.stop : Icons.mic,
                    variant: state.isListening
                        ? AppButtonVariant.tonal
                        : AppButtonVariant.primary,
                    onPressed: notifier.toggleListening,
                  ),
                ),
                if (state.currentTranscript.isNotEmpty) ...[
                  const SizedBox(width: AppSpacing.sm),
                  Expanded(
                    child: AppButton(
                      label: 'voice_input.convert_to_request'.tr(),
                      icon: Icons.check,
                      variant: AppButtonVariant.primary,
                      onPressed: () {
                        onTranscriptConfirmed(state.currentTranscript);
                        Navigator.of(context).pop();
                      },
                    ),
                  ),
                ],
              ],
            ),
          ],
        ),
      ),
    );
  }
}
