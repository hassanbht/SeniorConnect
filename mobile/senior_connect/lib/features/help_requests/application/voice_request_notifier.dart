// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
part 'voice_request_notifier.freezed.dart';
part 'voice_request_notifier.g.dart';

@freezed
class VoiceRequestState with _ {
  const factory VoiceRequestState({
    @Default(false) bool isListening,
    @Default('') String currentTranscript,
    @Default(false) bool emergencyDetected,
    @Default(false) bool nursingDetected,
  }) = _VoiceRequestState;
}

@riverpod
class VoiceRequestNotifier extends _ {
  static const _emergency = [
    'notfall',
    'schmerz',
    'herz',
    'sturz',
    'blut',
    'atemnot',
    '144',
    '112',
    'emergency',
  ];
  static const _nursing = [
    'spritze',
    'medikament dosieren',
    'verband wechseln',
    'infusion',
    'katheter',
  ];
  @override
  VoiceRequestState build() => const VoiceRequestState();
  void toggleListening() {
    final listening = !state.isListening;
    if (listening) {
      const t = 'Ich brauche morgen Hilfe beim Lebensmitteleinkauf beim SPAR.';
      final lower = t.toLowerCase();
      state = state.copyWith(
        isListening: true,
        currentTranscript: t,
        emergencyDetected: _emergency.any(lower.contains),
        nursingDetected: _nursing.any(lower.contains),
      );
    } else {
      state = state.copyWith(isListening: false);
    }
  }

  void setTranscript(String text) {
    final lower = text.toLowerCase();
    state = state.copyWith(
      currentTranscript: text,
      emergencyDetected: _emergency.any(lower.contains),
      nursingDetected: _nursing.any(lower.contains),
    );
  }
}
