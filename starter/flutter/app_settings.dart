// lib/core/settings/app_settings.dart
//
// Device-level preferences. These are deliberately NOT synced to the server:
// a senior's tablet and their daughter's phone legitimately differ.

import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../design_system/app_theme.dart';

class AppSettings extends ChangeNotifier {
  AppSettings._(this._prefs, this._themeMode, this._seniorMode);

  static const _kThemeMode = 'settings.themeMode';
  static const _kSeniorMode = 'settings.seniorMode';

  final SharedPreferences _prefs;

  AppThemeMode _themeMode;
  bool _seniorMode;

  AppThemeMode get themeMode => _themeMode;
  bool get seniorMode => _seniorMode;

  static Future<AppSettings> load() async {
    final prefs = await SharedPreferences.getInstance();

    final storedTheme = prefs.getString(_kThemeMode);
    final themeMode = AppThemeMode.values.firstWhere(
      (m) => m.name == storedTheme,
      orElse: () => AppThemeMode.system,
    );

    return AppSettings._(
      prefs,
      themeMode,
      prefs.getBool(_kSeniorMode) ?? false,
    );
  }

  Future<void> setThemeMode(AppThemeMode mode) async {
    if (_themeMode == mode) return;
    _themeMode = mode;
    notifyListeners();
    await _prefs.setString(_kThemeMode, mode.name);
  }

  /// "Große Ansicht" — never labelled "Seniorenmodus" in the UI.
  Future<void> setSeniorMode(bool enabled) async {
    if (_seniorMode == enabled) return;
    _seniorMode = enabled;
    notifyListeners();
    await _prefs.setBool(_kSeniorMode, enabled);
  }
}
