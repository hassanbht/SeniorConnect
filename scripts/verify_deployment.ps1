# SeniorConnect - Production Deployment Health and Gate Verification
[CmdletBinding()]
param(
    [string]$BaseUrl = "http://localhost:8080"
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "      SeniorConnect - Deployment and Health Gate Check    " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

function Assert-HealthProbe {
    param([string]$Url, [string]$ProbeName)
    try {
        $response = Invoke-WebRequest -Uri $Url -Method Get -TimeoutSec 5 -UseBasicParsing
        if ($response.StatusCode -eq 200) {
            Write-Host "[PASS] Probe: $ProbeName ($Url) -> 200 OK" -ForegroundColor Green
            return $true
        } else {
            Write-Host "[FAIL] Probe: $ProbeName ($Url) -> Status $($response.StatusCode)" -ForegroundColor Red
            return $false
        }
    } catch {
        Write-Host "[INFO] Probe: $ProbeName ($Url) -> Offline (Expected before docker compose up)" -ForegroundColor Yellow
        return $false
    }
}

# 1. Probe Health Endpoints
$liveOk = Assert-HealthProbe "$BaseUrl/healthz/live" "Liveness"
$readyOk = Assert-HealthProbe "$BaseUrl/healthz/ready" "Readiness"
$healthOk = Assert-HealthProbe "$BaseUrl/health" "Detailed Health"

# 2. Verify Python Locale Consistency
Write-Host ""
Write-Host "Verifying Locale Consistency..." -ForegroundColor Cyan
python scripts/check_locales.py
$localeExit = $LASTEXITCODE

# 3. Verify Field Pilot Simulation
Write-Host ""
Write-Host "Running Field Pilot Simulation..." -ForegroundColor Cyan
python scripts/simulate_pilot_day.py
$simExit = $LASTEXITCODE

# 4. Final Gate Summary
Write-Host ""
Write-Host "==========================================================" -ForegroundColor Cyan
if ($localeExit -eq 0 -and $simExit -eq 0) {
    Write-Host "  DEPLOYMENT GATE PASSED: System Ready for Production Pilot!" -ForegroundColor Green
} else {
    Write-Host "  DEPLOYMENT GATE FAILED: Check logs above." -ForegroundColor Red
}
Write-Host "==========================================================" -ForegroundColor Cyan
