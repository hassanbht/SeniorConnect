# Löschkonzept & Aufbewahrungsrichtlinie (Data Retention Policy) gemäß Art. 17 DSGVO

> **SeniorConnect Plattform**  
> **Stand:** August 2026

---

## 1. Zweistufiges Löschmodell (Two-Tier Deletion — BR-GDPR-04)

Um den Anforderungen der DSGVO (Recht auf Vergessenwerden) sowie den handels- und steuerrechtlichen Aufbewahrungspflichten für Nachweise gerecht zu werden, implementiert SeniorConnect ein **zweistufiges Löschmodell**:

### Stufe 1: Deaktivierung (Soft Delete)
- **Auslöser:** Nutzer beantragt Kontolöschung (`/api/v1/privacy/account-deletion:request`).
- **Wirkung:** Sofortige Sperre aller aktiven Hilfsangebote, Beendigung offener Anfragen, Deaktivierung des Kontozugriffs.
- **Widerrufsfrist:** 30 Tage Bedenkzeit, in der das Konto reaktiviert werden kann.

### Stufe 2: Vollständige Anonymisierung & Bereinigung (Tier-2 Hard Purge)
- **Auslöser:** Ablauf der 30-Tage-Frist bzw. automatischer periodischer Retention-Job (`DataMaintenanceHostedService`).
- **Wirkung:** 
  - Alle personenbezogenen Daten (`User.DisplayName`, `Phone`, `Email`, `Address`, `Notes`) werden unwiderruflich überschrieben (`user.AnonymizeForGdpr()`).
  - Verknüpfte Profildaten und temporäre Zustimmungen werden physisch entfernt.
  - Statistische Aggregate und anonyme Kennzahlen (z.B. für Funder-Reports) bleiben ohne Personenbezug erhalten.

---

## 2. Aufbewahrungsfristen

| Datenbereich | Aufbewahrungsfrist | Rechtliche Grundlage |
| :--- | :--- | :--- |
| **Inaktive Konten ohne Löschantrag** | 24 Monate Inaktivität $\to$ Benachrichtigung & Bereinigung | Art. 5 Abs. 1 lit. e DSGVO |
| **Audit- & Revisionsprotokolle** | 3 Jahre (nach 30 Tagen pseudonymisiert) | Revisionssicherheit / Haftungsansprüche |
| **Safeguarding-Fallakten** | 5 Jahre nach Schließung des Schutzfalls | Rechtlicher Schutz vulnerabler Personen |
| **Aggregierte Funder-Reports** | Dauerhaft (vollständig anonymisiert, Kohorten $\ge 10$) | Nicht personenbezogen |
