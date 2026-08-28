# Datenschutz-Folgenabschätzung (DPIA / DSFA) gemäß Art. 35 DSGVO

> **Projekt:** SeniorConnect / Mitanand Plattform  
> **Gegenstand:** Vermittlung von Nachbarschaftshilfe und gemeinschaftlichen Aktivitäten mit schutzbedürftigen Personen (Senioren)  
> **Stand:** August 2026 — Version 1.0 (Pilot Readiness)

---

## 1. Notwendigkeit der DSFA

Gemäß Art. 35 Abs. 1 und Abs. 3 DSGVO sowie der Blacklist der österreichischen Datenschutzbehörde (DSB) ist eine Datenschutz-Folgenabschätzung erforderlich, da:
1. **Verarbeitung von Daten vulnerabler Personen:** Seniorinnen und Senioren, teils mit eingeschränkter digitaler Kompetenz oder Hilfsbedürftigkeit.
2. **Systematische Erfassung von Interaktionen:** Aufzeichnung von Einsatzorten, Zeitpunkten, Begleitdiensten und Betreuungshistorien.
3. **Automatisierte Eignungs- und Vertrauensbewertung (Matching):** Regelbasierte Vorauswahl von Hilfskräften und Einsatzberechtigungen (Trust Levels 0–5).

---

## 2. Beschreibung der Verarbeitungsvorgänge

### 2.1 Datenflüsse und Zwecke
- **Identifikation & Authentifizierung:** Passwortloses Login via Einmal-Passwort (SMS/E-Mail-OTP) oder ID Austria.
- **Hilfeanfragen & Matching:** Erfassung von Hilfebedarfen im Alltag (Einkauf, Arztbegleitung, Behördengänge, Freizeit). Ausschluss medizinischer/pflegerischer Tätigkeiten.
- **Standort & Schutz:** Grobe Umkreissuche ohne kontinuierliches Hintergrund-Tracking. Kontaktdatenfreigabe erst nach beidseitiger Zuteilung (`BR-COMM-04`).
- **Safeguarding (Schutzfälle):** Isolierte Protokollierung und Fallbearbeitung von Vorfällen durch geprüfte Vertrauenspersonen (`SafeguardingDbContext`).

### 2.2 Datenminimierung (Privacy by Design)
- **Keine Speicherung von Ausweisdokumenten oder Strafregisterauszügen (`BR-TRUST-05`):** Es wird ausschließlich das Prüfergebnis (Status `Verified`, Prüfer, Ablaufdatum) gespeichert.
- **Kein Drittlandtransfer:** Hosting und Datenverarbeitung ausschließlich innerhalb der Europäischen Union (Österreich/Deutschland).
- **Keine Tracking- oder Werbe-SDKs:** Verzicht auf Google Analytics, Meta Pixel oder Werbe-Identifier.

---

## 3. Bewertung der Risiken für Rechte und Freiheiten

| Risiko-Szenario | Eintrittswahrscheinlichkeit | Schadensausmaß | Geplante Schutzmaßnahmen | Restrisiko |
| :--- | :---: | :---: | :--- | :---: |
| **Unbefugter Zugriff auf sensible Adressdaten** | Gering | Hoch | Kontaktdaten werden erst nach Zuteilung aufgedeckt. Maskierte Rufnummern optional. | Sehr gering |
| **Missbrauch des Vertrauensverhältnisses bei Hausbesuchen** | Mittel | Sehr hoch | Strikte Trust-Level-Hierarchie (Level 3+ erfordert Identitäts- & Strafregisterprüfung sowie Buddy-System bei den ersten 3 Einsätzen). | Gering |
| **Einblick von Angehörigen in intime Hilfebedarfe ohne Zustimmung** | Gering | Mittel | Granulare, jederzeit widerrufbare Berechtigungen (`BR-FAMILY-01..04`). Transparenter Zugriffs-Audit ("Wer hat was gesehen?"). | Gering |
| **Safeguarding-Datenleck an gewöhnliche Administratoren** | Sehr gering | Kritisch | Schema- und DbContext-Isolation, strikte Rollentrennung, lückenloses Access-Logging. | Sehr gering |

---

## 4. Fazit & Freigabe

Die technischen und organisatorischen Maßnahmen (TOMs) entsprechen dem Stand der Technik und gewährleisten ein dem Risiko angemessenes Schutzniveau. Die Verarbeitung ist für den Pilotbetrieb zulässig.
