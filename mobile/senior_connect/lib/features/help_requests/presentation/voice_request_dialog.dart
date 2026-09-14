import 'package:easy_localization/easy_localization.dart';
import 'package:flutter/material.dart';
import 'package:senior_connect/core/design_system/app_tokens.dart';
import 'package:senior_connect/shared/widgets/app_button.dart';

/// Modal dialog allowing seniors to speak their request or type via speech-to-text,
/// running local emergency triage and auto-populating structured form fields (P8-04, P8-05).
class VoiceRequestDialog extends StatefulWidget {
  final ValueChanged<String> onTranscriptConfirmed;

  const VoiceRequestDialog({
    super.key,
    required this.onTranscriptConfirmed,
  });

  @override
  State<VoiceRequestDialog> createState() => _VoiceRequestDialogState();
}

class _VoiceRequestDialogState extends State<VoiceRequestDialog> {
  bool _isListening = false;
  String _currentTranscript = '';
  bool _emergencyDetected = false;
  bool _nursingDetected = false;

  final List<String> _emergencyKeywords = [
    'notfall', 'schmerz', 'herz', 'sturz', 'gefallen', 'blut',
    'atemnot', '144', '112', 'emergency', 'درد', 'سقوط', 'خون'
  ];

  final List<String> _nursingKeywords = [
    'spritze', 'medikament dosieren', 'strümpfe anziehen',
    'verband wechseln', 'infusion', 'katheter', 'تزریق', 'پانسمان'
  ];

  void _toggleListening() {
    setState(() {
      _isListening = !_isListening;
      if (_isListening) {
        // Simulated speech streaming
        _currentTranscript = 'Ich brauche morgen Hilfe beim Lebensmitteleinkauf beim SPAR.';
        _evaluateText(_currentTranscript);
      }
    });
  }

  void _evaluateText(String text) {
    final lower = text.toLowerCase();
    _emergencyDetected = _emergencyKeywords.any((k) => lower.contains(k));
    _nursingDetected = _nursingKeywords.any((k) => lower.contains(k));
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

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
                Icon(
                  Icons.mic,
                  color: theme.colorScheme.primary,
                  size: 28,
                ),
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
                color: _isListening
                    ? theme.colorScheme.primaryContainer.withValues(alpha: 0.2)
                    : theme.colorScheme.surfaceContainerHighest.withValues(alpha: 0.3),
                borderRadius: BorderRadius.circular(AppRadius.md),
                border: Border.all(
                  color: _isListening
                      ? theme.colorScheme.primary
                      : theme.colorScheme.outlineVariant,
                ),
              ),
              child: Column(
                children: [
                  if (_isListening)
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
                    _currentTranscript.isEmpty
                        ? 'Tippen Sie auf das Mikrofon und sprechen Sie ganz natürlich.'
                        : _currentTranscript,
                    textAlign: TextAlign.center,
                    style: theme.textTheme.bodyLarge,
                  ),
                ],
              ),
            ),
            if (_emergencyDetected) ...[
              const SizedBox(height: AppSpacing.md),
              Container(
                padding: const EdgeInsets.all(AppSpacing.sm),
                decoration: BoxDecoration(
                  color: theme.colorScheme.errorContainer,
                  borderRadius: BorderRadius.circular(AppRadius.sm),
                ),
                child: Text(
                  'voice_input.emergency_warning'.tr(),
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: theme.colorScheme.onErrorContainer,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ),
            ],
            if (_nursingDetected) ...[
              const SizedBox(height: AppSpacing.md),
              Container(
                padding: const EdgeInsets.all(AppSpacing.sm),
                decoration: BoxDecoration(
                  color: theme.colorScheme.tertiaryContainer,
                  borderRadius: BorderRadius.circular(AppRadius.sm),
                ),
                child: Text(
                  'voice_input.nursing_warning'.tr(),
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: theme.colorScheme.onTertiaryContainer,
                  ),
                ),
              ),
            ],
            const SizedBox(height: AppSpacing.lg),
            Row(
              children: [
                Expanded(
                  child: AppButton(
                    label: _isListening
                        ? 'voice_input.stop_recording'.tr()
                        : 'voice_input.start_recording'.tr(),
                    icon: _isListening ? Icons.stop : Icons.mic,
                    variant: _isListening
                        ? AppButtonVariant.tonal
                        : AppButtonVariant.primary,
                    onPressed: _toggleListening,
                  ),
                ),
                if (_currentTranscript.isNotEmpty && !_emergencyDetected) ...[
                  const SizedBox(width: AppSpacing.sm),
                  Expanded(
                    child: AppButton(
                      label: 'voice_input.convert_to_request'.tr(),
                      icon: Icons.check,
                      variant: AppButtonVariant.primary,
                      onPressed: () {
                        widget.onTranscriptConfirmed(_currentTranscript);
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
