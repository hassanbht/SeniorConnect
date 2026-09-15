// lib/shared/riverpod/async_state.dart
//
// Shared five-state Freezed union used by every @riverpod Notifier.
//
// States: initial → loading → loaded(data) | empty | error(message)
//
// ⚠ ARCHITECTURE RULE (binding):
//   Every screen that performs I/O MUST use an AsyncNotifier + this union.
//   Direct setState() is FORBIDDEN outside of app_button.dart.
//   See AGENTS.md §State Management.
//
// After editing this file run:
//   dart run build_runner build --delete-conflicting-outputs

import 'package:freezed_annotation/freezed_annotation.dart';

part 'async_state.freezed.dart';

/// Five-state async value used by every Riverpod Notifier in SeniorConnect.
///
/// Usage inside a @riverpod class:
/// ```dart
/// @riverpod
/// class MyNotifier extends _$MyNotifier {
///   @override
///   AsyncState<MyData> build() => const AsyncState.initial();
///
///   Future<void> load() async {
///     state = const AsyncState.loading();
///     try {
///       final data = await _repo.fetch();
///       state = data.isEmpty
///           ? const AsyncState.empty()
///           : AsyncState.loaded(data);
///     } catch (e) {
///       state = AsyncState.error(e.toString());
///     }
///   }
/// }
/// ```
@freezed
sealed class AsyncState<T> with _$AsyncState<T> {
  /// No operation has started yet.
  const factory AsyncState.initial() = AsyncStateInitial<T>;

  /// An async operation is in progress.
  const factory AsyncState.loading() = AsyncStateLoading<T>;

  /// Data has been loaded (non-empty).
  const factory AsyncState.loaded(T data) = AsyncStateLoaded<T>;

  /// The request succeeded but there is nothing to show.
  const factory AsyncState.empty() = AsyncStateEmpty<T>;

  /// The operation failed.
  const factory AsyncState.error(String message) = AsyncStateError<T>;
}
