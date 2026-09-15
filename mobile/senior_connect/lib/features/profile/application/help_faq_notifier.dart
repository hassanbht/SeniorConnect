// lib/features/profile/application/help_faq_notifier.dart
// Search query state for HelpFaqScreen.
// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:riverpod_annotation/riverpod_annotation.dart';
part 'help_faq_notifier.g.dart';
@riverpod
class HelpFaqNotifier extends _ {
  @override
  String build() => '';
  void setQuery(String query) => state = query.trim();
}
