// lib/core/cache/reference_data_repository.dart
//
// Phase 2 (docs/architecture/reference-data-cache.md): a small read-through
// cache for public, slow-changing reference lists only. The server stays the
// source of truth; this only saves repeated downloads.
//
// Only the datasets in [ReferenceDataset] can be cached. There is no generic
// "cache any response" entry point on purpose. Entries live under the
// `reference.` key prefix, apart from LocalReadCache's session data, so they
// survive logout (they hold nothing user-specific).

import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../network/api_client.dart';

/// Bump when the stored shape of any dataset changes: every older entry is
/// then ignored and deleted.
const int kReferenceCacheVersion = 1;

const String kReferenceKeyPrefix = 'reference.';

/// The closed allowlist of cacheable datasets.
///
/// PLZ lookups are deliberately absent: the typed postal code is usually the
/// user's own, and a cache key naming it would outlive the session.
enum ReferenceDataset {
  bundeslaender('geo.bundeslaender', Duration(days: 30)),
  bezirke('geo.bezirke', Duration(days: 30), clearedOnLogout: true),
  gemeinden('geo.gemeinden', Duration(days: 30), clearedOnLogout: true),
  activityCategories('activity.categories', Duration(hours: 24));

  const ReferenceDataset(this.key, this.ttl, {this.clearedOnLogout = false});

  final String key;
  final Duration ttl;

  /// Parameterised entries: the key itself (which Bundesland/Bezirk was
  /// opened) hints at where the last user lives, so it ends with the session.
  final bool clearedOnLogout;
}

/// Field names that mark personal or session data. A payload carrying any of
/// them is not reference data and is never stored (defence in depth: the
/// allowlisted endpoints never return them).
const Set<String> kForbiddenReferenceFields = {
  'userId',
  'displayName',
  'addressLine',
  'phone',
  'phoneNumber',
  'email',
  'accessToken',
  'refreshToken',
  'token',
  'safeguardingCaseId',
  'helpRequestId',
  'assignedVolunteerId',
};

bool _containsForbiddenField(Object? node) {
  if (node is Map) {
    return node.entries.any(
      (e) =>
          kForbiddenReferenceFields.contains(e.key) ||
          _containsForbiddenField(e.value),
    );
  }
  if (node is List) return node.any(_containsForbiddenField);
  return false;
}

class ReferenceDataRepository {
  ReferenceDataRepository(this._api, {DateTime Function()? now})
    : _now = now ?? DateTime.now;

  final ApiClient _api;
  final DateTime Function() _now;

  static final _paramPattern = RegExp(r'^[0-9A-Za-z]{1,12}$');

  static String storageKey(ReferenceDataset dataset, [String? param]) =>
      '${kReferenceKeyPrefix}v$kReferenceCacheVersion.${dataset.key}'
      '${param == null ? '' : '.$param'}';

  /// GET /api/v1/activities/categories (global, not tenant- or user-specific).
  Future<List<Map<String, dynamic>>> activityCategories({
    bool refresh = false,
  }) => load(
    ReferenceDataset.activityCategories,
    path: '/api/v1/activities/categories',
    parse: (raw) => [for (final e in raw) Map<String, dynamic>.from(e as Map)],
    refresh: refresh,
  );

  /// Returns [dataset] from a fresh cache entry, else from the server. A stale
  /// entry is returned only when the server is temporarily unreachable; with no
  /// usable entry the error is rethrown. Nothing is ever invented.
  ///
  /// [parse] must throw on a malformed payload: a server payload that fails it
  /// is not stored, and a cached one that fails it is deleted.
  Future<T> load<T>(
    ReferenceDataset dataset, {
    required String path,
    required T Function(List<dynamic> raw) parse,
    String? param,
    Map<String, dynamic>? queryParameters,
    bool refresh = false,
  }) async {
    // Parameters become key segments; anything unexpected is not cached.
    final cacheable = param == null || _paramPattern.hasMatch(param);
    final key = storageKey(dataset, param);
    final entry = cacheable ? await _read(key) : null;

    T? cached;
    var isFresh = false;
    if (entry != null) {
      try {
        cached = parse(entry.payload);
        isFresh = _now().difference(entry.storedAt) < dataset.ttl;
      } catch (_) {
        await _removeWhere((k) => k == key);
      }
    }
    if (cached != null && isFresh && !refresh) return cached;

    final List<dynamic> raw;
    try {
      final response = await _api.get<dynamic>(
        path,
        queryParameters: queryParameters,
      );
      if (response is! List) {
        throw const FormatException('Reference payload is not a list');
      }
      raw = response;
    } catch (e) {
      if (cached != null && _isTemporaryFailure(e)) return cached;
      rethrow;
    }

    final result = parse(raw);
    if (cacheable && !_containsForbiddenField(raw)) await _write(key, raw);
    return result;
  }

  /// Drops one dataset (every parameter), e.g. on an explicit refresh request.
  static Future<void> invalidate(ReferenceDataset dataset) {
    final base = storageKey(dataset);
    return _removeWhere((k) => k == base || k.startsWith('$base.'));
  }

  /// Called on logout: drops the parameterised entries, keeps the rest.
  static Future<void> clearSessionTraces() async {
    for (final d in ReferenceDataset.values.where((d) => d.clearedOnLogout)) {
      await invalidate(d);
    }
  }

  /// Drops every reference entry. Never touches auth, session or user keys.
  static Future<void> invalidateAll() =>
      _removeWhere((k) => k.startsWith(kReferenceKeyPrefix));

  static bool _isTemporaryFailure(Object e) {
    if (e is! DioException) return false;
    switch (e.type) {
      case DioExceptionType.connectionTimeout:
      case DioExceptionType.sendTimeout:
      case DioExceptionType.receiveTimeout:
      case DioExceptionType.connectionError:
        return true;
      case DioExceptionType.badResponse:
        return (e.response?.statusCode ?? 0) >= 500;
      default:
        return false;
    }
  }

  Future<_Entry?> _read(String key) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final text = prefs.getString(key);
      if (text == null) return null;
      try {
        final json = jsonDecode(text) as Map<String, dynamic>;
        if (json['v'] != kReferenceCacheVersion) {
          throw const FormatException('Other cache version');
        }
        return _Entry(
          DateTime.fromMillisecondsSinceEpoch(json['storedAt'] as int),
          json['payload'] as List<dynamic>,
        );
      } catch (_) {
        await prefs.remove(key); // corrupt or from another cache version
        return null;
      }
    } catch (_) {
      return null; // storage unavailable: behave as a cache miss
    }
  }

  Future<void> _write(String key, List<dynamic> payload) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      await _purgeOtherVersions(prefs);
      await prefs.setString(
        key,
        jsonEncode({
          'v': kReferenceCacheVersion,
          'storedAt': _now().millisecondsSinceEpoch,
          'payload': payload,
        }),
      );
    } catch (_) {
      // A failed cache write only costs a later re-download.
    }
  }

  static Future<void> _purgeOtherVersions(SharedPreferences prefs) async {
    const current = '${kReferenceKeyPrefix}v$kReferenceCacheVersion.';
    for (final k in prefs.getKeys().toList()) {
      if (k.startsWith(kReferenceKeyPrefix) && !k.startsWith(current)) {
        await prefs.remove(k);
      }
    }
  }

  static Future<void> _removeWhere(bool Function(String key) test) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      for (final k in prefs.getKeys().where(test).toList()) {
        await prefs.remove(k);
      }
    } catch (_) {
      // Best effort.
    }
  }
}

class _Entry {
  const _Entry(this.storedAt, this.payload);

  final DateTime storedAt;
  final List<dynamic> payload;
}
