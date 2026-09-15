// DO NOT use setState in the screen. See AGENTS.md §State Management.
import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';
part 'org_post_form_notifier.freezed.dart';
part 'org_post_form_notifier.g.dart';

enum PostCategory { news, event }

@freezed
class OrgPostFormState with _ {
  const factory OrgPostFormState({
    @Default(PostCategory.event) PostCategory category,
    DateTime? startsAt,
    @Default(false) bool isSubmitting,
    @Default(false) bool hasError,
  }) = _OrgPostFormState;
}

@riverpod
class OrgPostFormNotifier extends _ {
  @override
  OrgPostFormState build() =>
      OrgPostFormState(startsAt: DateTime.now().add(const Duration(days: 7)));
  void setCategory(PostCategory cat) => state = state.copyWith(category: cat);
  void setStartsAt(DateTime d) => state = state.copyWith(startsAt: d);
  void setSubmitting(bool v) =>
      state = state.copyWith(isSubmitting: v, hasError: false);
  void setError() =>
      state = state.copyWith(hasError: true, isSubmitting: false);
}
