#!/usr/bin/env python3
"""
Locale consistency verification script for SeniorConnect.
Verifies that all keys present in the source locale (de.json)
are also present in all other supported locales (en.json, fa.json).
"""
import json
import os
import sys

def flatten_keys(d, prefix=""):
    keys = set()
    for k, v in d.items():
        full_key = f"{prefix}.{k}" if prefix else k
        if isinstance(v, dict):
            keys.update(flatten_keys(v, full_key))
        else:
            keys.add(full_key)
    return keys

def main():
    base_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    translations_dir = os.path.join(base_dir, "mobile", "senior_connect", "assets", "translations")
    
    locales = ["de.json", "en.json", "fa.json"]
    loaded = {}

    for loc in locales:
        p = os.path.join(translations_dir, loc)
        if not os.path.exists(p):
            print(f"ERROR: Missing locale file {p}")
            sys.exit(1)
        with open(p, "r", encoding="utf-8") as f:
            loaded[loc] = flatten_keys(json.load(f))

    source_keys = loaded["de.json"]
    print(f"Source locale (de.json) has {len(source_keys)} keys.")

    errors = 0
    for loc, keys in loaded.items():
        if loc == "de.json":
            continue
        missing = source_keys - keys
        extra = keys - source_keys
        if missing:
            print(f"ERROR: {loc} is missing {len(missing)} keys from de.json:")
            for k in sorted(missing)[:10]:
                print(f"  - {k}")
            if len(missing) > 10:
                print(f"  ... and {len(missing) - 10} more")
            errors += 1
        if extra:
            print(f"WARNING: {loc} has {len(extra)} extra keys not in de.json:")
            for k in sorted(extra)[:5]:
                print(f"  + {k}")

    if errors == 0:
        print(f"OK: All {len(locales)} locales ({', '.join(locales)}) are consistent ({len(source_keys)} keys).")
        sys.exit(0)
    else:
        print(f"FAILED: {errors} locale(s) have missing keys.")
        sys.exit(1)

if __name__ == "__main__":
    main()
