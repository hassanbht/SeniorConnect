#!/usr/bin/env python3
"""
Locale key parity checker for SeniorConnect.

German (de.json) is the source of truth. Every key in de.json must exist in
every other locale file, with the same placeholder set.

Run in CI:
    python3 tools/check_locales.py assets/translations

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

    for code in LOCALES:
        path = root / f"{code}.json"
        if not path.exists():
            print(f"FAIL  missing locale file: {path}")
            return 1
        try:
            flat[code] = flatten(json.loads(path.read_text(encoding="utf-8")))
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

    if failed:
        return 1

    print(f"OK  {len(source)} keys, {len(LOCALES)} locales, all consistent")
    return 0


if __name__ == "__main__":
    sys.exit(main())
