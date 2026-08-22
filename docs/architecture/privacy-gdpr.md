# Privacy & GDPR Architecture

> Not legal advice. This is an engineering design that makes compliance achievable.
> The legal work must be done by an Austrian lawyer and, given the vulnerable-population
> context, most likely a DPO.

## 1. Data classification

| Class | Examples | Access |
| --- | --- | --- |
| `PublicProfile` | first name, area, interests, languages | any authenticated user |
| `PersonalData` | full name, address, phone, DOB | self, granted family, assigned counterparty (post-assignment), staff with a logged reason |
| `SensitiveData` | verification outcomes, reliability components | self, authorised staff |
| `HealthRelated` | mobility need, vulnerability flag | **avoided by design**; staff with an explicit reason only |
| `Safeguarding` | concerns, cases, notes | Safeguarding Officers only, every read logged |

Each class maps to an EF entity annotation and to an authorization policy. A field with no
classification cannot be added — the architecture test fails.

## 2. Data minimisation, concretely

- No ID documents stored. Only `verification.status + provider + reference` (BR-TRUST-05).
- No diagnoses. `mobility_note` records a **functional need**
  („braucht eine Begleitperson beim Gehen"), never „Parkinson im Stadium 2".
- Address is stored once, on the profile. Help requests reference it; they do not copy it.
- Phone numbers are revealed only after assignment, only to the counterparty.
- Location is captured **only** at check-in/check-out, never continuously, never in the
  background. No `ACCESS_BACKGROUND_LOCATION` permission is requested. Ever.
- Photos are optional everywhere.

## 3. Controller / processor map

```
Independent community use (no organization)
   → the platform operator is the Controller

Organization-scoped use
   → the organization is the Controller for its clients' and volunteers' data
   → the platform operator is the Processor  → a DPA is required per organization

Safeguarding data
   → always the organization's, where an organization exists
   → for independent cases, the operator, under a documented policy
```

Write this down per participant before the pilot. Ambiguity here is the single most
common way social-sector software projects die at the legal review.

## 4. Consent

- Versioned documents; each consent row stores the version accepted.
- Separate, granular consents: terms, privacy policy, notification channels, optional
  research/statistics participation.
- Withdrawal is one tap and takes effect immediately.
- Consent is **not** the legal basis for the core service (that is contract / legitimate
  interest / and for safeguarding, likely legal obligation or vital interest) — but it is
  required for optional processing. Do not over-rely on consent; get the basis per purpose
  reviewed.

## 5. Subject rights

| Right | Implementation |
| --- | --- |
| Access | in-app "Meine Daten" + JSON/PDF export |
| Rectification | in-app editing of everything editable |
| Erasure | two-tier deletion (below) |
| Portability | JSON export |
| Objection / restriction | account deactivation, matching opt-out |
| Information | plain-language privacy page, written at B1 level |

### Two-tier deletion

```
Tier 1 — immediate
  profile, address, phone, email, photos, messages, interests → erased
  display name → "Gelöschtes Konto"

Tier 2 — retained under a documented legal basis, pseudonymised
  audit entries        (accountability)
  safeguarding cases   (protection of others; retention policy defined per organization)
  aggregated hours     (organizations' reporting obligations)
  → user_id replaced with a one-way pseudonym; no path back to a person
```

Tell the user exactly this, in plain German, before they confirm.

## 6. Technical measures

```
Transport      TLS 1.3, HSTS, certificate pinning in the mobile app
At rest        full-disk encryption + pgcrypto for the most sensitive columns
Secrets        a managed vault, never in appsettings, never in the repository
Passwords      Argon2id or ASP.NET Identity defaults with a raised work factor
Tokens         short-lived access, rotating refresh, revocable per device
Backups        encrypted, EU region, restore tested quarterly
Logs           never contain personal data; IPs stored only as a salted hash
Access         least privilege; production access is logged and requires a reason
Retention      scheduled jobs per data class
Hosting        EU only, contractually verified
Third parties  minimal, each with a DPA, none for analytics or advertising
```

## 7. DPIA triggers present in this product

All of these are present, so plan for a DPIA:

- systematic processing of data about **vulnerable persons**
- large-scale processing of location data (even if bounded)
- data that can reveal health-related information
- matching/scoring that affects whether a person receives a service
- safeguarding data with a serious impact on individuals

## 8. Non-negotiables

```
✗ No third-party analytics SDK
✗ No advertising SDK
✗ No runtime font/asset loading from a non-EU CDN
✗ No background location
✗ No selling, sharing or "anonymised" licensing of user data
✗ No dark patterns in consent
✗ No real personal data in dev or staging
```
