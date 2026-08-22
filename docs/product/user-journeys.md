# User Journeys

> These are the flows that must work. Every screen in the app should be traceable to a step
> in one of these journeys. If a screen is not in a journey, question why it exists.

---

## J1 — Family-led onboarding *(the most important journey in the product)*

**Actor:** Sabine (family caregiver) → Maria (senior)

```
1. Sabine installs the app, registers, verifies email + phone.
2. "Für wen sind Sie hier?" → „Ich möchte jemanden unterstützen."
3. Creates a senior account for Maria: name, address, phone, languages,
   interests, mobility notes (functional, never diagnostic).
4. Chooses which permissions she wants; the app states clearly that
   Maria must confirm them.
5. Receives a printable/shareable Zugangskarte with a large code and a QR.
6. Maria's phone: install → scan QR (or type the 6-digit code) → SMS code → in.
7. Maria's first screen is ALREADY populated: her name, her daughter,
   the walking group nearby, one suggested activity.
8. Maria confirms Sabine's permissions with one screen, one tap per permission,
   in Sie-form, in large type.
```

**Failure modes to design for:** Maria cannot scan a QR (offer the code), Maria has no
smartphone (Phase 8 IVR; in the meantime an organization-assisted account), Maria says no
to permissions (everything must still work).

**Success metric:** > 60 % of senior accounts in the pilot are created by a family member.

---

## J2 — Senior requests everyday help

**Actor:** Maria

```
1. Home screen: four buttons. She taps „Hilfe anfragen" (largest, first).
2. "Wobei brauchen Sie Hilfe?" — 6 large picture-and-word cards:
   Einkaufen · Arzttermin · Behördengang · Begleitung/Spaziergang ·
   Kleine Hilfe zu Hause · Etwas anderes
3. "Wann?" — Heute · Morgen · Diese Woche · Anderes Datum
   (Then a large time picker with sensible defaults.)
4. "Möchten Sie etwas dazu sagen?" — optional, with a microphone button.
5. One review screen, plain sentences: „Sie brauchen am Dienstag um 10:00
   jemanden, der Sie zum Arzt begleitet. Stimmt das?"
6. „Ja, anfragen" → confirmation with an honest expectation:
   „Wir suchen jemanden. Sie hören spätestens morgen von uns."
7. Notification when a volunteer is found: name, photo, what was verified,
   arrival time. „Passt das für Sie?" → Ja / Nein, jemand anderen.
8. Reminder 24 h and 2 h before.
9. Volunteer arrives, checks in. Optional: Sabine gets a notification.
10. After: „Wie war es?" — Gut / Nicht so gut / Ich möchte etwas melden.
```

**Rules in play:** BR-SCOPE-02/03/04 (blocked categories and emergency at step 2–4),
BR-SAFETY-01 (safety level assigned server-side, never shown as a choice),
BR-HELP-01 (state machine).

**Constraint:** maximum **5 taps** from home screen to a submitted request.

---

## J3 — Volunteer finds and completes an activity

**Actor:** Anna

```
1. Home: „In Ihrer Nähe" — a list, sorted by match score, each card showing:
   what · when · how long · how far · safety level · who asks (first name + area).
2. Filter: only what I am eligible for (default ON). Toggling it off shows
   ineligible items greyed out WITH the reason and the path to eligibility.
3. Detail → „Ich übernehme das."
4. Atomic assignment. If someone was faster: a clear, non-blaming message
   and three similar suggestions.
5. Now she sees the exact address and phone number (not before — BR-COMM-04).
6. Reminder at T-24h and T-2h with one-tap „Ich komme" / „Ich schaffe es leider nicht".
7. On arrival: Check-in (large button, time-bounded, no background tracking).
8. Check-out → duration recorded → hours credited.
9. „Alles in Ordnung?" → Ja / „Ich möchte etwas melden" (→ safeguarding, one tap).
```

---

## J4 — Senior creates a community group *(seniors as providers)*

**Actor:** Thomas, 69

```
1. „Gruppe gründen"
2. Was? (title, category, description)
3. Wann? — one-off or recurring (weekly / biweekly / monthly)
4. Wo? — place name + optional map pin
5. Wer darf mitmachen? — Alle · Nur auf Anfrage · Nur Eingeladene
6. Wie viele Plätze?
7. Publish. Group is instantly discoverable by interest and distance.
8. Thomas manages: participants, cancel one occurrence, message the group.
```

**No approval step, no organization, no gatekeeper.** This is the whole point of
"the platform works without organizations".

---

## J5 — Coordinator's morning

**Actor:** Elisabeth (NGO coordinator), on the web dashboard

```
1. Login → "Braucht heute Aufmerksamkeit":
     · 3 Anfragen ohne Zuordnung (älteste: 2 Tage)
     · 1 Freiwillige:r hat abgesagt — Termin in 4 Stunden
     · 2 Verifizierungen laufen diese Woche ab
     · 1 neue Freiwilligen-Bewerbung wartet auf Prüfung
2. Clicks an unmatched request → sees ranked candidates with the score breakdown
   → contacts one directly or assigns manually with a recorded reason.
3. Approves a verification: sees what was checked, records the outcome, no
   document is stored on the platform.
4. Month end: „Bericht erstellen" → PDF with hours, people, activities, fulfilment rate.
```

**This journey is the product's commercial value.** Build the dashboard around it.

---

## J6 — A concern is raised

**Actor:** Anna (volunteer) → Dr. Wagner (safeguarding officer)

```
1. After a home visit, Anna taps „Ich möchte etwas melden."
2. Category (Sorge um die Person · Verhalten · Sicherheit · Sonstiges),
   severity, free text. Explicitly NOT a rating or a public review.
3. Case created, restricted immediately. Anna sees only:
   „Danke. Eine zuständige Person kümmert sich darum."
4. Only users with the SafeguardingOfficer capability are notified.
   The OrganizationAdmin is NOT.
5. Dr. Wagner: opens case → assigns to himself → adds notes → takes actions →
   resolves. Every read and write is audited.
6. The subject of the case never learns who reported it.
```

---

## J7 — Emergency vs. Safety Alert

```
Maria taps the red button, or says something the system flags as acute:

  ┌── EMERGENCY ────────────────────────────────┐
  │ „Brauchen Sie sofort Hilfe?"                │
  │ [ 144 anrufen ]  ← huge, one tap            │
  │ [ 112 anrufen ]                             │
  │ [ Meine Kontakte anrufen ]                  │
  │ [ Nein, ich brauche keine Notfallhilfe ]    │
  └─────────────────────────────────────────────┘
       The app NEVER says help is on the way.
       The app NEVER dispatches anyone.

  Separately — SAFETY ALERT (internal workflow, no red, no siren language):
     · a scheduled activity was missed
     · a volunteer raised a concern
     · a family member requested a welfare check
   → notifies trusted contacts and/or the coordinator, per configuration.
```

---

## J8 — Blocked request → referral

```
Maria: „Ich brauche jemanden, der mir die Kompressionsstrümpfe anzieht."

  → Category detection flags a nursing task.
  → NO help request is created.
  → Screen: „Dafür braucht es ausgebildete Pflege. Das dürfen wir nicht vermitteln —
     aber hier sind Stellen in Ihrer Nähe, die das können."
     [ Liste: Mobile Pflege, Hauskrankenpflege, Sozialsprengel ]
     [ Ich möchte stattdessen etwas anderes anfragen ]
  → Logged as BlockedCategoryReferral (for product learning, not against the user).
```

Note: the referral directory is a small, manually curated, per-region dataset. Curate it
for the pilot Gemeinde only. Do not build a national directory.
