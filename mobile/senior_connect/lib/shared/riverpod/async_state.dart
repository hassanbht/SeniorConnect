import 'package:flutter/foundation.dart';

/// Immutable 5-state representation for async UI data workflows in SeniorConnect.
///
/// BINDING RULE: All five states (`initial`, `loading`, `loaded`, `empty`, `error`)
/// must be supported and handled gracefully by every screen.
@immutable
sealed class AsyncState<T> {
  const AsyncState();

  const factory AsyncState.initial() = AsyncInitial<T>;
  const factory AsyncState.loading() = AsyncLoading<T>;
  const factory AsyncState.loaded(T data) = AsyncLoaded<T>;
  const factory AsyncState.empty([String? message]) = AsyncEmpty<T>;
  const factory AsyncState.error(String message, [Object? error, StackTrace? stackTrace]) =
      AsyncError<T>;

  bool get isInitial => this is AsyncInitial<T>;
  bool get isLoading => this is AsyncLoading<T>;
  bool get isLoaded => this is AsyncLoaded<T>;
  bool get isEmpty => this is AsyncEmpty<T>;
  bool get isError => this is AsyncError<T>;

  T? get dataOrNull {
    final self = this;
    return self is AsyncLoaded<T> ? self.data : null;
  }

  R when<R>({
    required R Function() initial,
    required R Function() loading,
    required R Function(T data) loaded,
    required R Function(String? message) empty,
    required R Function(String message, Object? err) error,
  }) {
    return switch (this) {
      AsyncInitial<T>() => initial(),
      AsyncLoading<T>() => loading(),
      AsyncLoaded<T>(:final data) => loaded(data),
      AsyncEmpty<T>(:final message) => empty(message),
      AsyncError<T>(message: final msg, error: final err) => error(msg, err),
    };
  }

  R maybeWhen<R>({
    R Function()? initial,
    R Function()? loading,
    R Function(T data)? loaded,
    R Function(String? message)? empty,
    R Function(String message, Object? err)? error,
    required R Function() orElse,
  }) {
    return switch (this) {
      AsyncInitial<T>() => initial != null ? initial() : orElse(),
      AsyncLoading<T>() => loading != null ? loading() : orElse(),
      AsyncLoaded<T>(:final data) => loaded != null ? loaded(data) : orElse(),
      AsyncEmpty<T>(:final message) => empty != null ? empty(message) : orElse(),
      AsyncError<T>(message: final msg, error: final err) =>
        error != null ? error(msg, err) : orElse(),
    };
  }
}

final class AsyncInitial<T> extends AsyncState<T> {
  const AsyncInitial();

  @override
  String toString() => 'AsyncInitial<$T>()';

  @override
  bool operator ==(Object other) => identical(this, other) || other is AsyncInitial<T>;

  @override
  int get hashCode => runtimeType.hashCode;
}

final class AsyncLoading<T> extends AsyncState<T> {
  const AsyncLoading();

  @override
  String toString() => 'AsyncLoading<$T>()';

  @override
  bool operator ==(Object other) => identical(this, other) || other is AsyncLoading<T>;

  @override
  int get hashCode => runtimeType.hashCode;
}

final class AsyncLoaded<T> extends AsyncState<T> {
  const AsyncLoaded(this.data);
  final T data;

  @override
  String toString() => 'AsyncLoaded<$T>(data: $data)';

  @override
  bool operator ==(Object other) =>
      identical(this, other) || (other is AsyncLoaded<T> && other.data == data);

  @override
  int get hashCode => Object.hash(runtimeType, data);
}

final class AsyncEmpty<T> extends AsyncState<T> {
  const AsyncEmpty([this.message]);
  final String? message;

  @override
  String toString() => 'AsyncEmpty<$T>(message: $message)';

  @override
  bool operator ==(Object other) =>
      identical(this, other) || (other is AsyncEmpty<T> && other.message == message);

  @override
  int get hashCode => Object.hash(runtimeType, message);
}

final class AsyncError<T> extends AsyncState<T> {
  const AsyncError(this.message, [this.error, this.stackTrace]);
  final String message;
  final Object? error;
  final StackTrace? stackTrace;

  @override
  String toString() => 'AsyncError<$T>(message: $message, error: $error)';

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is AsyncError<T> && other.message == message && other.error == error);

  @override
  int get hashCode => Object.hash(runtimeType, message, error);
}
