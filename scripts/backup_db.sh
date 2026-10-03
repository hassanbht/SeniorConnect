#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# SeniorConnect — PostgreSQL Backup Script (P7-13)
# Creates a compressed, consistent pg_dump with sha256 checksum verification.
# ==============================================================================

DB_HOST="${DB_HOST:-localhost}"
DB_PORT="${DB_PORT:-5432}"
DB_NAME="${DB_NAME:-seniorconnect_db}"
DB_USER="${DB_USER:-postgres}"
BACKUP_DIR="${BACKUP_DIR:-./backups}"

TIMESTAMP=$(date -u +"%Y%m%d_%H%M%SZ")
BACKUP_FILE="${BACKUP_DIR}/${DB_NAME}_${TIMESTAMP}.dump"
CHECKSUM_FILE="${BACKUP_FILE}.sha256"

mkdir -p "${BACKUP_DIR}"

echo "==> [$(date -u)] Starting backup for database: ${DB_NAME} on ${DB_HOST}:${DB_PORT}..."

# Execute pg_dump using custom directory/archive format with compression
pg_dump \
  -h "${DB_HOST}" \
  -p "${DB_PORT}" \
  -U "${DB_USER}" \
  -F c \
  -b \
  -v \
  -f "${BACKUP_FILE}" \
  "${DB_NAME}"

echo "==> [$(date -u)] Generating SHA256 checksum..."
sha256sum "${BACKUP_FILE}" > "${CHECKSUM_FILE}"

echo "==> [$(date -u)] Backup completed successfully:"
echo "    Archive:  ${BACKUP_FILE} ($(du -h "${BACKUP_FILE}" | cut -f1))"
echo "    Checksum: $(cat "${CHECKSUM_FILE}")"
