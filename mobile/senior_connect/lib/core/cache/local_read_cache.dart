// lib/core/cache/local_read_cache.dart
//
// P7-04: Offline read cache for low-connectivity and airplane mode resilience.
// Caches read queries (e.g. today's appointments, emergency contacts) locally
// so they remain accessible even when completely offline.

import 'dart:convert';
import 'package:shared_preferences/shared_preferences.dart';

class LocalReadCache {
  static const String _prefix = 'senior_connect_cache_';

  /// Phase 4 (docs/security/location-consent-and-visibility.md §11): exact
  /// coordinates are never written to disk. They stay in memory only, so a
  /// revoked consent leaves no copy on the device beyond the current screen.
  static const _neverPersisted = {'latitude', 'longitude', 'lat', 'lng', 'lon'};

  static dynamic withoutCoordinates(dynamic data) {
    if (data is Map) {
      return {
        for (final entry in data.entries)
          if (!_neverPersisted.contains(entry.key.toString().toLowerCase()))
            entry.key: withoutCoordinates(entry.value),
      };
    }
    if (data is List) return data.map(withoutCoordinates).toList();
    return data;
  }

  /// Saves [data] into local persistent storage under [key], without any
  /// coordinate fields.
  static Future<bool> save(String key, dynamic data) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final jsonStr = jsonEncode(withoutCoordinates(data));
      return await prefs.setString('$_prefix$key', jsonStr);
    } catch (_) {
      return false;
    }
  }

  /// Reads cached data for [key], or returns null if not cached or corrupted.
  static Future<dynamic> read(String key) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final jsonStr = prefs.getString('$_prefix$key');
      if (jsonStr == null || jsonStr.isEmpty) return null;
      return jsonDecode(jsonStr);
    } catch (_) {
      return null;
    }
  }

  /// Removes cached data for [key].
  static Future<bool> remove(String key) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      return await prefs.remove('$_prefix$key');
    } catch (_) {
      return false;
    }
  }

  /// Alias for [remove].
  static Future<bool> clear(String key) => remove(key);

  /// Removes every cached entry. Called when a session ends (logout or a
  /// rejected refresh) so the next user of the device sees nothing cached.
  static Future<void> clearAll() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final keys = prefs.getKeys().where((k) => k.startsWith(_prefix));
      for (final key in keys.toList()) {
        await prefs.remove(key);
      }
    } catch (_) {
      // Best effort: a failed cache wipe must never block signing out.
    }
  }
}
