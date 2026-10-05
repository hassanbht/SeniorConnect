// test/wave2_client_boundary_test.dart
//
// Wave-2 guards for three client rules that are easy to reintroduce by accident:
//
//   1. A translated sentence must never be sent as request data. A `.tr()`
//      inside a `data:` map writes the active locale's prose into an auditable
//      server record, where it stops being greppable or comparable.
//   2. Authorization must never be inferred from a non-authoritative field.
//      `primaryAuthMethod == 'Password'` decided "is staff", but password is
//      legitimate for seniors and ordinary volunteers too.
//   3. A raw server enum must not be rendered as a label; the neighbouring
//      filter chips translate the same codes, so the card used to show
//      "Dormant" while the chip showed its translation.
//
// These read the source tree rather than exercising widgets, so a regression
// fails on the exact line that reintroduces it.

import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

Directory _libDirectory() {
  var dir = Directory.current;
  while (dir.path != dir.parent.path) {
    if (Directory('${dir.path}${Platform.pathSeparator}lib').existsSync()) {
      return Directory('${dir.path}${Platform.pathSeparator}lib');
    }
    dir = dir.parent;
  }
  throw StateError('Could not locate lib/ from ${Directory.current.path}');
}

Iterable<File> _dartFilesUnder(String root) => Directory(root)
    .listSync(recursive: true)
    .whereType<File>()
    .where((f) => f.path.endsWith('.dart'));

void main() {
  group('client boundary', () {
    test('no translated string is sent as request data', () {
      final offenders = <String>[];

      for (final file in _dartFilesUnder(_libDirectory().path)) {
        final lines = file.readAsLinesSync();
        for (var i = 0; i < lines.length; i++) {
          final line = lines[i];
          if (!line.contains('.tr()')) continue;
          final isPayload = line.contains('data:') ||
              line.contains('notes:') ||
              line.contains('reason:') ||
              line.contains('payload');
          if (isPayload) {
            offenders.add('${file.path}:${i + 1}  ${line.trim()}');
          }
        }
      }

      expect(offenders, isEmpty,
          reason: 'a localized sentence in a request body becomes part of an '
              'audit record; send a stable code and localize it for display');
    });

    test('staff-ness is never inferred from the primary auth method', () {
      final offenders = <String>[];

      for (final file in _dartFilesUnder(_libDirectory().path)) {
        final lines = file.readAsLinesSync();
        for (var i = 0; i < lines.length; i++) {
          final line = lines[i];
          final infersStaff = line.contains('primaryAuthMethod') &&
              (line.contains('isStaff') || line.contains("== 'Password'"));
          if (infersStaff) {
            offenders.add('${file.path}:${i + 1}  ${line.trim()}');
          }
        }
      }

      expect(offenders, isEmpty,
          reason: 'an auth method is not a role; use the server-computed '
              'canManageStaffSecurity flag');
    });

    test('a raw roster status is never used directly as a chip label', () {
      final offenders = <String>[];

      for (final file in _dartFilesUnder(_libDirectory().path)) {
        final lines = file.readAsLinesSync();
        for (var i = 0; i < lines.length; i++) {
          final line = lines[i];
          // Reading the server code is fine; rendering it raw is the defect. So the
          // guard looks for the code flowing straight into a label, not for
          // the read itself.
          final readsThenRenders = line.contains("volunteer['rosterStatus']") &&
              !line.contains('_rosterStatusLabel') &&
              line.contains('AppStatusChip');
          if (readsThenRenders) {
            offenders.add('${file.path}:${i + 1}  ${line.trim()}');
          }

          final chipWithoutLabel = line.contains('AppStatusChip(') &&
              line.contains('label: status') &&
              !line.contains('_rosterStatusLabel');
          if (chipWithoutLabel) {
            offenders.add('${file.path}:${i + 1}  ${line.trim()}');
          }
        }
      }

      expect(offenders, isEmpty,
          reason: 'map the server status code through a localized label helper');
    });
  });
}