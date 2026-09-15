// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
part 'create_help_request_notifier.freezed.dart';
part 'create_help_request_notifier.g.dart';

@freezed
class CreateHelpRequestState with _ {
  const factory CreateHelpRequestState({
    @Default('Garten & Pflanzen') String selectedCategory,
    @Default(false) bool isSubmitting,
  }) = _CreateHelpRequestState;
}

@riverpod
class CreateHelpRequestNotifier extends _ {
  @override
  CreateHelpRequestState build() => const CreateHelpRequestState();
  void selectCategory(String cat) =>
      state = state.copyWith(selectedCategory: cat);
  void setSubmitting(bool v) => state = state.copyWith(isSubmitting: v);
}
