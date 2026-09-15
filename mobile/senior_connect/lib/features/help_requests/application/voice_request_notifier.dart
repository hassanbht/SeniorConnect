import 'package:flutter_riverpod/flutter_riverpod.dart';

class VoiceRequestState {
  const VoiceRequestState({
    this.isListening = false,
    this.currentTranscript = '',
    this.emergencyDetected = false,
    this.nursingDetected = false,
  });

  final bool isListening;
  final String currentTranscript;
  final bool emergencyDetected;
  final bool nursingDetected;

  VoiceRequestState copyWith({
    bool? isListening,
    String? currentTranscript,
    bool? emergencyDetected,
    bool? nursingDetected,
  }) {
    return VoiceRequestState(
      isListening: isListening ?? this.isListening,
      currentTranscript: currentTranscript ?? this.currentTranscript,
      emergencyDetected: emergencyDetected ?? this.emergencyDetected,
      nursingDetected: nursingDetected ?? this.nursingDetected,
    );
  }
}

class VoiceRequestNotifier extends StateNotifier<VoiceRequestState> {
  VoiceRequestNotifier() : super(const VoiceRequestState());

  static const List<String> emergencyKeywords = [
    'notfall', 'schmerz', 'herz', 'sturz', 'gefallen', 'blut',
    'atemnot', '144', '112', 'emergency', 'درد', 'سقوط', 'خون'
  ];

  static const List<String> nursingKeywords = [
    'spritze', 'medikament dosieren', 'strümpfe anziehen',
    'verband wechseln', 'infusion', 'katheter', 'تزریق', 'پانسمان'
  ];

  void toggleListening() {
    final newListening = !state.isListening;
    if (newListening) {
      const simulated =
          'Ich brauche morgen Hilfe beim Lebensmitteleinkauf beim SPAR.';
      final lower = simulated.toLowerCase();
      final emerg = emergencyKeywords.any((k) => lower.contains(k));
      final nurs = nursingKeywords.any((k) => lower.contains(k));
      state = state.copyWith(
        isListening: true,
        currentTranscript: simulated,
        emergencyDetected: emerg,
        nursingDetected: nurs,
      );
    } else {
      state = state.copyWith(isListening: false);
    }
  }

  void updateTranscript(String text) {
    final lower = text.toLowerCase();
    final emerg = emergencyKeywords.any((k) => lower.contains(k));
    final nurs = nursingKeywords.any((k) => lower.contains(k));
    state = state.copyWith(
      currentTranscript: text,
      emergencyDetected: emerg,
      nursingDetected: nurs,
    );
  }
}

final voiceRequestProvider =
    StateNotifierProvider.autoDispose<VoiceRequestNotifier, VoiceRequestState>(
  (ref) => VoiceRequestNotifier(),
);
