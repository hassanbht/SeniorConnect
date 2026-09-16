// lib/core/cache/local_read_cache.dart
//
// P7-04: Offline read cache for low-connectivity and airplane mode resilience.
// Caches read queries (e.g. today's appointments, emergency contacts) locally
// so they remain accessible even when completely offline.

import 'dart:convert';
import 'package:shared_preferences/shared_preferences.dart';

class LocalReadCache {
  static const String _prefix = 'senior_connect_cache_';

  /// Saves [data] into local persistent storage under [key].
  static Future<bool> save(String key, dynamic data) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final jsonStr = jsonEncode(data);
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
}
