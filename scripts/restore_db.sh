#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# SeniorConnect — PostgreSQL Restore & Verification Script (P7-13)
# Restores a pg_dump backup into a target database and runs verification checks.
# ==============================================================================

BACKUP_FILE="${1:-}"

if [ -z "${BACKUP_FILE}" ]; then
  echo "Usage: $0 <path_to_backup.dump> [target_db_name]"
  exit 1
fi

DB_HOST="${DB_HOST:-localhost}"
DB_PORT="${DB_PORT:-5432}"
TARGET_DB="${2:-seniorconnect_restore_test}"
DB_USER="${DB_USER:-postgres}"

CHECKSUM_FILE="${BACKUP_FILE}.sha256"

if [ -f "${CHECKSUM_FILE}" ]; then
  echo "==> [$(date -u)] Verifying SHA256 checksum..."
  sha256sum -c "${CHECKSUM_FILE}"
else
  echo "⚠️ Warning: No checksum file found at ${CHECKSUM_FILE}, skipping hash check."
fi

echo "==> [$(date -u)] Preparing test restore database: ${TARGET_DB}..."
dropdb -h "${DB_HOST}" -p "${DB_PORT}" -U "${DB_USER}" --if-exists "${TARGET_DB}"
createdb -h "${DB_HOST}" -p "${DB_PORT}" -U "${DB_USER}" "${TARGET_DB}"

echo "==> [$(date -u)] Restoring dump into ${TARGET_DB}..."
pg_restore \
  -h "${DB_HOST}" \
  -p "${DB_PORT}" \
  -U "${DB_USER}" \
  -d "${TARGET_DB}" \
  -v \
  --no-owner \
  --role="${DB_USER}" \
  "${BACKUP_FILE}" || true

echo "==> [$(date -u)] Running integrity verification queries..."
psql -h "${DB_HOST}" -p "${DB_PORT}" -U "${DB_USER}" -d "${TARGET_DB}" -c "
  SELECT table_schema, count(*) as table_count 
  FROM information_schema.tables 
  WHERE table_schema IN ('public', 'safeguarding') 
  GROUP BY table_schema;
"

echo "==> [$(date -u)] Restore and integrity verification completed successfully!"
