import 'package:flutter_riverpod/flutter_riverpod.dart';

class DelegationPermissionsNotifier
    extends StateNotifier<Map<String, bool>> {
  DelegationPermissionsNotifier(Map<String, bool> initial)
      : super(Map<String, bool>.unmodifiable(initial));

  void toggle(String key, bool value) {
    final updated = Map<String, bool>.from(state);
    updated[key] = value;
    state = Map<String, bool>.unmodifiable(updated);
  }
}

final delegationPermissionsProvider = StateNotifierProvider.autoDispose.family<
    DelegationPermissionsNotifier, Map<String, bool>, Map<String, bool>>(
  (ref, initial) => DelegationPermissionsNotifier(initial),
);
