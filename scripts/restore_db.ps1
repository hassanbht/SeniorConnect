# ==============================================================================
# SeniorConnect — PowerShell Restore & Verification Script (P7-13)
# ==============================================================================

param(
    [Parameter(Mandatory=$true)]
    [string]$BackupFile,
    [string]$TargetDb = "seniorconnect_restore_test",
    [string]$DbHost = $(if ($env:DB_HOST) { $env:DB_HOST } else { "localhost" }),
    [string]$DbPort = $(if ($env:DB_PORT) { $env:DB_PORT } else { "5432" }),
    [string]$DbUser = $(if ($env:DB_USER) { $env:DB_USER } else { "postgres" })
)

$ErrorActionPreference = "Stop"

if (!(Test-Path -Path $BackupFile)) {
    throw "Backup file not found: $BackupFile"
}

$ChecksumFile = "${BackupFile}.sha256"
if (Test-Path -Path $ChecksumFile) {
    Write-Host "==> Verifying SHA256 checksum..."
    $ExpectedHash = (Get-Content -Path $ChecksumFile).Split(' ')[0]
    $ActualHash = (Get-FileHash -Path $BackupFile -Algorithm SHA256).Hash
    if ($ExpectedHash -ne $ActualHash) {
        throw "Checksum mismatch! Expected: $ExpectedHash, Got: $ActualHash"
    }
    Write-Host "    Checksum verified: $ActualHash"
}

Write-Host "==> Restoring dump into database $TargetDb..."
& dropdb -h $DbHost -p $DbPort -U $DbUser --if-exists $TargetDb
& createdb -h $DbHost -p $DbPort -U $DbUser $TargetDb

& pg_restore -h $DbHost -p $DbPort -U $DbUser -d $TargetDb -v --no-owner $BackupFile
Write-Host "==> Verifying table schema counts..."
& psql -h $DbHost -p $DbPort -U $DbUser -d $TargetDb -c "SELECT table_schema, count(*) FROM information_schema.tables WHERE table_schema IN ('public', 'safeguarding') GROUP BY table_schema;"

Write-Host "==> Restore test finished successfully!"
