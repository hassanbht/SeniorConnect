#!/usr/bin/env python3
"""
Locale check for SeniorConnect — thin wrapper.

The real checker (key parity, placeholders, and keys used in lib/**/*.dart
that are missing from de.json) lives at
mobile/senior_connect/scripts/check_locales.py, which is what CI runs.
This wrapper keeps `python scripts/check_locales.py` working from the repo root.
"""
import runpy
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
APP = ROOT / "mobile" / "senior_connect"

if __name__ == "__main__":
    sys.argv = [str(APP / "scripts" / "check_locales.py"), str(APP / "assets" / "translations")]
    runpy.run_path(sys.argv[0], run_name="__main__")
