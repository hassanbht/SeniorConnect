# SeniorConnect — AI Development Guide

> This file is the entry point for any AI coding agent (Codex, Claude Code, Aider,
> OpenCode) working in this repository. Keep it short. Deep knowledge lives in `docs/`.
> Nested `AGENTS.md` files in `backend/` and `mobile/` add stack-specific rules.

---

## Project mission

SeniorConnect is a trusted local platform where seniors, families, volunteers and social
organizations organise everyday help and shared activities.

**The platform MUST provide value when no organization is present.**
Organizations are an optional extension, never a core dependency.

---

## Source of truth

Before implementing or modifying anything, read:

1. `00-PRODUCT-BRIEF.md`
2. `docs/product/vision.md`
3. `docs/product/business-rules.md`      ← binding rules
4. `docs/product/discovery-findings.md`  ← the evidence, and its confidence level
5. `docs/product/user-journeys.md`
6. `docs/architecture/system-design.md`
7. `docs/architecture/data-model.md`
8. `docs/architecture/authorization.md`
9. `docs/architecture/trust-safety.md`
10. `docs/design/design-system.md`         ← for anything user-facing
11. relevant ADRs in `docs/decisions/`

**If your implementation conflicts with these documents, stop.**
Do not silently change architecture or business rules. Explain the conflict and propose
an ADR.

---

## Stack

```
Backend    ASP.NET Core 10 · C# · EF Core 10 · PostgreSQL 16 · Hangfire
Architecture  Modular monolith, domain-oriented modules, explicit boundaries
Mobile     Flutter · feature-first · Cubit + Freezed · go_router · get_it/injectable
           easy_localization (de source of truth) · Drift (from Phase 7)
Web        Blazor or React staff dashboard (Phase 6)
```

Do not introduce microservices, a message broker, GraphQL, a new state-management library,
or a new ORM without an approved ADR.

---

## Core principles

1. Safety before convenience.
2. Accessibility is architecture, not a feature.
3. Organizations are optional.
4. Trust level, safety level and authorization are computed **server-side only**.
5. The client is never the source of truth for anything that matters.
6. Safeguarding data is isolated from everything else.
7. Every important state transition is auditable.
8. Prefer the simplest solution that satisfies the current phase.
9. Do not implement future-phase features without an approved plan.
10. Explain refusals: a 403 must tell the user what is missing and how to fix it.

---

## Required workflow

```
1. Read the relevant documentation.
2. Identify affected modules and bounded contexts.
3. Write or update a plan in docs/plans/active/.
4. List your assumptions explicitly.
5. Implement the smallest coherent change.
6. Add tests — including the negative authorization tests.
7. Run build, tests, analyzer, formatter.
8. Update documentation if behaviour changed.
9. Summarise what changed and what you did NOT do.
```

Never implement a large feature in one uncontrolled change.
Never touch more than one module in a single change without saying why.

---

## Forbidden

```
✗ Business logic in controllers or in widgets
✗ Duplicating business rules between the API and Flutter
✗ Trusting userId, role, trustLevel, safetyLevel or capabilities from a client
✗ Cross-module database access without an ADR
✗ Adding a dependency without explaining why in the PR description
✗ Storing verification documents, ID scans or criminal-record certificates
✗ Exposing safeguarding data to ordinary administrators
✗ Implementing medical or nursing workflows as ordinary volunteer tasks
✗ Hardcoded user-visible strings — everything goes through easy_localization
✗ Hardcoded colours — everything comes from the theme
✗ Disabling text scaling (TextScaler.noScaling is a build-breaking defect)
✗ Background location tracking
✗ Third-party analytics, advertising or tracking SDKs
✗ Public five-star ratings of people
✗ Password fields in senior-facing or volunteer-facing screens (BR-AUTH-03)
✗ Reporting total roster size as if it were active volunteers (BR-ROSTER-03)
✗ Any personally identifying field reachable from the funder namespace (BR-FUNDER-02)
✗ Marketing, engagement or "we miss you" push notifications (BR-NOTIFY-01)
✗ Real personal data in dev or staging
```

---

## Definition of Done

A change is complete only when:

```
[ ] Business rules are documented and respected
[ ] Backend implementation is complete and layered correctly
[ ] Authorization is enforced server-side and tested (401 / 403 / 404 / happy path)
[ ] Unit tests for business rules, integration tests for critical workflows
[ ] Error cases return ProblemDetails with a stable code
[ ] Audit requirements considered and implemented
[ ] API contract (OpenAPI) updated
[ ] Flutter: all three locales, LTR + RTL, light + dark, text scale 1.0/1.5/2.0
[ ] Flutter: loading, empty and error states exist
[ ] Flutter: semantic labels, 48dp/64dp touch targets
[ ] Documentation updated
```

---

## Current phase

> **Update this line every phase. An agent that does not know the phase will build the
> wrong thing.**

```
CURRENT PHASE: 0 — Discovery & Project Brain (partially complete)
IN SCOPE:  documentation, remaining interviews, repository setup, CI
OUT OF SCOPE: all feature code

BLOCKED PHASES: 5 (Community) and 6 (Family) — no seniors and no family
members have been interviewed. Do not build these on assumption.
```

> The roadmap was re-cut after discovery (ADR-015). Phase 2 is now the
> **Coordinator Wedge**, not Community. If you are working from an older
> understanding of the phase order, re-read `docs/product/roadmap.md`.

See `docs/product/roadmap.md` for the phase definitions and their test gates.
See `docs/plans/BUILD-CHECKLIST.md` for the ordered task list. When asked to
implement something, first locate its task ID there. **If the task belongs to a
later phase, say so and stop** rather than building it early.
