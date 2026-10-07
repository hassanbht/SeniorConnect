# Auftragsverarbeitungsvertrag (AVV / DPA) gemäß Art. 28 DSGVO (Muster)

> **Vertragspartner:**
> 1. **Auftraggeber (Verantwortlicher):** [Gemeinde / Trägerorganisation / Verein]
> 2. **Auftragnehmer (Auftragsverarbeiter):** SeniorConnect Plattformbetrieb GmbH / Verein

---

## 1. Gegenstand und Dauer der Vereinbarung

1. Der Auftragnehmer verarbeitet personenbezogene Daten im Auftrag des Verantwortlichen zur Bereitstellung der SeniorConnect-Softwareplattform für Nachbarschaftshilfe und Freiwilligenkoordination.
2. Die Laufzeit richtet sich nach der Hauptvereinbarung über die Nutzung der Plattform.

---

## 2. Art und Zweck der Verarbeitung, Kategorien der Daten

- **Zwecke:** Bereitstellung der Softwareplattform, Verwaltung von Freiwilligen-Rostern, Zuteilung von Hilfsanfragen, revisionssichere Erfassung von Einsatzstunden, Erstellung anonymisierter Gemeindeberichte.
- **Datenkategorien:** Stammdaten (Name, Telefonnummer, E-Mail), Profildaten (Sprachen, Interessen), Aktivitätsdaten (Datum, Einsatzkategorie, Dauer), Verifikationsstatus.
- **Betroffene Personen:** Hilfesuchende, Ehrenamtliche, Angehörige, Koordinatoren.

---

## 3. Pflichten des Auftragnehmers

1. **Weisungsgebundene Verarbeitung:** Verarbeitung erfolgt ausschließlich auf dokumentierte Weisung des Verantwortlichen.
2. **Vertraulichkeit:** Alle mit der Datenverarbeitung betrauten Personen sind zur Vertraulichkeit verpflichtet.
3. **Technische und organisatorische Maßnahmen (TOMs):**
   - Verschlüsselung von Daten im Ruhezustand (AES-256) und bei der Übertragung (TLS 1.3).
   - Rollen- und berechtigungsbasierte Zugriffskontrollen (RBAC).
   - Revisionssichere Audit-Protokolle bei zustimmungsrelevanten Vorgängen.
   - Isolierte Speicherung von Schutzdaten (`safeguarding`).
4. **Unterauftragsverarbeiter:** Der Einsatz von Unterauftragsverarbeitern bedarf der Zustimmung des Auftraggebers. Hosting-Provider befinden sich ausnahmslos in der EU.
5. **Unterstützung bei Betroffenenrechten:** Der Auftragnehmer stellt Schnittstellen zur Auskunft (Datenexport JSON/PDF) und Löschung zur Verfügung.

---

## 4. Löschung und Rückgabe von Daten

Nach Beendigung der Vereinbarung werden alle im Auftrag verarbeiteten Daten nach Wahl des Verantwortlichen datenschutzkonform gelöscht oder übergeben, sofern keine gesetzlichen Aufbewahrungspflichten entgegenstehen.
