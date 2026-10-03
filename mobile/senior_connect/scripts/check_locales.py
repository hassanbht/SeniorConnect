#!/usr/bin/env python3
"""
Locale key parity checker for SeniorConnect.

German (de.json) is the source of truth. Every key in de.json must exist in
every other locale file, with the same placeholder set.

It also scans lib/**/*.dart for literal keys used via 'key'.tr(), tr('key'),
context.tr('key') and 'key'.plural(...) and fails if one is missing from
de.json. Keys built by string interpolation are ignored.

Run in CI:
    python3 mobile/senior_connect/scripts/check_locales.py mobile/senior_connect/assets/translations

Exit code 1 on any mismatch. A missing key is a build-breaking defect
(see mobile/AGENTS.md).
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

SOURCE = "de"
LOCALES = ["de", "en", "fa"]
PLACEHOLDER = re.compile(r"\{(\w+)\}")

# A literal key: no quotes, no newline, no interpolation ($ or braces).
_KEY = r"""[^'"$\n{}]+"""
# 'key'.tr(  /  'key'.plural(
_KEY_METHOD = re.compile(r"""(['"])(""" + _KEY + r""")\1\s*\.\s*(tr|plural)\(""")
# tr('key'  /  context.tr('key'  /  plural('key'
_KEY_CALL = re.compile(r"""\b(tr|plural)\(\s*(['"])(""" + _KEY + r""")\2""")


def used_keys(lib: Path) -> dict[str, tuple[str, str]]:
    """Literal localization keys used in Dart code -> (kind, first location)."""
    found: dict[str, tuple[str, str]] = {}
    for dart in sorted(lib.rglob("*.dart")):
        text = dart.read_text(encoding="utf-8")
        hits = [(m.group(2), m.group(3), m.start()) for m in _KEY_METHOD.finditer(text)]
        hits += [(m.group(3), m.group(1), m.start()) for m in _KEY_CALL.finditer(text)]
        for key, kind, pos in hits:
            line = text.count("\n", 0, pos) + 1
            found.setdefault(key, (kind, f"{dart.as_posix()}:{line}"))
    return found


def check_usage(lib: Path, source_json: dict, source_flat: dict[str, str]) -> bool:
    """True if every literal key used in lib/ exists in the source locale."""
    ok = True
    for key, (kind, where) in sorted(used_keys(lib).items()):
        if key in source_flat:
            continue
        # plural() may point at an object holding zero/one/other forms.
        node: object = source_json
        for part in key.split("."):
            node = node.get(part) if isinstance(node, dict) else None
        if kind == "plural" and isinstance(node, dict):
            continue
        print(f"FAIL  {SOURCE}.json  missing key used in code: {key}  ({where})")
        ok = False
    return ok


def flatten(obj: dict, prefix: str = "") -> dict[str, str]:
    out: dict[str, str] = {}
    for key, value in obj.items():
        path = f"{prefix}.{key}" if prefix else key
        if isinstance(value, dict):
            out.update(flatten(value, path))
        elif isinstance(value, str):
            out[path] = value
        else:
            out[path] = str(value)
    return out


def main() -> int:
    root = Path(sys.argv[1] if len(sys.argv) > 1 else "assets/translations")
    flat: dict[str, dict[str, str]] = {}
    raw: dict[str, dict] = {}

    for code in LOCALES:
        path = root / f"{code}.json"
        if not path.exists():
            print(f"FAIL  missing locale file: {path}")
            return 1
        try:
            raw[code] = json.loads(path.read_text(encoding="utf-8"))
            flat[code] = flatten(raw[code])
        except json.JSONDecodeError as exc:
            print(f"FAIL  {path} is not valid JSON: {exc}")
            return 1

    source = flat[SOURCE]
    failed = False

    for code in LOCALES:
        if code == SOURCE:
            continue

        target = flat[code]

        missing = sorted(set(source) - set(target))
        extra = sorted(set(target) - set(source))

        for key in missing:
            print(f"FAIL  {code}.json  missing key: {key}")
            failed = True
        for key in extra:
            print(f"FAIL  {code}.json  key not in {SOURCE}.json: {key}")
            failed = True

        for key in sorted(set(source) & set(target)):
            want = set(PLACEHOLDER.findall(source[key]))
            got = set(PLACEHOLDER.findall(target[key]))
            if want != got:
                print(
                    f"FAIL  {code}.json  {key}: placeholders "
                    f"{sorted(want)} != {sorted(got)}"
                )
                failed = True

        empty = [k for k, v in target.items() if not v.strip()]
        for key in empty:
            print(f"FAIL  {code}.json  empty value: {key}")
            failed = True

    # lib/ sits next to assets/ (senior_connect/assets/translations).
    lib = root.resolve().parent.parent / "lib"
    if lib.is_dir():
        if not check_usage(lib, raw[SOURCE], source):
            failed = True
    else:
        print(f"WARN  {lib} not found, skipped code usage scan")

    if failed:
        return 1

    print(f"OK  {len(source)} keys, {len(LOCALES)} locales, all consistent")
    return 0


if __name__ == "__main__":
    sys.exit(main())
