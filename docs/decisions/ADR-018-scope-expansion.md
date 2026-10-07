# ADR-018 — Scope expansion: from "app for seniors" to a general mutual-aid platform

Status: Accepted
Date: 2026-08
Refines: 00-PRODUCT-BRIEF.md, docs/product/vision.md, docs/product/personas.md,
docs/product/business-rules.md §1, docs/architecture/data-model.md §1

## Context

The founding brief and early roadmap framed the product around one primary
user: an isolated senior in Austria. Personas, business rules, categories and
even table names (`senior_profiles`) were built around that framing.

The person has since reconsidered the product's edges and wants it to also
serve: **migrants and newcomers integrating into Austrian life, families
across generations, independent volunteers, mentors, language-learning
partners, and local community groups** — with seniors as one important group
among several, not the organizing category.

This arrives at a fortunate moment: per `docs/plans/PHASE-AUDIT-2026-08.md`,
the mobile screens for the core Help & Matching loop (`P3-21`–`P3-24`) were
never actually built, and Phase 7 hardening never reached real data. **This
generalization is far cheaper now than it would be after 20+ senior-specific
screens ship.** Doing it now is the right call; doing it after Phase 3's
mobile UI ships would have meant redoing that UI.

## Decision

### 1. Reframing, not rebuilding

The platform's purpose becomes:

> **A trusted local platform where people who need everyday help or
> connection — because of age, new arrival, isolation, disability, or
> circumstance — find it from people willing to give it, safely and
> verifiably, with or without an organization's help.**

Everything already built survives this change **almost unchanged**, because
the architecture was already more general than the naming suggested:

| Already general (keep as-is) | Was senior-specific in naming only |
| --- | --- |
| `interests`, `languages`, `skills` reference tables | `senior_profiles` table name |
| `CommunityGroup`, `Event` (Phase 5) — scope was never age-restricted | Persona docs framed around Maria/Sabine as the default case |
| `Activity`, `HelpRequest` — category-driven, not audience-driven | Category catalogue (shopping, doctor, accompaniment) skewed toward elderly-care errands |
| Trust levels, Safety levels, Safeguarding — apply to any vulnerable interaction | Onboarding copy ("Für wen sind Sie hier?") offered only senior/family/volunteer framings |
| `mobility_note` as a *functional*, non-diagnostic field (BR-GDPR-02) | — |

The one real schema change is a rename, not a redesign (§3).

### 2. What does NOT change

- **BR-SCOPE-01 stays**: non-medical, non-nursing, unpaid help only. A
  newcomer needing help with a Behördengang is Safety Level 3 exactly like a
  senior needing the same — the rule was never age-specific.
- **The trust and safety architecture is unchanged.** A vulnerable adult is a
  vulnerable adult regardless of why.
- **Senior Mode ("Große Ansicht") remains a device preference**, not a user
  category — this was already correct (ADR from `design-system.md` §2.5) and
  now clearly generalizes: a newcomer volunteer with low vision benefits from
  it exactly as a senior does.
- **No existing Phase 1–7 work is thrown away.**

### 3. Schema change: `senior_profiles` → `support_profiles`

Renamed, not restructured. Same columns, broadened meaning, documented in
`data-model.md`. See `starter/sql/003_scope_generalization.sql` for the
verified migration.

```
senior_profiles  →  support_profiles
```

Rationale for renaming rather than just re-labelling in the UI: a table
called `senior_profiles` holding a Ukrainian newcomer's address is a
misleading artifact the day someone else reads the schema — including a
future data-protection auditor. The rename costs one migration; leaving it
costs an explanation forever.

`volunteer_profiles` is unchanged — "volunteer" was never age-specific.

### 4. New reference data, not new tables

Two additions cover the newcomer/mentoring use cases without new aggregates:

- `activity_categories`: add `language_practice`, `newcomer_orientation`,
  `mentoring` (all Safety Level 1–2 — public or semi-public, low risk)
- `interests`: add `language_exchange`, `local_orientation`, `job_search_support`

Both slot into the existing `Activity`/`HelpRequest`/`CommunityGroup`
machinery unchanged (BR-SCOPE-02 blocked-category logic, safety-level
mapping, matching engine — none of it is category-count-sensitive).

### 5. Hard new rule: forbidden fields

This is the one place the expanded audience introduces real new risk. A
platform now serving migrants must **never** collect, store, or expose:

```
residency status · asylum status · visa type · citizenship ·
country of origin as a mandatory field · ethnicity · religion ·
immigration case number · anything resembling it
```

This is not the same caution as `mobility_note` (BR-GDPR-02) — this is a
category of data whose exposure or breach could have **immigration and legal
consequences** for a real person, not just a privacy inconvenience. Added as
`BR-GDPR-07` in `business-rules.md`, enforced the same way `BR-FUNDER-02` is
enforced: an architecture test that fails the build if a forbidden field name
pattern appears on any entity (see §7).

**Native language** remains fully fine to collect (it already exists via
`languages`/`user_languages` and exists to serve the person — matching them
with someone who speaks it). The line is: *serve the person* (language,
interests, availability) vs. *classify the person's legal status* (never).

### 6. Onboarding copy changes, not new screens

The single "Für wen sind Sie hier?" (`Who are you here for?`) screen from
`user-journeys.md` J1 gets two more options, using the same UI pattern
already built:

```
Ich möchte jemanden unterstützen        (unchanged — family/carer path)
Ich brauche Unterstützung               (was "senior", now general)
Ich bin neu in Österreich               (NEW — newcomer path)
Ich möchte helfen / mitmachen           (unchanged — volunteer path)
```

The "Ich bin neu in Österreich" path leads to the same
`support_profiles` record as every other "I need support" path — it is a
framing difference in copy, not a different data model or a different
verification tier.

### 7. Architecture test addition

```csharp
// NoSensitiveMigrationDataTests.cs — same walk-the-graph technique as
// FunderApiSurfaceTests, applied to every persisted entity, not just the
// funder namespace.

private static readonly string[] ForbiddenFieldPatterns =
[
    "residency", "asylum", "visastatus", "citizenship",
    "immigrationstatus", "ethnicity", "religion", "casenumber",
];

[Fact]
public void No_entity_persists_a_forbidden_sensitive_field()
{
    var offenders = AllEntityProperties()
        .Where(p => ForbiddenFieldPatterns.Any(f =>
            p.Name.ToLowerInvariant().Contains(f)))
        .Select(p => $"{p.DeclaringType!.Name}.{p.Name}");

    offenders.Should().BeEmpty(
        "this field category can carry immigration or legal consequences "
        + "for a real person if it exists at all, regardless of access "
        + "control — see ADR-018 §5");
}
```

## Consequences

**Positive:** the product now honestly describes what it already mostly was.
Marketing, personas and the pitch stop under-selling the platform to
Caritas-style partners who serve a broader population than seniors alone.
The rename is cheap now and expensive later.

**Negative, accepted:** the category catalogue, referral directory
(`referral_providers`) and onboarding copy all need a review pass before
Phase 3's mobile screens are built, adding roughly 3–5 days of work
(`docs/plans/BUILD-CHECKLIST.md`, new §PSG). This is explicitly sequenced
**before** `P3-21`, not after — see the checklist update.

**Explicitly out of scope for this ADR:** full translation into additional
languages (Turkish, Arabic, Bosnian/Croatian/Serbian, Ukrainian) for the
newcomer audience. Machine-translating 122 UI strings and presenting them as
finished localization would be worse than not having them — a wrong or
tone-deaf translation actively damages trust with the audience it's meant to
serve. This is tracked as its own gated task requiring native-speaker review,
not solved by an AI agent alone. See `docs/design/localization-expansion.md`.

## Alternatives considered

**Launch a second, separate app for newcomers.** Rejected: duplicates the
entire trust, safety, matching and safeguarding architecture for no
structural reason — a newcomer and a senior both need "someone verified to
help me with X, safely, nearby." The mutual-aid mechanism is identical; only
the category and the onboarding copy differ.

**Keep `senior_profiles` and add a comment.** Rejected: a comment does not
survive a schema export, a new developer's first `\d` in `psql`, or a future
data-protection audit. The rename is one migration; the confusion from not
renaming compounds indefinitely.
