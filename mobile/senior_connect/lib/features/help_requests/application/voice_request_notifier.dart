import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Voice input state. Emergency / nursing detection is NOT done here: the
/// transcript becomes the request notes, and the server rejects it on create
/// with EMERGENCY_DETECTED / CATEGORY_BLOCKED (BR-SCOPE-02/03/04), which the
/// request flow handles. No duplicated keyword rules on the client.
class VoiceRequestState {
  const VoiceRequestState({
    this.isListening = false,
    this.currentTranscript = '',
  });

  final bool isListening;
  final String currentTranscript;

  VoiceRequestState copyWith({bool? isListening, String? currentTranscript}) {
    return VoiceRequestState(
      isListening: isListening ?? this.isListening,
      currentTranscript: currentTranscript ?? this.currentTranscript,
    );
  }
}

class VoiceRequestNotifier extends StateNotifier<VoiceRequestState> {
  VoiceRequestNotifier() : super(const VoiceRequestState());

  void toggleListening() {
    final newListening = !state.isListening;
    if (newListening) {
      // ponytail: simulated transcript until a speech-to-text engine is wired.
      const simulated =
          'Ich brauche morgen Hilfe beim Lebensmitteleinkauf beim SPAR.';
      state = state.copyWith(isListening: true, currentTranscript: simulated);
    } else {
      state = state.copyWith(isListening: false);
    }
  }

  void updateTranscript(String text) {
    state = state.copyWith(currentTranscript: text);
  }
}

final voiceRequestProvider =
    StateNotifierProvider.autoDispose<VoiceRequestNotifier, VoiceRequestState>(
      (ref) => VoiceRequestNotifier(),
    );
