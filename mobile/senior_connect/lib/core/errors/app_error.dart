// lib/core/errors/app_error.dart
//
// Sealed union of all domain-level errors the app can encounter.
// Maps from ProblemDetails `code`, never from message text.

sealed class AppError {
  const AppError();

  const factory AppError.unauthenticated() = _UnauthenticatedError;
  const factory AppError.rateLimited() = _RateLimitedError;
  const factory AppError.noInternet() = _NoInternetError;
  const factory AppError.tokenCompromised() = _TokenCompromisedError;
  const factory AppError.forbidden({required List<String> missing}) =
      _ForbiddenError;
  const factory AppError.unknown({required String code}) = _UnknownError;

  /// Localization key to show in the UI. Never exposes the raw code.
  String get l10nKey;

  /// True if the error means the session is gone and auth screens must show.
  bool get requiresReauth => false;
}

final class _UnauthenticatedError extends AppError {
  const _UnauthenticatedError();

  @override
  String get l10nKey => 'errors.session_expired';

  @override
  bool get requiresReauth => true;
}

final class _TokenCompromisedError extends AppError {
  const _TokenCompromisedError();

  @override
  String get l10nKey => 'errors.session_expired';

  @override
  bool get requiresReauth => true;
}

final class _RateLimitedError extends AppError {
  const _RateLimitedError();

  @override
  String get l10nKey => 'auth.otp.rate_limited';
}

final class _NoInternetError extends AppError {
  const _NoInternetError();

  @override
  String get l10nKey => 'errors.no_internet';
}

final class _ForbiddenError extends AppError {
  const _ForbiddenError({required this.missing});

  /// The capability/verification keys that are missing (from ProblemDetails).
  final List<String> missing;

  @override
  String get l10nKey => 'errors.not_allowed';
}

final class _UnknownError extends AppError {
  const _UnknownError({required this.code});

  final String code;

  @override
  String get l10nKey => switch (code) {
        'RATE_LIMITED' => 'auth.otp.rate_limited',
        'EMAIL_ALREADY_REGISTERED' => 'auth.register.email_already_registered',
        'PASSWORDS_DO_NOT_MATCH' => 'auth.register.password_mismatch',
        'INVALID_CREDENTIALS' => 'auth.login.invalid_credentials',
        'EMAIL_VERIFICATION_REQUIRED' => 'auth.login.email_verification_required',
        _ => 'errors.generic',
      };
}
