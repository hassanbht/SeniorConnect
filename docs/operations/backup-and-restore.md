# Backup & Restore Betriebskonzept (P7-13)

> **Plattform:** SeniorConnect / Mitanand  
> **Datenbank:** PostgreSQL 16  
> **Stand:** August 2026

---

## 1. Backup-Strategie

- **Tägliche Vollsicherungen (Daily Full Dumps):** Automatische Ausführung über Cronjob mittels `scripts/backup_db.sh` im komprimierten Custom-Archive-Format (`pg_dump -Fc`).
- **Integritätsprüfung (SHA-256):** Jedes Backup erzeugt eine kryptografische Prüfsummendatei (`.sha256`), die vor dem Restore validiert wird.
- **Verschlüsselung & Aufbewahrung:** Backups werden im Ruhezustand (AES-256) auf getrenntem Backup-Speicher in der EU vorgehalten. Aufbewahrungsdauer: 30 Tage rotierend.
- **Safeguarding-Schema-Erhalt:** Das getrennte Schema `safeguarding` wird konsistent mitgesichert.

---

## 2. Restore- & Desaster-Recovery-Prozess

### Wiederherstellungsschritte
1. **Prüfsumme prüfen:** `sha256sum -c seniorconnect_db_*.dump.sha256`
2. **Wiederherstellung in Zielinstanz ausführen:**
   ```bash
   ./scripts/restore_db.sh /path/to/backup.dump seniorconnect_target_db
   ```
3. **Integritätskontrolle:**
   - Tabellenanzahl in Schema `public` und `safeguarding` prüfen.
   - EF Core Migration-Historie (`__EFMigrationsHistory`) verifizieren.
   - Stichprobenprüfung auf anonymisierte User-Datensätze und Audit-Logs.

---

## 3. RTO / RPO Ziele

- **RPO (Recovery Point Objective):** $\le 24$ Stunden (für Standard-Einsatz) bzw. $\le 1$ Stunde mit aktivem WAL-Archiving.
- **RTO (Recovery Time Objective):** $\le 30$ Minuten bis zur vollen Betriebsbereitschaft.
