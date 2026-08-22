# Brand, Naming & Tone

## 1. Recommended name: **SeniorConnect**

`SeniorConnect` is the Austrian/Bavarian dialect form of *miteinander* — "with one another".

Why it works:

| Criterion | Assessment |
| --- | --- |
| Meaning | Literally "together". The product's entire thesis in one word. |
| Austrian identity | Instantly recognisable as Austrian, not German, not international. Gemeinden and Seniorenbund respond to this. |
| Warmth | Dialect reads as human and local, not as corporate software. Critical for a senior audience. |
| Age fit | Older Austrians use dialect naturally. It is *their* word, not a tech word. |
| Pronounceability | MIT-a-nand. Three syllables, no ambiguity, easy to say on the phone. |
| Not a health/care word | Avoids "Pflege", "Senior", "Care", "Help" — so the product does not read as "an app for frail people". This matters enormously for adoption: nobody wants to be the person who needs the senior app. |
| Extensible | Works for the platform brand *and* white-label: "Caritas Community — powered by SeniorConnect". |

**Full product name:** `SeniorConnect`
**Legal/platform name for B2B docs:** `SeniorConnect — Plattform für Nachbarschaftshilfe`
**Bundle ID:** `at.SeniorConnect.app`
**Domain targets:** `SeniorConnect.at` (primary), `SeniorConnect.eu`, `SeniorConnect.app`

> ⚠️ Before committing: check `SeniorConnect.at` availability at nic.at, run a trademark search
> at the Österreichisches Patentamt (Klasse 9, 42, 45), and check the App Store / Play Store
> for name collisions. I cannot verify these for you.

### Alternatives if SeniorConnect is unavailable

| Name | Meaning | Note |
| --- | --- | --- |
| **Beisamm** | dialect for "beisammen" (together) | Same logic, softer sound |
| **Vis-a-Vis** | the person across from you | Elegant, but reads French/urban |
| **Grätzl** | Viennese for neighbourhood | Too Vienna-specific for a Tirol pilot |
| **Nachbarschafft** | pun: Nachbarschaft + schaffen | Clever but hard to spell on the phone |
| **Weggefährt** | companion on the way | Beautiful, slightly literary/heavy |
| **Servus** | Austrian greeting | Warm, but too generic to trademark |

### Names to avoid

`SeniorConnect` (the working title in the ideation docs) — it labels the user as old,
sounds like enterprise software, is English in a German market, and is generic enough that
trademarking will be difficult. Use it only as an internal codename if at all.

---

## 2. Tagline

Primary (DE): **„Hilfe von nebenan."**
Secondary (DE): „Gemeinsam geht's leichter."
EN: "Help from next door."

---

## 3. Logo direction

Do not commission an illustration. Commission a **mark that survives at 24 px in a
notification bar and at 512 px on a poster in a Gemeindeamt.**

Recommended concept: **two overlapping rounded forms** — one slightly larger, one smaller,
sharing an overlap area in the accent colour. Reads as: two people, a handshake, a Venn
overlap of "need" and "offer". Abstract enough to never look condescending.

Constraints:

- Must work in single colour (fax, stamp, embroidery on a volunteer vest)
- Must work inverted on the dark-mode surface
- No hearts, no hands cupping, no walking-stick silhouettes, no generational
  "young hand holds old hand" imagery — these are patronising and every NGO already uses them
- App icon: mark on `#14625A`, no text

---

## 4. Voice & tone

The app talks like a **competent, warm neighbour**, not like a hospital and not like a startup.

| Do | Don't |
| --- | --- |
| „Ihre Anfrage ist unterwegs." | „Request submitted successfully." |
| „Anna kommt am Dienstag um 10:00." | „Assignment confirmed. Volunteer: Anna M." |
| „Das hat leider nicht geklappt. Versuchen Sie es bitte nochmal." | „Error 500: Internal Server Error" |
| „Möchten Sie das wirklich absagen?" | „Are you sure?" |
| Sie-Form for seniors, always | Du-Form (too familiar for this generation in Austria) |

Rules:

1. **Sie**, never **Du**, in German — for all user-facing text. This is non-negotiable for
   an Austrian 70+ audience. (Volunteer-facing screens may use Du if research supports it,
   but default to Sie.)
2. Maximum one idea per sentence. Target B1 German / *Leichte Sprache*-adjacent.
3. Never use a technical noun in a senior-facing string ("Synchronisierung", "Token",
   "Profil aktualisiert"). Say what happened, in a verb.
4. Every error message must say **what to do next**, not what went wrong internally.
5. Never use time pressure ("Nur noch 2 Minuten!", countdowns, streaks). This is an
   anti-pattern for the target group and for a social-care product.
6. No gamification badges for seniors. Recognition for volunteers is fine and is opt-in.
