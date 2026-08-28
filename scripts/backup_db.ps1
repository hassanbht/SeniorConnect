# ==============================================================================
# SeniorConnect — PowerShell Backup Script (P7-13)
# ==============================================================================

param(
    [string]$DbHost = $(if ($env:DB_HOST) { $env:DB_HOST } else { "localhost" }),
    [string]$DbPort = $(if ($env:DB_PORT) { $env:DB_PORT } else { "5432" }),
    [string]$DbName = $(if ($env:DB_NAME) { $env:DB_NAME } else { "seniorconnect_db" }),
    [string]$DbUser = $(if ($env:DB_USER) { $env:DB_USER } else { "postgres" }),
    [string]$BackupDir = "./backups"
)

$ErrorActionPreference = "Stop"

$Timestamp = (Get-Date).ToUniversalTime().ToString("yyyyMMdd_HHmmssZ")
if (!(Test-Path -Path $BackupDir)) {
    New-Item -ItemType Directory -Path $BackupDir | Out-Null
}

$BackupFile = Join-Path $BackupDir "${DbName}_${Timestamp}.dump"
$ChecksumFile = "${BackupFile}.sha256"

Write-Host "==> Starting backup for database: $DbName on ${DbHost}:${DbPort}..."

& pg_dump -h $DbHost -p $DbPort -U $DbUser -F c -b -v -f $BackupFile $DbName
if ($LASTEXITCODE -ne 0) {
    throw "pg_dump failed with exit code $LASTEXITCODE"
}

$Hash = (Get-FileHash -Path $BackupFile -Algorithm SHA256).Hash
Set-Content -Path $ChecksumFile -Value "$Hash  $(Split-Path -Leaf $BackupFile)"

Write-Host "==> Backup completed successfully!"
Write-Host "    Archive:  $BackupFile"
Write-Host "    Checksum: $Hash"
