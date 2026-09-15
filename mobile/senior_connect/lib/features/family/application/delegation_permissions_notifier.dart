// Manages in-dialog permission toggle state.
// DO NOT use setState in the dialog. See AGENTS.md §State Management.
import 'package:riverpod_annotation/riverpod_annotation.dart';
part 'delegation_permissions_notifier.g.dart';

@riverpod
class DelegationPermissionsNotifier extends _ {
  @override
  Map<String, bool> build(Map<String, bool> initialPermissions) =>
      Map<String, bool>.from(initialPermissions);
  void toggle(String key, bool value) => state = {...state, key: value};
}
