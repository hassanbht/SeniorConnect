# SeniorConnect

> Eine digitale Plattform für Nachbarschaftshilfe und soziale Teilhabe.
> Ältere Menschen, Angehörige, Freiwillige und soziale Organisationen finden
> zueinander — mit überprüfter Identität, klaren Sicherheitsstufen und
> nachvollziehbarer Dokumentation.

This repository is the **Project Brain**: the product decisions, architecture,
business rules, design system and agent instructions that all code must follow.

---

## Read in this order

| # | File | What it answers |
| --- | --- | --- |
| 1 | [`00-PRODUCT-BRIEF.md`](00-PRODUCT-BRIEF.md) | What are we building, why, for whom, and what did we change from the original idea |
| 2 | [`docs/product/vision.md`](docs/product/vision.md) | Mission, principles, non-goals |
| 3 | [`docs/product/personas.md`](docs/product/personas.md) | Who the users actually are |
| 4 | [`docs/product/user-journeys.md`](docs/product/user-journeys.md) | The flows that must work |
| 5 | [`docs/product/discovery-findings.md`](docs/product/discovery-findings.md) | **The evidence** — twelve findings, each with a confidence level |
| 6 | [`docs/product/business-rules.md`](docs/product/business-rules.md) | **Binding rules.** Code that violates one is a defect |
| 7 | [`docs/architecture/system-design.md`](docs/architecture/system-design.md) | Stack, modules, boundaries |
| 8 | [`docs/product/roadmap.md`](docs/product/roadmap.md) | **What to build, in order, with a test gate per phase** |
| 9 | [`docs/plans/BUILD-CHECKLIST.md`](docs/plans/BUILD-CHECKLIST.md) | **⭐ The file you actually work from** — 157 ordered tasks, each with its own test |
| 10 | [`docs/design/design-system.md`](docs/design/design-system.md) | Colours (light + dark), typography, components |
| 11 | [`AGENTS.md`](AGENTS.md) | How an AI coding agent must behave here |

---

## Full contents

```
00-PRODUCT-BRIEF.md          executive summary + the six market corrections
AGENTS.md                    root agent instructions
backend/AGENTS.md            ASP.NET Core rules
mobile/AGENTS.md             Flutter rules

docs/product/
  vision.md                  mission, principles, non-goals
  personas.md                8 personas + the anti-persona
  user-journeys.md           J1–J8, the flows that must work
  discovery-findings.md      F1–F12, confidence levels, verification plan
  business-rules.md          BR-SCOPE / TRUST / SAFETY / HELP / FAMILY / SG /
                             TENANT / AUDIT / COMM / GDPR  + v2: TRANSPORT /
                             KEYS / EXPENSE / FUNDER / AUTH / NOTIFY / ROSTER /
                             ONBOARD / VISIBILITY
  glossary.md                fixed vocabulary, incl. the German UI strings
  roadmap.md                 v2 — phases 0–8, scope + test gate per phase
                             (re-cut after discovery, see ADR-015)
  go-to-market.md            cold start, channels, pilot plan, pricing

docs/architecture/
  system-design.md           modular monolith, folder layout, tech decisions
  data-model.md              full ERD, all phases, with indexes
  authorization.md           the decision pipeline, capabilities, the matrix
  trust-safety.md            trust engine, safety levels, safeguarding, buddy rule
  matching-engine.md         hard filters, scoring, offer tiers, explainability
  privacy-gdpr.md            data classes, controller map, two-tier deletion

docs/design/
  brand.md                   naming rationale, alternatives, logo direction, tone
  design-system.md           colour tokens light/dark/high-contrast, type, spacing
  accessibility.md           WCAG targets, Senior Mode spec, testing protocol

docs/api/
  api-conventions.md         REST, errors, pagination, concurrency, idempotency
  endpoints.md               the full endpoint surface, by phase

docs/plans/
  BUILD-CHECKLIST.md         ⭐ 157 ordered tasks · 9 gates · tick as you go

docs/decisions/
  README.md                  ADR index (17 accepted decisions)
  ADR-015-roadmap-recut.md   why Phase 2 is now the coordinator wedge
  ADR-016-passwordless-auth.md
  ADR-017-funder-scope.md    a Gemeinde is a funder, not a tenant
docs/plans/                  active/ and completed/ feature plans

skills/
  SeniorConnect-feature/          plan-before-code workflow + Definition of Done
  SeniorConnect-api/              endpoint authoring rules + required tests
  SeniorConnect-flutter-ux/       design system, dark mode, Senior Mode, i18n, RTL, a11y
  SeniorConnect-trust-safety/     the highest-risk area — read before touching it
  SeniorConnect-database/         schema, migrations, query filters, indexing
  SeniorConnect-code-review/      classified findings, no code changes

prompts/agent-prompts.md     7 ready-to-use prompts, incl. one for agent drift

starter/
  flutter/app_colors.dart    all colour tokens + ThemeExtension
  flutter/app_tokens.dart    spacing, radius, touch targets, breakpoints, type
  flutter/app_theme.dart     ThemeData for light / dark / high-contrast × Senior Mode
  flutter/app_settings.dart  persisted theme mode + Senior Mode
  flutter/main.dart          bootstrap: i18n, theming, Senior Mode, text scaling
  flutter/app_widgets.dart   AppButton · AppTextField · AppStatusChip · TrustBadge
                             AppLoading · AppEmptyState · AppErrorView · AppConfirmSheet
  flutter/senior_shell.dart  SeniorHome (max 5 actions) · SeniorScaffold
  i18n/de.json               122 keys — German is the source of truth
  i18n/en.json               parity verified
  i18n/fa.json               parity verified, RTL
  i18n/check_locales.py      CI parity checker (missing key = build failure)
  sql/001_phase1_init.sql    Phase 1 schema  — VERIFIED on PostgreSQL 16.15
  sql/002_phase2_wedge.sql   Phase 2 schema  — VERIFIED on PostgreSQL 16.15
  sql/900_schema_tests.sql   9 constraint tests, all passing
  backend/                   .NET 10 skeleton — NOT compiled, see its README
    src/ modules/ tests/     Activity slice + 5 architecture test suites
```

---

## Where to start, concretely

```bash
# 1. Create the repository and drop this tree in
git init SeniorConnect && cd SeniorConnect
# copy the contents of this bundle here
git add . && git commit -m "Project brain"

# 2. Run the Phase 0 bootstrap prompt from prompts/agent-prompts.md (P0)
#    against your agent of choice. It will tell you what is still ambiguous.

# 3. Finish Phase 0. discovery-findings.md currently holds SIMULATED answers.
#    ONE real call to a coordinator verifies five of the twelve findings.
#    Five seniors and five family members are still missing — those two gaps
#    block Phase 5 and Phase 6.

# 4. Freeze the MVP scope. Update the CURRENT PHASE block at the bottom
#    of AGENTS.md.

# 5. Open docs/plans/BUILD-CHECKLIST.md and work top to bottom.
#    Start at P1-01. Do not skip ahead — later tasks assume earlier ones.
```

---

## Non-negotiables (the short version)

```
Safety before convenience.
Accessibility is architecture, not a feature.
Organizations are optional — the platform must work without them.
Trust level, safety level and authorization are computed server-side ONLY.
Safeguarding data is isolated from everything else.
Non-medical, non-nursing, unpaid. Blocked categories return a referral.
Emergency routes to 144/112 — the app never dispatches anyone.
No verification documents are stored. Outcomes only.
No background location. No third-party analytics. No public star ratings.
Dark mode and Senior Mode ship in Phase 1, not later.
No password fields for seniors or volunteers. Login killed the last two tools.
Report active volunteers, never roster size. A funder must never see a name.
German is the localisation source of truth.
No real personal data before the Phase 7 legal gate.
```

---

## Caveats you must resolve yourself

This bundle contains engineering and product design, not legal, insurance or
regulatory advice. Before a pilot with real people you need to independently verify:

- the domain `SeniorConnect.at`, a trademark search, and App/Play Store name collisions
- whether Barrierefreiheitsgesetz applies to this product (build to the standard regardless)
- the exact ID Austria Service Provider registration and accreditation requirements
- volunteer accident and liability insurance, with an Austrian broker
- your data-controller / processor structure and whether a DPIA is required
- the boundary between neighbourly help and regulated care work, with a lawyer

Every legal statement in these documents is a starting point for that
conversation, not a conclusion.
