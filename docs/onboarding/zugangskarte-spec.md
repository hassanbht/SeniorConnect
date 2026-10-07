# Spezifikation: Gedruckte Zugangskarte (Zugangskarte für Seniorinnen & Senioren)

> **Referenz:** `P7-17` / `P6-04` — Benutzerfreundliches Onboarding ohne Passwort
> **Zielgruppe:** Seniorinnen und Senioren sowie Angehörige

---

## 1. Zweck und Anwendungsfall

Um Barrieren für ältere Menschen ohne E-Mail-Konto oder mit eingeschränkter Smartphone-Erfahrung zu minimieren, wird das Konto durch Angehörige oder die Koordination vorbereitet. Der Zugang erfolgt über eine physisch gedruckte Karte (Kreditkartenformat oder DIN A6 Faltkarte).

---

## 2. Visueller Aufbau der Zugangskarte

```
┌─────────────────────────────────────────────────────────────┐
│  Mitanand / SeniorConnect                Gemeinde Salzburg  │
│                                                             │
│  Ihre persönliche Zugangskarte                              │
│                                                             │
│  Name: Elisabeth Huber                                      │
│  Telefon: 0664 / 123 45 67                                  │
│                                                             │
│  ┌──────────────┐     Ihr persönlicher Anmeldecode:         │
│  │              │                                           │
│  │   [QR-CODE]  │     ┌───┐ ┌───┐ ┌───┐ ┌───┐ ┌───┐ ┌───┐   │
│  │              │     │ 4 │ │ 8 │ │ 2 │ │ 9 │ │ 1 │ │ 5 │   │
│  │              │     └───┘ └───┘ └───┘ └───┘ └───┘ └───┘   │
│  └──────────────┘                                           │
│                       Gültig für die erste Anmeldung        │
│                                                             │
│  Hilfe & Unterstützung durch die Koordination:              │
│  Telefon: 0662 / 8072-0 · Mo–Fr 08:00–12:00 Uhr             │
└─────────────────────────────────────────────────────────────┘
```

---

## 3. Technische Umsetzung

1. **QR-Code-Payload:**
   `https://app.seniorconnect.local/onboarding?code=482915&phone=+436641234567`
2. **Kamera-Erkennung in der App:**
   Automatisches Ausfüllen der Telefonnummer und des Einmal-Codes bei Scan.
3. **Sicherheitsregeln:**
   - Der 6-stellige Code ist zeitlich befristet (z.B. 7 Tage) und wird nach einmaliger erfolgreicher Aktivierung ungültig.
   - Keine Passwörter auf der Karte aufgedruckt (`BR-AUTH-03`).
