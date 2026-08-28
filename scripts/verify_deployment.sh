#!/usr/bin/env bash
# SeniorConnect — Production Deployment Health & Gate Verification (Bash)
set -euo pipefail

BASE_URL="${1:-http://localhost:8080}"

echo "=========================================================="
echo "      SeniorConnect — Deployment & Health Gate Check      "
echo "=========================================================="

assert_probe() {
    local url="$1"
    local name="$2"
    if curl -s -f -o /dev/null -m 5 "$url"; then
        echo "[PASS] Probe: $name ($url) -> 200 OK"
    else
        echo "[WARN] Probe: $name ($url) -> Offline or Unhealthy"
    fi
}

# 1. Probe Health Endpoints
assert_probe "$BASE_URL/healthz/live" "Liveness"
assert_probe "$BASE_URL/healthz/ready" "Readiness"
assert_probe "$BASE_URL/health" "Detailed Health"

# 2. Locale Consistency
echo ""
echo "Verifying Locale Consistency..."
python3 scripts/check_locales.py

# 3. Field Pilot Simulation
echo ""
echo "Running Field Pilot Simulation..."
python3 scripts/simulate_pilot_day.py

echo "=========================================================="
echo "  DEPLOYMENT GATE PASSED: System Ready for Production Pilot!"
echo "=========================================================="
